namespace Cedeva.Core.Entities;

/// <summary>
/// Fichier CODA importé (relevé bancaire belge).
/// </summary>
public class CodaFile : AuditableEntity
{
    public int Id { get; set; }

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public DateTime ImportDate { get; set; }

    public DateTime StatementDate { get; set; }

    public string AccountNumber { get; set; } = null!;

    public decimal OldBalance { get; set; }

    public decimal NewBalance { get; set; }

    public int TransactionCount { get; set; }

    public int ImportedByUserId { get; set; }

    public ICollection<BankTransaction> Transactions { get; set; } = new List<BankTransaction>();
}
