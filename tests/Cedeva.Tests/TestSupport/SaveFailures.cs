using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.TestSupport;

/// <summary>
/// Shared data for fault-injection tests that drive controllers' defensive catch branches via
/// <see cref="ThrowingSaveChangesInterceptor"/>. <see cref="Kinds"/> feeds an xUnit [MemberData] so
/// every endpoint is tested once per exception type the controllers catch (InvalidOperationException,
/// DbUpdateException, and a generic Exception).
/// </summary>
public static class SaveFailures
{
    public static IEnumerable<object[]> Kinds() => new[]
    {
        new object[] { "invalid-op" },
        new object[] { "db-update" },
        new object[] { "generic" },
    };

    public static Exception Make(string kind) => kind switch
    {
        "invalid-op" => new InvalidOperationException("injected failure"),
        "db-update" => new DbUpdateException("injected failure"),
        _ => new Exception("injected failure"),
    };
}
