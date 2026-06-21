using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cedeva.Tests.TestSupport;

/// <summary>
/// EF Core interceptor that throws a configured exception whenever the context tries to persist
/// changes. Used to drive controllers' defensive <c>catch (DbUpdateException)</c> / <c>catch
/// (Exception)</c> branches: seed first (provider returns <c>null</c> so the save succeeds), then set
/// the exception and issue the POST so the controller's <c>SaveChangesAsync</c> throws.
/// </summary>
public sealed class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly Func<Exception?> _exceptionProvider;

    public ThrowingSaveChangesInterceptor(Func<Exception?> exceptionProvider) =>
        _exceptionProvider = exceptionProvider;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        var ex = _exceptionProvider();
        if (ex != null) throw ex;
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var ex = _exceptionProvider();
        if (ex != null) throw ex;
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
