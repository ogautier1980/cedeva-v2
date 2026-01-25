using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Entities;

namespace Cedeva.Website.Features.PublicRegistration.ViewModels;

public class SelectActivityViewModel
{
    [Required]
    public int? ActivityId { get; set; }

    public List<Activity> AvailableActivities { get; set; } = new();
}
