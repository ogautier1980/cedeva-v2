using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.Payments.ViewModels;

public class PaymentViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public int BookingId { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.Amount")]
    [Range(0.01, 9999999, ErrorMessage = "Validation.AmountRange")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.PaymentDate")]
    public DateTime PaymentDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.PaymentMethod")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [Display(Name = "Field.Reference")]
    [StringLength(200, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string? Reference { get; set; }

    // Display only
    public string? ChildName { get; set; }
    public string? ParentName { get; set; }
    public string? ActivityName { get; set; }
    public decimal BookingTotalAmount { get; set; }
    public decimal BookingPaidAmount { get; set; }
    public decimal BookingRemainingAmount => BookingTotalAmount - BookingPaidAmount;

    // Audit fields
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    // Audit display names (for UI)
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public string? ModifiedByDisplayName { get; set; }
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
