using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cedeva.Website.Features.EmailTemplates.ViewModels;

public class EmailTemplateViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Field.Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "EmailTemplate.TemplateType")]
    public EmailTemplateType TemplateType { get; set; }

    [Required]
    [StringLength(500)]
    [Display(Name = "Field.Subject")]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [StringLength(10000)]
    [Display(Name = "EmailTemplate.HtmlContent")]
    public string HtmlContent { get; set; } = string.Empty;

    [Display(Name = "EmailTemplate.IsDefault")]
    public bool IsDefault { get; set; }

    [Display(Name = "EmailTemplate.IsShared")]
    public bool IsShared { get; set; }

    public List<SelectListItem> TemplateTypeOptions { get; set; } = new();
}
