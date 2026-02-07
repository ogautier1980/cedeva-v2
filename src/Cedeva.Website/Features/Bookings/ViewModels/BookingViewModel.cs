using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;
using Cedeva.Website.ViewModels;

namespace Cedeva.Website.Features.Bookings.ViewModels;

public class BookingViewModel : AuditableViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.Date)]
    [Display(Name = "Field.BookingDate")]
    public DateTime BookingDate { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.Child")]
    public int ChildId { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.Activity")]
    public int ActivityId { get; set; }

    [Display(Name = "Field.Group")]
    public int? GroupId { get; set; }

    [Display(Name = "Field.IsConfirmed")]
    public bool IsConfirmed { get; set; }

    [Display(Name = "Field.IsMedicalSheet")]
    public bool IsMedicalSheet { get; set; }

    // Financial information
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public Core.Enums.PaymentStatus PaymentStatus { get; set; }

    // Navigation properties for display
    public string? ChildFullName { get; set; }
    public string? ParentFullName { get; set; }
    public string? ActivityName { get; set; }
    public DateTime? ActivityStartDate { get; set; }
    public DateTime? ActivityEndDate { get; set; }
    public string? GroupLabel { get; set; }

    // Summary counts
    public int DaysCount { get; set; }
    public int QuestionAnswersCount { get; set; }

    // Day selection
    public List<BookingDaySelectionViewModel> AvailableDays { get; set; } = new();
    public List<int> SelectedActivityDayIds { get; set; } = new();

    // Days grouped by week (for Details view)
    public List<WeeklyBookingDaysViewModel> WeeklyDays { get; set; } = new();

    // Questions and answers (for Details/Edit views)
    public List<BookingQuestionViewModel> Questions { get; set; } = new();
}

public class BookingQuestionViewModel
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; }
    public bool IsRequired { get; set; }
    public string? Options { get; set; }
    public int DisplayOrder { get; set; }
    public string? AnswerText { get; set; }
}
