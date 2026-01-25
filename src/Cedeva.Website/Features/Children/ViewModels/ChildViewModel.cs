using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.Children.ViewModels;

public class ChildViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Field.FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Field.LastName")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [StringLength(15, MinimumLength = 11)]
    [RegularExpression(@"^(\d{2})[.\- ]?(0[1-9]|1[0-2])[.\- ]?(0[1-9]|[12]\d|3[01])[.\- ]?(\d{3})[.\- ]?(\d{2})$")]
    [Display(Name = "Field.NationalRegisterNumber")]
    public string NationalRegisterNumber { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Field.BirthDate")]
    public DateTime BirthDate { get; set; }

    [Display(Name = "Field.IsDisadvantagedEnvironment")]
    public bool IsDisadvantagedEnvironment { get; set; }

    [Display(Name = "Field.IsMildDisability")]
    public bool IsMildDisability { get; set; }

    [Display(Name = "Field.IsSevereDisability")]
    public bool IsSevereDisability { get; set; }

    [Required]
    [Display(Name = "Field.Parent")]
    public int ParentId { get; set; }

    [Display(Name = "Field.ActivityGroup")]
    public int? ActivityGroupId { get; set; }

    // Navigation properties for display
    public string? ParentFullName { get; set; }
    public string? ActivityGroupName { get; set; }
    public List<BookingSummaryViewModel> Bookings { get; set; } = new();
}

public class BookingSummaryViewModel
{
    public int Id { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsConfirmed { get; set; }
}
