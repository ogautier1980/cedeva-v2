using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class SendEmailViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Field.Recipient")]
    public string SelectedRecipient { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    [Display(Name = "Field.Subject")]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [StringLength(1024)]
    [Display(Name = "Field.Message")]
    public string Message { get; set; } = string.Empty;

    [Display(Name = "Field.Attachment")]
    public IFormFile? AttachmentFile { get; set; }

    public List<SelectListItem> RecipientOptions { get; set; } = new();
}
