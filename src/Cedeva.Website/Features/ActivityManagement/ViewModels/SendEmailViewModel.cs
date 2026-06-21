using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class SendEmailViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.Recipient")]
    public string? SelectedRecipient { get; set; }

    [Display(Name = "Email.ScheduledForDay")]
    public int? SelectedDayId { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(255, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Subject")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(5000, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Message")]
    public string Message { get; set; } = string.Empty;

    [Display(Name = "Email.SendSeparateEmailPerChild")]
    public bool SendSeparateEmailPerChild { get; set; } = true;

    [Display(Name = "Field.Attachment")]
    public IFormFile? AttachmentFile { get; set; }

    public List<SelectListItem> RecipientOptions { get; set; } = new();
    public List<SelectListItem> DayOptions { get; set; } = new();

    /// <summary>
    /// Emails picked when "Custom contacts" is selected as recipient — an ad-hoc group built from
    /// the organisation's contacts (parents, team members and other contacts).
    /// </summary>
    public List<string> SelectedContactEmails { get; set; } = new();

    /// <summary>All selectable contacts (with an email) for the custom-group picker.</summary>
    public List<ContactSelectItem> ContactOptions { get; set; } = new();
}

/// <summary>A selectable contact in the custom email-group picker.</summary>
public class ContactSelectItem
{
    public string Email { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
