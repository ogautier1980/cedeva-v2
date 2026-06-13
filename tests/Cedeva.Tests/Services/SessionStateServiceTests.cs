using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cedeva.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NSubstitute;

namespace Cedeva.Tests.Services;

public class SessionStateServiceTests
{
    private const string SessionKeyPrefix = "State_";
    private const string CookieKeyPrefix = "State_";

    /// <summary>
    /// Minimal in-memory <see cref="ISession"/> implementation. Mirrors the byte-oriented
    /// store that <c>GetString</c>/<c>SetString</c> extension methods wrap.
    /// </summary>
    private sealed class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public bool IsAvailable => true;
        public string Id => "fake-session";
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();
        public System.Threading.Tasks.Task CommitAsync(System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task LoadAsync(System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }

    /// <summary>
    /// Builds an accessor over a real <see cref="DefaultHttpContext"/> with an in-memory session
    /// and (optionally) seeded request cookies. Returns the context so tests can inspect
    /// response cookies and session state directly.
    /// </summary>
    private static (IHttpContextAccessor Accessor, DefaultHttpContext Context, FakeSession Session) BuildAccessor(
        IDictionary<string, string>? requestCookies = null)
    {
        var context = new DefaultHttpContext();
        var session = new FakeSession();
        context.Features.Set<ISessionFeature>(new FakeSessionFeature(session));

        if (requestCookies is { Count: > 0 })
        {
            var header = string.Join("; ", requestCookies.Select(kv => $"{kv.Key}={kv.Value}"));
            context.Request.Headers["Cookie"] = header;
        }

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        return (accessor, context, session);
    }

    private sealed class FakeSessionFeature : ISessionFeature
    {
        public FakeSessionFeature(ISession session) => Session = session;
        public ISession Session { get; set; }
    }

    private static string? ResponseCookieValue(DefaultHttpContext context, string cookieKey)
    {
        // Set-Cookie header looks like "State_Key=value; expires=...; path=/; ..."
        var setCookie = context.Response.Headers["Set-Cookie"];
        foreach (var line in setCookie)
        {
            if (line is null)
                continue;
            var prefix = cookieKey + "=";
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                var rest = line.Substring(prefix.Length);
                var end = rest.IndexOf(';');
                return end >= 0 ? rest.Substring(0, end) : rest;
            }
        }
        return null;
    }

    // ---------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------

    [Fact]
    public void Constructor_NullAccessor_Throws()
    {
        var act = () => new SessionStateService(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ---------------------------------------------------------------------
    // No HttpContext (null) — every method must be a safe no-op
    // ---------------------------------------------------------------------

    [Fact]
    public void Get_NoHttpContext_ReturnsNull()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var sut = new SessionStateService(accessor);

        sut.Get("k").Should().BeNull();
        sut.Get<int>("k").Should().BeNull();
    }

    [Fact]
    public void SetAndClear_NoHttpContext_DoNotThrow()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var sut = new SessionStateService(accessor);

        var act = () =>
        {
            sut.Set("k", "v");
            sut.Set<int>("k", 5);
            sut.Clear("k");
            sut.ClearAllWithPrefix("State_");
        };

        act.Should().NotThrow();
    }

    // ---------------------------------------------------------------------
    // String set/get round-trips
    // ---------------------------------------------------------------------

    [Fact]
    public void Set_String_StoresInSessionAndCookie()
    {
        var (accessor, context, session) = BuildAccessor();
        var sut = new SessionStateService(accessor);

        sut.Set("ActivityId", "42");

        sut.Get("ActivityId").Should().Be("42");
        session.GetString(SessionKeyPrefix + "ActivityId").Should().Be("42");
        ResponseCookieValue(context, CookieKeyPrefix + "ActivityId").Should().Be("42");
    }

    [Fact]
    public void Set_String_PersistToCookieFalse_OnlyStoresInSession()
    {
        var (accessor, context, session) = BuildAccessor();
        var sut = new SessionStateService(accessor);

        sut.Set("Filter", "open", persistToCookie: false);

        session.GetString(SessionKeyPrefix + "Filter").Should().Be("open");
        ResponseCookieValue(context, CookieKeyPrefix + "Filter").Should().BeNull();
    }

