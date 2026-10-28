namespace Cedeva.Website.Features.Financial.ViewModels;

public class ExpenseDetailViewModel
{
    public int Id { get; set; }
    public DateTime? Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Category { get; set; }
    public decimal Amount { get; set; }
}
