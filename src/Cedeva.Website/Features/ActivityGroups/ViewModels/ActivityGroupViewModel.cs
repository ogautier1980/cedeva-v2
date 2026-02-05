using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.ActivityGroups.ViewModels;

public class ActivityGroupViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.Label")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string Label { get; set; } = string.Empty;

    [Display(Name = "Field.Capacity")]
    public int? Capacity { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.Activity")]
    public int ActivityId { get; set; }

    public string? ActivityName { get; set; }
    public int BookingsCount { get; set; }
}
