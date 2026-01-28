using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.ActivityGroups.ViewModels;

public class ActivityGroupViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Field.Label")]
    [StringLength(100)]
    public string Label { get; set; } = string.Empty;

    [Display(Name = "Field.Capacity")]
    public int? Capacity { get; set; }

    [Required]
    [Display(Name = "Field.Activity")]
    public int ActivityId { get; set; }

    public string? ActivityName { get; set; }
    public int BookingsCount { get; set; }
}
