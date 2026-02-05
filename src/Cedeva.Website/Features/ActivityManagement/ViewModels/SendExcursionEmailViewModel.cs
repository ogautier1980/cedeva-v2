using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class SendExcursionEmailViewModel
{
    public int ExcursionId { get; set; }
    public Excursion? Excursion { get; set; }
    public Activity? Activity { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.Recipient")]
    public string SelectedRecipient { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(255, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.Subject")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(5000, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.Message")]
    public string Message { get; set; } = string.Empty;

    [Display(Name = "Email.SendSeparateEmailPerChild")]
    public bool SendSeparateEmailPerChild { get; set; } = true;

    [Display(Name = "Field.Attachment")]
    public IFormFile? AttachmentFile { get; set; }

    public List<SelectListItem> RecipientOptions { get; set; } = new();
}
