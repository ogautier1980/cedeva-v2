using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class ActivityQuestionAnswer : AuditableEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    [Required(ErrorMessage = "Validation.Required")]
    public int ActivityQuestionId { get; set; }
    public ActivityQuestion ActivityQuestion { get; set; } = null!;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(1000, ErrorMessage = "Validation.StringLength")]
    public string AnswerText { get; set; } = string.Empty;
}
