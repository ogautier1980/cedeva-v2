using Cedeva.Core.Entities;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class ExcursionRegistrationsViewModel
{
    public Excursion Excursion { get; set; } = null!;
    public Activity Activity { get; set; } = null!;
    public Dictionary<ActivityGroup, List<ExcursionChildInfo>> ChildrenByGroup { get; set; } = new();
}

public class ExcursionChildInfo
{
    public int BookingId { get; set; }
    public int ChildId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public bool IsRegistered { get; set; }
    public int? RegistrationId { get; set; }
    public decimal ExcursionCost { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}
