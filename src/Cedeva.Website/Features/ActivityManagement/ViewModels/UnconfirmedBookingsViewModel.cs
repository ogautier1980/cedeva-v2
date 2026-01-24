using Cedeva.Core.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class UnconfirmedBookingsViewModel
{
    public Activity Activity { get; set; } = null!;
    public IEnumerable<Booking> UnconfirmedBookings { get; set; } = new List<Booking>();
    public List<SelectListItem> GroupOptions { get; set; } = new();
}
