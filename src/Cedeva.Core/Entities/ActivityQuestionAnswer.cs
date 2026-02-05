using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class ActivityQuestionAnswer
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    [Required(ErrorMessage = "The {0} field is required.")]
    public int ActivityQuestionId { get; set; }
    public ActivityQuestion ActivityQuestion { get; set; } = null!;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(1000, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string AnswerText { get; set; } = string.Empty;
}
