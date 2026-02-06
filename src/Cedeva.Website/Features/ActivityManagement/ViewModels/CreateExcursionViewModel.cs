using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class CreateExcursionViewModel
{
    public int ActivityId { get; set; }
    public Activity? Activity { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.ExcursionName")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.Description")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.ExcursionDate")]
    public DateTime ExcursionDate { get; set; } = DateTime.Today;

    [Display(Name = "Field.StartTime")]
    [RegularExpression(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "Format invalide (HH:mm)")]
    public string? StartTime { get; set; }

    [Display(Name = "Field.EndTime")]
    [RegularExpression(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "Format invalide (HH:mm)")]
    public string? EndTime { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.ExcursionCost")]
    [Range(0, 9999.99, ErrorMessage = "Validation.Range")]
    public decimal Cost { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.ExcursionType")]
    public ExcursionType Type { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.TargetGroups")]
    public List<int> SelectedGroupIds { get; set; } = new();

    public List<ActivityGroup> AvailableGroups { get; set; } = new();
}
