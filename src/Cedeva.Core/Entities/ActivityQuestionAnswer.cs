using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class ActivityQuestionAnswer
{
    public int Id { get; set; }

    [Required]
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    [Required]
    public int ActivityQuestionId { get; set; }
    public ActivityQuestion ActivityQuestion { get; set; } = null!;

    [Required]
    [StringLength(1000)]
    public string AnswerText { get; set; } = string.Empty;
}
