using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

public class EmailSent
{
    public int Id { get; set; }

    public int? ActivityId { get; set; }
    public Activity? Activity { get; set; }

    [Required]
    public EmailRecipient RecipientType { get; set; }

    public int? RecipientGroupId { get; set; }

    [Required]
    public string RecipientEmails { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [StringLength(1024)]
    public string Message { get; set; } = string.Empty;

    [StringLength(255)]
    public string? AttachmentFileName { get; set; }

    [StringLength(500)]
    public string? AttachmentFilePath { get; set; }

    [Required]
    public DateTime SentDate { get; set; }
}
