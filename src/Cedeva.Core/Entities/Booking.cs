using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class Booking
{
    public int Id { get; set; }

    [DataType(DataType.Date)]
    public DateTime BookingDate { get; set; }

    [Required]
    public int ChildId { get; set; }
    public Child Child { get; set; } = null!;

    [Required]
    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public int? GroupId { get; set; }
    public ActivityGroup? Group { get; set; }

    [Required]
    public bool IsConfirmed { get; set; }

    [Required]
    public bool IsMedicalSheet { get; set; }

    public ICollection<BookingDay> Days { get; set; } = new List<BookingDay>();
    public ICollection<ActivityQuestionAnswer> QuestionAnswers { get; set; } = new List<ActivityQuestionAnswer>();
}
