using Cedeva.Core.Entities;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class SentEmailsViewModel
{
    public Activity Activity { get; set; } = null!;
    public List<EmailSent> SentEmails { get; set; } = new();
}
