namespace Cedeva.Website.Features.Financial.ViewModels;

public class TransactionsListViewModel
{
    public string ActivityName { get; set; } = string.Empty;
    public int ActivityId { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetBalance { get; set; }
    public decimal TotalOffBalance { get; set; }
    public List<TransactionViewModel> Transactions { get; set; } = new();
}

public class TransactionViewModel
{
    public DateTime Date { get; set; }
    public int TicketNumber { get; set; }
    public string Type { get; set; } = string.Empty; // "Payment" or "Expense"
    public string Label { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? AssignedTo { get; set; } // Pour les dépenses
    public decimal Amount { get; set; }
    public bool IsIncome { get; set; }
    /// <summary>Internal transfer (e.g. cash-to-bank) — excluded from the Entrées/Sorties totals.</summary>
    public bool IsOffBalance { get; set; }
    public string? PaymentMethod { get; set; } // Pour les paiements
    public string? ChildName { get; set; } // Pour les paiements
    public int? RelatedId { get; set; } // PaymentId ou ExpenseId
    public string? ExcursionName { get; set; } // Pour les dépenses liées à une excursion
}
