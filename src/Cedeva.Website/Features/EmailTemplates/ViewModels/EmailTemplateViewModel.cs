using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cedeva.Website.Features.EmailTemplates.ViewModels;

public class EmailTemplateViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(200, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "EmailTemplate.TemplateType")]
    public EmailTemplateType TemplateType { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(500, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.Subject")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(10000, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "EmailTemplate.HtmlContent")]
    public string HtmlContent { get; set; } = string.Empty;

    [Display(Name = "EmailTemplate.IsDefault")]
    public bool IsDefault { get; set; }

    [Display(Name = "EmailTemplate.IsShared")]
    public bool IsShared { get; set; }

    public List<SelectListItem> TemplateTypeOptions { get; set; } = new();
}
