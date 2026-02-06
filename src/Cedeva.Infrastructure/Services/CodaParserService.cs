using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace Cedeva.Infrastructure.Services;

/// <summary>
/// Service de parsing des fichiers CODA (format bancaire belge).
/// Spécification: lignes de 128 caractères avec positions fixes.
/// </summary>
public class CodaParserService : ICodaParserService
{
    // CODA Format Constants
    private const int CodaLineLength = 128;

    // Header positions (Record Type 0)
    private const int HeaderAccountNumberStart = 5;
    private const int HeaderAccountNumberLength = 12;
    private const int HeaderStatementDateStart = 97;
    private const int HeaderStatementDateLength = 6;

    // Old Balance positions (Record Type 1)
    private const int OldBalanceAmountStart = 42;
    private const int OldBalanceAmountLength = 15;
    private const int OldBalanceSignPosition = 41;
    private const int DecimalPlaces = 3;
    private const decimal DecimalDivisor = 1000m;

    // Date parsing
    private const int DateDayLength = 2;
    private const int DateMonthStart = 2;
    private const int DateMonthLength = 2;
    private const int DateYearStart = 4;
    private const int DateYearLength = 2;
    private const int DateYearBase = 2000;

    private readonly CedevaDbContext _context;
    private readonly ILogger<CodaParserService> _logger;

