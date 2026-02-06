using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class GroupAssignmentViewModel
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public List<UnassignedChildViewModel> UnassignedChildren { get; set; } = new();
    public List<SelectListItem> GroupOptions { get; set; } = new();
}

public class UnassignedChildViewModel
{
    public int BookingId { get; set; }
    public int ChildId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public int Age => DateTime.Today.Year - BirthDate.Year - (DateTime.Today.DayOfYear < BirthDate.DayOfYear ? 1 : 0);
}
