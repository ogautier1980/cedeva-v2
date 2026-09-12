using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

/// <summary>
/// Lot K #5 — garderie (before/after-school care), an optional day-by-day service: a child can be
/// registered for garderie on some of the activity's active days but not others. One row = one
/// child registered for garderie on one day, with the amount charged for that day. Included in
/// Booking.TotalAmount like an excursion registration.
/// </summary>
public class ChildcareRegistration : AuditableEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    [Required(ErrorMessage = "Validation.Required")]
    public int ActivityDayId { get; set; }
    public ActivityDay ActivityDay { get; set; } = null!;

    [Range(0, 9999999, ErrorMessage = "Validation.AmountRange")]
    public decimal Amount { get; set; }
}