    [Fact]
    public void Set_NullValue_ClearsKey()
    {
        var (accessor, context, session) = BuildAccessor();
        var sut = new SessionStateService(accessor);
        sut.Set("Key", "value");

        sut.Set("Key", (string?)null);

        session.GetString(SessionKeyPrefix + "Key").Should().BeNull();
        sut.Get("Key").Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Set_BlankValue_ClearsKey(string blank)
    {
        var (accessor, _, session) = BuildAccessor();
        var sut = new SessionStateService(accessor);
        sut.Set("Key", "value", persistToCookie: false);

        sut.Set("Key", blank, persistToCookie: false);

        session.GetString(SessionKeyPrefix + "Key").Should().BeNull();
    }

    [Fact]
    public void Get_MissingKey_ReturnsNull()
    {
        var (accessor, _, _) = BuildAccessor();
        var sut = new SessionStateService(accessor);

        sut.Get("DoesNotExist").Should().BeNull();
    }

    // ---------------------------------------------------------------------
    // Cookie fallback behaviour
    // ---------------------------------------------------------------------

    [Fact]
    public void Get_FallsBackToCookie_WhenSessionEmpty()
    {
        var (accessor, _, session) = BuildAccessor(
            new Dictionary<string, string> { [CookieKeyPrefix + "ActivityId"] = "7" });
        var sut = new SessionStateService(accessor);

        sut.Get("ActivityId").Should().Be("7");
        // cookie value gets restored into session for faster subsequent reads
        session.GetString(SessionKeyPrefix + "ActivityId").Should().Be("7");
    }

    [Fact]
    public void Get_PrefersSessionOverCookie()
    {
        var (accessor, _, session) = BuildAccessor(
            new Dictionary<string, string> { [CookieKeyPrefix + "Key"] = "fromCookie" });
        session.SetString(SessionKeyPrefix + "Key", "fromSession");
        var sut = new SessionStateService(accessor);

        sut.Get("Key").Should().Be("fromSession");
    }

    // ---------------------------------------------------------------------
    // Clear
    // ---------------------------------------------------------------------

    [Fact]
    public void Clear_RemovesSessionAndDeletesCookie()
    {
        var (accessor, context, session) = BuildAccessor();
        var sut = new SessionStateService(accessor);
        sut.Set("Key", "value");

        sut.Clear("Key");

        session.GetString(SessionKeyPrefix + "Key").Should().BeNull();
        // Delete appends an expired Set-Cookie header for the key
        var setCookie = context.Response.Headers["Set-Cookie"];
        setCookie.Any(c => c != null && c.StartsWith(CookieKeyPrefix + "Key=", StringComparison.Ordinal))
            .Should().BeTrue();
    }

    [Fact]
    public void ClearAllWithPrefix_IsNoOp()
    {
        var (accessor, _, session) = BuildAccessor();
        var sut = new SessionStateService(accessor);
        sut.Set("Key", "value", persistToCookie: false);

        sut.ClearAllWithPrefix("State_");

        // Documented as a no-op (session has no key enumeration) — value remains.
        session.GetString(SessionKeyPrefix + "Key").Should().Be("value");
    }

    // ---------------------------------------------------------------------
    // Typed set/get round-trips
    // ---------------------------------------------------------------------

    [Fact]
    public void SetGet_Int_RoundTrips()
    {
        var (accessor, _, _) = BuildAccessor();
        var sut = new SessionStateService(accessor);

        sut.Set<int>("n", 123);

        sut.Get<int>("n").Should().Be(123);
    }

    [Fact]
    public void SetGet_Bool_RoundTrips()
    {
        var (accessor, _, _) = BuildAccessor();
        var sut = new SessionStateService(accessor);

        sut.Set<bool>("b", true);

        sut.Get<bool>("b").Should().Be(true);
    }

    [Fact]
    public void SetGet_DateTime_RoundTripsViaInvariantRoundtripFormat()
    {
        var (accessor, _, session) = BuildAccessor();
        var sut = new SessionStateService(accessor);
        var value = new DateTime(2026, 7, 3, 14, 30, 15, DateTimeKind.Unspecified);

        sut.Set<DateTime>("d", value);

        // stored using the round-trip ("o") format
        session.GetString(SessionKeyPrefix + "d").Should().Be(value.ToString("o", CultureInfo.InvariantCulture));
        sut.Get<DateTime>("d").Should().Be(value);
    }

    [Fact]
    public void SetGet_Decimal_RoundTripsViaInvariantCulture()
    {
        var (accessor, _, session) = BuildAccessor();
        var sut = new SessionStateService(accessor);

        sut.Set<decimal>("m", 12.34m);

        // invariant culture => dot decimal separator
        session.GetString(SessionKeyPrefix + "m").Should().Be("12.34");
        sut.Get<decimal>("m").Should().Be(12.34m);
    }

    [Fact]
    public void SetTyped_NullValue_ClearsKey()
    {
        var (accessor, _, session) = BuildAccessor();
        var sut = new SessionStateService(accessor);
        sut.Set<int>("n", 5);

        sut.Set<int>("n", (int?)null);

        session.GetString(SessionKeyPrefix + "n").Should().BeNull();
        sut.Get<int>("n").Should().BeNull();
    }

    [Fact]
    public void GetTyped_MissingKey_ReturnsNull()
    {
        var (accessor, _, _) = BuildAccessor();
        var sut = new SessionStateService(accessor);

        sut.Get<int>("missing").Should().BeNull();
    }

    [Fact]
    public void GetTyped_UnparseableValue_ReturnsNull()
    {
        var (accessor, _, _) = BuildAccessor();
        var sut = new SessionStateService(accessor);
        sut.Set("n", "not-a-number", persistToCookie: false);

        sut.Get<int>("n").Should().BeNull();
    }

    [Fact]
    public void GetTyped_UnsupportedType_ReturnsNull()
    {
        var (accessor, _, _) = BuildAccessor();
        var sut = new SessionStateService(accessor);
        sut.Set("g", Guid.NewGuid().ToString(), persistToCookie: false);

        // Guid is not one of the handled types (int/bool/DateTime/decimal) => null
        sut.Get<Guid>("g").Should().BeNull();
    }

    [Fact]
    public void Set_PersistToCookieFalse_TypedOverload_OnlySession()
    {
        var (accessor, context, session) = BuildAccessor();
        var sut = new SessionStateService(accessor);

        sut.Set<int>("n", 9, persistToCookie: false);

        session.GetString(SessionKeyPrefix + "n").Should().Be("9");
        ResponseCookieValue(context, CookieKeyPrefix + "n").Should().BeNull();
    }
}
