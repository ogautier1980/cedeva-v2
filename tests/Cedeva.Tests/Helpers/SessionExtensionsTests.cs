using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cedeva.Website.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NSubstitute;

namespace Cedeva.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="SessionExtensions"/> (SetObject/GetObject) and the
/// <see cref="SessionState"/> wrapper in <c>Cedeva.Website.Infrastructure</c>.
/// </summary>
public class SessionExtensionsTests
{
    /// <summary>
    /// Minimal in-memory <see cref="ISession"/> backed by a dictionary, mirroring the
    /// byte store that GetString/SetString extension methods wrap.
    /// </summary>
    private sealed class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public bool IsAvailable => true;
        public string Id => "fake-session";
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }

    private sealed class FakeSessionFeature : ISessionFeature
    {
        public FakeSessionFeature(ISession session) => Session = session;
        public ISession Session { get; set; }
    }

    private sealed record Person(int Id, string Name);

    private static DefaultHttpContext BuildContext(out FakeSession session)
    {
        var context = new DefaultHttpContext();
        session = new FakeSession();
        context.Features.Set<ISessionFeature>(new FakeSessionFeature(session));
        return context;
    }

    private static (IHttpContextAccessor Accessor, FakeSession Session) BuildAccessor()
    {
        var context = BuildContext(out var session);
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        return (accessor, session);
    }

    // ---------------------------------------------------------------------
    // SessionExtensions.SetObject / GetObject
    // ---------------------------------------------------------------------

    [Fact]
    public void SetGetObject_ComplexType_RoundTrips()
    {
        BuildContext(out var session);
        var value = new Person(7, "Alice");

        session.SetObject("person", value);

        session.GetObject<Person>("person").Should().Be(value);
    }

    [Fact]
    public void SetObject_SerializesAsJsonString()
    {
        BuildContext(out var session);

        session.SetObject("person", new Person(1, "Bob"));

        session.GetString("person").Should().Be("{\"Id\":1,\"Name\":\"Bob\"}");
    }

    [Fact]
    public void GetObject_MissingKey_ReturnsDefault()
    {
        BuildContext(out var session);

        session.GetObject<Person>("missing").Should().BeNull();
        session.GetObject<int>("missing").Should().Be(0);
    }

    [Fact]
    public void SetObject_Overwrite_ReturnsLatestValue()
    {
        BuildContext(out var session);
        session.SetObject("person", new Person(1, "First"));

        session.SetObject("person", new Person(2, "Second"));

        session.GetObject<Person>("person").Should().Be(new Person(2, "Second"));
    }

    [Fact]
    public void SetGetObject_Primitive_RoundTrips()
    {
        BuildContext(out var session);

        session.SetObject("count", 42);

        session.GetObject<int>("count").Should().Be(42);
    }

    [Fact]
    public void SetGetObject_Collection_RoundTrips()
    {
        BuildContext(out var session);
        var list = new List<int> { 1, 2, 3 };

        session.SetObject("ids", list);

        session.GetObject<List<int>>("ids").Should().BeEquivalentTo(list);
    }

    [Fact]
    public void SetObject_NullValue_GetReturnsDefault()
    {
        BuildContext(out var session);

        session.SetObject<Person?>("person", null);

        // JSON "null" deserializes back to default(Person) => null
        session.GetObject<Person>("person").Should().BeNull();
    }

    // ---------------------------------------------------------------------
    // SessionState constructor
    // ---------------------------------------------------------------------

    [Fact]
    public void Constructor_NullAccessor_Throws()
    {
        var act = () => new SessionState(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ---------------------------------------------------------------------
    // SessionState — null HttpContext (safe no-op / defaults)
    // ---------------------------------------------------------------------

    [Fact]
    public void NoHttpContext_GettersReturnNull_SettersDoNotThrow()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var sut = new SessionState(accessor);

        sut.SelectedActivityId.Should().BeNull();
        sut.SelectedOrganisationId.Should().BeNull();

        var act = () =>
        {
            sut.SelectedActivityId = 5;
            sut.SelectedOrganisationId = 9;
            sut.SelectedActivityId = null;
            sut.SelectedOrganisationId = null;
            sut.Clear();
        };

        act.Should().NotThrow();
    }

    // ---------------------------------------------------------------------
    // SelectedActivityId
    // ---------------------------------------------------------------------

    [Fact]
    public void SelectedActivityId_Unset_ReturnsNull()
    {
        var (accessor, _) = BuildAccessor();
        var sut = new SessionState(accessor);

        sut.SelectedActivityId.Should().BeNull();
    }

    [Fact]
    public void SelectedActivityId_SetGet_RoundTrips()
    {
        var (accessor, session) = BuildAccessor();
        var sut = new SessionState(accessor);

        sut.SelectedActivityId = 123;

        sut.SelectedActivityId.Should().Be(123);
        session.GetString("Activity_Id").Should().Be("123");
    }

    [Fact]
    public void SelectedActivityId_SetNull_RemovesKey()
    {
        var (accessor, session) = BuildAccessor();
        var sut = new SessionState(accessor);
        sut.SelectedActivityId = 50;

        sut.SelectedActivityId = null;

        sut.SelectedActivityId.Should().BeNull();
        session.GetString("Activity_Id").Should().BeNull();
    }

    [Fact]
    public void SelectedActivityId_Overwrite_ReturnsLatest()
    {
        var (accessor, _) = BuildAccessor();
        var sut = new SessionState(accessor);
        sut.SelectedActivityId = 1;

        sut.SelectedActivityId = 2;

        sut.SelectedActivityId.Should().Be(2);
    }

    [Fact]
    public void SelectedActivityId_NonNumericStored_ReturnsNull()
    {
        var (accessor, session) = BuildAccessor();
        session.SetString("Activity_Id", "not-a-number");
        var sut = new SessionState(accessor);

        sut.SelectedActivityId.Should().BeNull();
    }

    // ---------------------------------------------------------------------
    // SelectedOrganisationId
    // ---------------------------------------------------------------------

    [Fact]
    public void SelectedOrganisationId_Unset_ReturnsNull()
    {
        var (accessor, _) = BuildAccessor();
        var sut = new SessionState(accessor);

        sut.SelectedOrganisationId.Should().BeNull();
    }

    [Fact]
    public void SelectedOrganisationId_SetGet_RoundTrips()
    {
        var (accessor, session) = BuildAccessor();
        var sut = new SessionState(accessor);

        sut.SelectedOrganisationId = 456;

        sut.SelectedOrganisationId.Should().Be(456);
        session.GetString("Organisation_Id").Should().Be("456");
    }

    [Fact]
    public void SelectedOrganisationId_SetNull_RemovesKey()
    {
        var (accessor, session) = BuildAccessor();
        var sut = new SessionState(accessor);
        sut.SelectedOrganisationId = 77;

        sut.SelectedOrganisationId = null;

        sut.SelectedOrganisationId.Should().BeNull();
        session.GetString("Organisation_Id").Should().BeNull();
    }

    [Fact]
    public void SelectedOrganisationId_NonNumericStored_ReturnsNull()
    {
        var (accessor, session) = BuildAccessor();
        session.SetString("Organisation_Id", "xyz");
        var sut = new SessionState(accessor);

        sut.SelectedOrganisationId.Should().BeNull();
    }

    // ---------------------------------------------------------------------
    // Clear
    // ---------------------------------------------------------------------

    [Fact]
    public void Clear_RemovesAllSessionValues()
    {
        var (accessor, session) = BuildAccessor();
        var sut = new SessionState(accessor);
        sut.SelectedActivityId = 1;
        sut.SelectedOrganisationId = 2;

        sut.Clear();

        sut.SelectedActivityId.Should().BeNull();
        sut.SelectedOrganisationId.Should().BeNull();
        session.Keys.Should().BeEmpty();
    }
}
