using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

public class EmailSent
{
    public int Id { get; set; }

    public int? ActivityId { get; set; }
    public Activity? Activity { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public EmailRecipient RecipientType { get; set; }

    public int? RecipientGroupId { get; set; }

    /// <summary>
    /// Optional day filter - when set, email was sent only to parents with bookings on this specific day
    /// </summary>
    public int? ScheduledDayId { get; set; }
    public ActivityDay? ScheduledDay { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public string RecipientEmails { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(255, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Email template content (may contain variables like %prenom_enfant%)
    /// Increased from 1024 to 5000 to accommodate HTML content
    /// </summary>
    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(5000, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Whether a separate email was sent per child (true) or one email per parent (false)
    /// </summary>
    public bool SendSeparateEmailPerChild { get; set; } = true;

    [StringLength(255, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string? AttachmentFileName { get; set; }

    [StringLength(500, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string? AttachmentFilePath { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public DateTime SentDate { get; set; }
}
