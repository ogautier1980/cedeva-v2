using Cedeva.Core.Entities;
using Cedeva.Infrastructure.Services;
using Cedeva.Tests.TestSupport;

namespace Cedeva.Tests.Services;

public class UserDisplayServiceTests
{
    private static CedevaUser User(string id, string firstName, string lastName) => new()
    {
        Id = id,
        UserName = $"{id}@test.be",
        Email = $"{id}@test.be",
        FirstName = firstName,
        LastName = lastName
    };

    [Fact]
    public void Constructor_NullContext_Throws()
    {
        var act = () => new UserDisplayService(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ----- GetUserDisplayNameAsync -----

    [Fact]
    public async Task GetUserDisplayName_SystemIdentifier_ReturnsSystem()
    {
        using var db = new SqliteTestContext();
        var sut = new UserDisplayService(db.Context);

        var result = await sut.GetUserDisplayNameAsync("System");

        result.Should().Be("System");
    }

    [Fact]
    public async Task GetUserDisplayName_KnownUser_ReturnsFullName()
    {
        using var db = new SqliteTestContext();
        db.Context.Users.Add(User("u1", "Alice", "Martin"));
        db.Context.SaveChanges();
        var sut = new UserDisplayService(db.NewContext());

        var result = await sut.GetUserDisplayNameAsync("u1");

        result.Should().Be("Alice Martin");
    }

    [Fact]
    public async Task GetUserDisplayName_UserWithEmptyLastName_TrimsTrailingSpace()
    {
        using var db = new SqliteTestContext();
        db.Context.Users.Add(User("u1", "Alice", ""));
        db.Context.SaveChanges();
        var sut = new UserDisplayService(db.NewContext());

        var result = await sut.GetUserDisplayNameAsync("u1");

        result.Should().Be("Alice");
    }

    [Fact]
    public async Task GetUserDisplayName_UserWithEmptyFirstAndLast_ReturnsEmpty()
    {
        using var db = new SqliteTestContext();
        db.Context.Users.Add(User("u1", "", ""));
        db.Context.SaveChanges();
        var sut = new UserDisplayService(db.NewContext());

        var result = await sut.GetUserDisplayNameAsync("u1");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserDisplayName_UnknownUser_ReturnsUserId()
    {
        using var db = new SqliteTestContext();
        var sut = new UserDisplayService(db.Context);

        var result = await sut.GetUserDisplayNameAsync("does-not-exist");

        result.Should().Be("does-not-exist");
    }

    // ----- GetUserDisplayNamesAsync -----

    [Fact]
    public async Task GetUserDisplayNames_MixOfKnownUnknownAndSystem_MapsEachCorrectly()
    {
        using var db = new SqliteTestContext();
        db.Context.Users.Add(User("u1", "Alice", "Martin"));
        db.Context.Users.Add(User("u2", "Bob", "Durand"));
        db.Context.SaveChanges();
        var sut = new UserDisplayService(db.NewContext());

        var result = await sut.GetUserDisplayNamesAsync(new[] { "u1", "u2", "missing", "System" });

        result.Should().HaveCount(4);
        result["u1"].Should().Be("Alice Martin");
        result["u2"].Should().Be("Bob Durand");
        result["missing"].Should().Be("missing");
        result["System"].Should().Be("System");
    }

    [Fact]
    public async Task GetUserDisplayNames_OnlySystemIds_MapsAllToSystem()
    {
        using var db = new SqliteTestContext();
        var sut = new UserDisplayService(db.Context);

        var result = await sut.GetUserDisplayNamesAsync(new[] { "System" });

        result.Should().HaveCount(1);
        result["System"].Should().Be("System");
    }

    [Fact]
    public async Task GetUserDisplayNames_EmptyInput_ReturnsEmpty()
    {
        using var db = new SqliteTestContext();
        var sut = new UserDisplayService(db.Context);

        var result = await sut.GetUserDisplayNamesAsync(Array.Empty<string>());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserDisplayNames_DuplicateIds_DeduplicatesViaDistinctQuery()
    {
        using var db = new SqliteTestContext();
        db.Context.Users.Add(User("u1", "Alice", "Martin"));
        db.Context.SaveChanges();
        var sut = new UserDisplayService(db.NewContext());

        var result = await sut.GetUserDisplayNamesAsync(new[] { "u1", "u1", "missing", "missing" });

        result.Should().HaveCount(2);
        result["u1"].Should().Be("Alice Martin");
        result["missing"].Should().Be("missing");
    }

    [Fact]
    public async Task GetUserDisplayNames_AllUnknown_MapsEachToItsOwnId()
    {
        using var db = new SqliteTestContext();
        var sut = new UserDisplayService(db.Context);

        var result = await sut.GetUserDisplayNamesAsync(new[] { "x", "y" });

        result.Should().HaveCount(2);
        result["x"].Should().Be("x");
        result["y"].Should().Be("y");
    }
}
