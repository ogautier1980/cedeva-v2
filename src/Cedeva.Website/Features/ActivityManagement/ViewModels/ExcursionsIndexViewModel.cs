using Cedeva.Core.Entities;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class ExcursionsIndexViewModel
{
    public Activity Activity { get; set; } = null!;
    public List<ExcursionListItem> Excursions { get; set; } = new();
}

public class ExcursionListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime ExcursionDate { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public bool IsActive { get; set; }
    public List<string> TargetGroupNames { get; set; } = new();
    public int RegistrationCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetBalance { get; set; }
}
