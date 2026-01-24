using Cedeva.Core.Entities;

namespace Cedeva.Website.Features.PublicRegistration.ViewModels;

public class ConfirmationViewModel
{
    public Booking Booking { get; set; } = null!;
    public Activity Activity { get; set; } = null!;
    public Parent Parent { get; set; } = null!;
    public Child Child { get; set; } = null!;
}