    public CodaParserService(CedevaDbContext context, ILogger<CodaParserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CodaFileDto> ParseCodaFileAsync(Stream fileStream, string fileName)
    {
        var codaFile = new CodaFileDto { FileName = fileName };
        var currentTransaction = new CodaTransactionDto();
        var additionalInfo = new StringBuilder();

        using var reader = new StreamReader(fileStream, Encoding.GetEncoding("ISO-8859-1"));
        string? line;
        int lineNumber = 0;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            lineNumber++;

            if (line.Length < CodaLineLength)
            {
                _logger.LogWarning("Line {LineNumber} is too short ({Length} chars), skipping", lineNumber, line.Length);
                continue;
            }

            var recordType = line.Substring(0, 1);

            try
            {
                switch (recordType)
                {
                    case "0": // Header
                        ParseHeader(line, codaFile);
                        break;

                    case "1": // Old balance
                        ParseOldBalance(line, codaFile);
                        break;

                    case "2": // Transaction (mouvement)
                        SaveCurrentTransaction(currentTransaction, additionalInfo, codaFile);
                        currentTransaction = ParseTransaction(line);
                        break;

                    case "3": // Additional information
                        ParseAdditionalInfo(line, currentTransaction, additionalInfo);
                        break;

                    case "8": // New balance
                        ParseNewBalance(line, codaFile);
                        break;

                    case "9": // Footer
                        SaveCurrentTransaction(currentTransaction, additionalInfo, codaFile);
                        break;

                    default:
                        _logger.LogWarning("Unknown record type '{RecordType}' at line {LineNumber}", recordType, lineNumber);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing line {LineNumber}: {Line}", lineNumber, line);
                throw new InvalidOperationException($"Error parsing CODA file at line {lineNumber}", ex);
            }
        }

        _logger.LogInformation("Parsed CODA file: {TransactionCount} transactions", codaFile.Transactions.Count);
        return codaFile;
    }

    /// <summary>
    /// Saves the current transaction to the CODA file if it has data.
    /// Appends any accumulated additional information before saving.
    /// </summary>
    private static void SaveCurrentTransaction(
        CodaTransactionDto currentTransaction,
        StringBuilder additionalInfo,
        CodaFileDto codaFile)
    {
        if (currentTransaction.TransactionCode != null)
        {
            if (additionalInfo.Length > 0)
            {
                currentTransaction.FreeCommunication = additionalInfo.ToString().Trim();
                additionalInfo.Clear();
            }
            codaFile.Transactions.Add(currentTransaction);
        }
    }

    private void ParseHeader(string line, CodaFileDto codaFile)
    {
        // Position 6-17: Account number (12 chars)
        codaFile.AccountNumber = line.Substring(HeaderAccountNumberStart, HeaderAccountNumberLength).Trim();

        // Position 98-103: Statement date (DDMMYY)
        var datePart = line.Substring(HeaderStatementDateStart, HeaderStatementDateLength);
        if (int.TryParse(datePart, out _))
        {
            var day = int.Parse(datePart.Substring(0, DateDayLength));
            var month = int.Parse(datePart.Substring(DateMonthStart, DateMonthLength));
            var year = DateYearBase + int.Parse(datePart.Substring(DateYearStart, DateYearLength));
            codaFile.StatementDate = new DateTime(year, month, day);
        }
    }

    private void ParseOldBalance(string line, CodaFileDto codaFile)
    {
        // Position 43-57: Old balance (15 chars: 12 digits + 3 decimals)
        var balanceStr = line.Substring(OldBalanceAmountStart, OldBalanceAmountLength);
        if (decimal.TryParse(balanceStr, out var balance))
        {
            codaFile.OldBalance = balance / DecimalDivisor; // 3 decimals implied
        }

        // Position 42: Debit/Credit (0=Credit, 1=Debit)
        if (line.Length > OldBalanceSignPosition && line[OldBalanceSignPosition] == '1')
        {
            codaFile.OldBalance = -codaFile.OldBalance;
        }
    }

    private CodaTransactionDto ParseTransaction(string line)
    {
        var transaction = new CodaTransactionDto();

        // Position 2-7: Transaction sequence number (skip for now)

        // Position 8-13: Reference (skip for now)

        // Position 14-21: Transaction date (DDMMYYYY or DDMMYY)
        var transDateStr = line.Substring(13, 6);
        if (int.TryParse(transDateStr, out _))
        {
            var day = int.Parse(transDateStr.Substring(0, 2));
            var month = int.Parse(transDateStr.Substring(2, 2));
            var year = 2000 + int.Parse(transDateStr.Substring(4, 2));
            transaction.TransactionDate = new DateTime(year, month, day);
        }

        // Position 32-37: Value date (DDMMYY)
        var valueDateStr = line.Substring(31, 6);
        if (int.TryParse(valueDateStr, out _))
        {
            var day = int.Parse(valueDateStr.Substring(0, 2));
            var month = int.Parse(valueDateStr.Substring(2, 2));
            var year = 2000 + int.Parse(valueDateStr.Substring(4, 2));
            transaction.ValueDate = new DateTime(year, month, day);
        }
        else
        {
            transaction.ValueDate = transaction.TransactionDate;
        }

        // Position 32: Debit/Credit (0=Credit/Income, 1=Debit/Expense)
        var debitCredit = line.Length > 31 ? line[31] : '0';

        // Position 33-47: Amount (15 chars: 12 digits + 3 decimals)
        var amountStr = line.Substring(32, 15);
        if (decimal.TryParse(amountStr, out var amount))
        {
            transaction.Amount = amount / 1000m; // 3 decimals implied
            if (debitCredit == '1')
            {
                transaction.Amount = -transaction.Amount; // Debit = negative
            }
        }

        // Position 62-69: Transaction code (8 chars, but we keep first 2-3)
        transaction.TransactionCode = line.Substring(61, 8).Trim();

        // Position 113-125: Structured communication (13 chars)
        var structuredComm = line.Substring(112, 13).Trim();
        if (!string.IsNullOrWhiteSpace(structuredComm) && structuredComm != "0")
        {
            // Format: XXXXXXXXXXXCC (11 digits + 2 checksum)
            // Convert to +++XXX/XXXX/XXXXX+++
            if (structuredComm.Length == 12 && structuredComm.All(char.IsDigit))
            {
                transaction.StructuredCommunication = $"+++{structuredComm.Substring(0, 3)}/{structuredComm.Substring(3, 4)}/{structuredComm.Substring(7, 5)}+++";
            }
        }

        return transaction;
    }

    private void ParseAdditionalInfo(string line, CodaTransactionDto transaction, StringBuilder additionalInfo)
    {
        var infoType = line.Substring(1, 1);

        switch (infoType)
        {
            case "1": // Counterparty information
                // Position 11-73: Counterparty name/info
                var counterpartyInfo = line.Substring(10, 63).Trim();
                if (!string.IsNullOrWhiteSpace(counterpartyInfo))
                {
                    if (string.IsNullOrEmpty(transaction.CounterpartyName))
                    {
                        transaction.CounterpartyName = counterpartyInfo;
                    }
                    else
                    {
                        transaction.CounterpartyName += " " + counterpartyInfo;
                    }
                }
                break;

            case "2": // Communication
                // Position 11-73: Free communication
                var communication = line.Substring(10, 63).Trim();
                if (!string.IsNullOrWhiteSpace(communication))
                {
                    if (additionalInfo.Length > 0)
                    {
                        additionalInfo.Append(" ");
                    }
                    additionalInfo.Append(communication);
                }
                break;

            case "3": // Counterparty account number
                // Position 11-47: Account number
                var accountNumber = line.Substring(10, 37).Trim();
                if (!string.IsNullOrWhiteSpace(accountNumber))
                {
                    transaction.CounterpartyAccount = accountNumber;
                }
                break;
        }
    }

    private void ParseNewBalance(string line, CodaFileDto codaFile)
    {
        // Position 43-57: New balance (15 chars: 12 digits + 3 decimals)
        var balanceStr = line.Substring(42, 15);
        if (decimal.TryParse(balanceStr, out var balance))
        {
            codaFile.NewBalance = balance / 1000m; // 3 decimals implied
        }

        // Position 42: Debit/Credit (0=Credit, 1=Debit)
        if (line.Length > 41 && line[41] == '1')
        {
            codaFile.NewBalance = -codaFile.NewBalance;
        }
    }

    public async Task<int> ImportCodaFileAsync(CodaFileDto codaData, int organisationId, int userId)
    {
        var codaFile = new CodaFile
        {
            OrganisationId = organisationId,
            FileName = codaData.FileName,
            ImportDate = DateTime.UtcNow,
            StatementDate = codaData.StatementDate,
            AccountNumber = codaData.AccountNumber,
            OldBalance = codaData.OldBalance,
            NewBalance = codaData.NewBalance,
            TransactionCount = codaData.Transactions.Count,
            ImportedByUserId = userId
        };

        _context.CodaFiles.Add(codaFile);
        await _context.SaveChangesAsync();

        foreach (var transactionDto in codaData.Transactions)
        {
            var transaction = new BankTransaction
            {
                OrganisationId = organisationId,
                CodaFileId = codaFile.Id,
                TransactionDate = transactionDto.TransactionDate,
                ValueDate = transactionDto.ValueDate,
                Amount = transactionDto.Amount,
                StructuredCommunication = transactionDto.StructuredCommunication,
                FreeCommunication = transactionDto.FreeCommunication,
                CounterpartyName = transactionDto.CounterpartyName,
                CounterpartyAccount = transactionDto.CounterpartyAccount,
                TransactionCode = transactionDto.TransactionCode,
                IsReconciled = false
            };

            _context.BankTransactions.Add(transaction);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Imported CODA file {FileName} with {TransactionCount} transactions for organisation {OrganisationId}",
            codaData.FileName, codaData.Transactions.Count, organisationId);

        return codaFile.Id;
    }
}
