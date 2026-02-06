using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;
using Cedeva.Website.ViewModels;

namespace Cedeva.Website.Features.Payments.ViewModels;

public class PaymentViewModel : AuditableViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public int BookingId { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.Amount")]
    [Range(0.01, 9999999, ErrorMessage = "Validation.AmountRange")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.PaymentDate")]
    public DateTime PaymentDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.PaymentMethod")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [Display(Name = "Field.Reference")]
    [StringLength(200, ErrorMessage = "Validation.StringLength")]
    public string? Reference { get; set; }

    // Display only
    public string? ChildName { get; set; }
    public string? ParentName { get; set; }
    public string? ActivityName { get; set; }
    public decimal BookingTotalAmount { get; set; }
    public decimal BookingPaidAmount { get; set; }
    public decimal BookingRemainingAmount => BookingTotalAmount - BookingPaidAmount;
}

public class PaymentListViewModel
{
    public int Id { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus Status { get; set; }
    public string? Reference { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public string ActivityName { get; set; } = string.Empty;
    public int BookingId { get; set; }
}
