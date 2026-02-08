using Cedeva.Core.Interfaces;
using Cedeva.Website.Localization;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Infrastructure;

public interface ICedevaControllerContext<T> where T : class
{
    ICurrentUserService CurrentUser { get; }
    IUserDisplayService UserDisplay { get; }
    IStringLocalizer<SharedResources> Localizer { get; }
    ISessionStateService Session { get; }
    ILogger<T> Logger { get; }
}

public class CedevaControllerContext<T> : ICedevaControllerContext<T> where T : class
{
    public ICurrentUserService CurrentUser { get; }
    public IUserDisplayService UserDisplay { get; }
    public IStringLocalizer<SharedResources> Localizer { get; }
    public ISessionStateService Session { get; }
    public ILogger<T> Logger { get; }

    public CedevaControllerContext(
        ICurrentUserService currentUser,
        IUserDisplayService userDisplay,
        IStringLocalizer<SharedResources> localizer,
        ISessionStateService session,
        ILogger<T> logger)
    {
        CurrentUser = currentUser;
        UserDisplay = userDisplay;
        Localizer = localizer;
        Session = session;
        Logger = logger;
    }
}
