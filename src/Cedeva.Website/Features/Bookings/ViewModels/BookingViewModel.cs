using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.Bookings.ViewModels;

public class BookingViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "La date de réservation est requise")]
    [DataType(DataType.Date)]
    [Display(Name = "Date de réservation")]
    public DateTime BookingDate { get; set; }

    [Required(ErrorMessage = "L'enfant est requis")]
    [Display(Name = "Enfant")]
    public int ChildId { get; set; }

    [Required(ErrorMessage = "L'activité est requise")]
    [Display(Name = "Activité")]
    public int ActivityId { get; set; }

    [Display(Name = "Groupe")]
    public int? GroupId { get; set; }

    [Display(Name = "Confirmée")]
    public bool IsConfirmed { get; set; }

    [Display(Name = "Fiche médicale reçue")]
    public bool IsMedicalSheet { get; set; }

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
}
