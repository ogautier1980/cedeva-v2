using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.Financial.ViewModels;

public class AddTransactionViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public List<OutstandingBookingViewModel> OutstandingBookings { get; set; } = new();
    public ExpenseViewModel Expense { get; set; } = new();
}

public class OutstandingBookingViewModel
{
    public int Id { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
}
