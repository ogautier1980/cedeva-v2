namespace Cedeva.Website.Features.Financial.ViewModels;

public class AddTransactionViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public ExpenseViewModel Expense { get; set; } = new();
}
