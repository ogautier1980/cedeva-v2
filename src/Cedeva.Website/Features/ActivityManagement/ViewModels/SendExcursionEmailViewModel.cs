using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class SendExcursionEmailViewModel
{
    public int ExcursionId { get; set; }
    public Excursion? Excursion { get; set; }
    public Activity? Activity { get; set; }

    [Required]
    [Display(Name = "Field.Recipient")]
    public string SelectedRecipient { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    [Display(Name = "Field.Subject")]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [StringLength(5000)]
    [Display(Name = "Field.Message")]
    public string Message { get; set; } = string.Empty;

    [Display(Name = "Email.SendSeparateEmailPerChild")]
    public bool SendSeparateEmailPerChild { get; set; } = true;

    [Display(Name = "Field.Attachment")]
    public IFormFile? AttachmentFile { get; set; }

    public List<SelectListItem> RecipientOptions { get; set; } = new();
}
