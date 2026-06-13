using System.Security.Claims;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Cedeva.Tests.Services;

public class CurrentUserServiceTests
{
    private static CurrentUserService BuildSut(params Claim[] claims)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        accessor.HttpContext.Returns(new DefaultHttpContext { User = principal });
        return new CurrentUserService(accessor);
    }

    // --- UserId ---

    [Fact]
    public void UserId_ReturnsNameIdentifierClaim()
    {
        var sut = BuildSut(new Claim(ClaimTypes.NameIdentifier, "user-42"));

        sut.UserId.Should().Be("user-42");
    }

    [Fact]
    public void UserId_WhenClaimMissing_ReturnsNull()
    {
        var sut = BuildSut(new Claim("OrganisationId", "1"));

        sut.UserId.Should().BeNull();
    }

    // --- OrganisationId ---

    [Fact]
    public void OrganisationId_WithValidInteger_ParsesValue()
    {
        var sut = BuildSut(new Claim("OrganisationId", "7"));

        sut.OrganisationId.Should().Be(7);
    }

    [Fact]
    public void OrganisationId_WhenClaimMissing_ReturnsNull()
    {
        var sut = BuildSut(new Claim(ClaimTypes.NameIdentifier, "user-1"));

        sut.OrganisationId.Should().BeNull();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("99999999999999999999")] // overflows int
    public void OrganisationId_WithUnparsableValue_ReturnsNull(string value)
    {
        var sut = BuildSut(new Claim("OrganisationId", value));

        sut.OrganisationId.Should().BeNull();
    }

    [Fact]
    public void OrganisationId_WithNegativeInteger_ParsesValue()
    {
        // int.TryParse accepts negatives; the service does no range validation.
        var sut = BuildSut(new Claim("OrganisationId", "-3"));

        sut.OrganisationId.Should().Be(-3);
    }

    // --- Role ---

    [Theory]
    [InlineData("Admin", Role.Admin)]
    [InlineData("Coordinator", Role.Coordinator)]
    public void Role_WithValidName_ParsesEnum(string claimValue, Role expected)
    {
        var sut = BuildSut(new Claim("Role", claimValue));

        sut.Role.Should().Be(expected);
    }

    [Fact]
    public void Role_WithNumericValue_ParsesEnum()
    {
        // Enum.TryParse accepts the underlying numeric value as well.
        var sut = BuildSut(new Claim("Role", "1"));

        sut.Role.Should().Be(Role.Coordinator);
    }

    [Fact]
    public void Role_WhenClaimMissing_ReturnsNull()
    {
        var sut = BuildSut(new Claim(ClaimTypes.NameIdentifier, "user-1"));

        sut.Role.Should().BeNull();
    }

    [Theory]
    [InlineData("NotARole")]
    [InlineData("")]
    public void Role_WithUnknownValue_ReturnsNull(string value)
    {
        var sut = BuildSut(new Claim("Role", value));

        sut.Role.Should().BeNull();
    }

    // --- IsAdmin ---

    [Fact]
    public void IsAdmin_WhenRoleIsAdmin_ReturnsTrue()
    {
        var sut = BuildSut(new Claim("Role", "Admin"));

        sut.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public void IsAdmin_WhenRoleIsCoordinator_ReturnsFalse()
    {
        var sut = BuildSut(new Claim("Role", "Coordinator"));

        sut.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public void IsAdmin_WhenRoleClaimMissing_ReturnsFalse()
    {
        var sut = BuildSut(new Claim(ClaimTypes.NameIdentifier, "user-1"));

        sut.IsAdmin.Should().BeFalse();
    }

    // --- All claims together (happy path) ---

    [Fact]
    public void AllProperties_WithFullClaimSet_AreResolved()
    {
        var sut = BuildSut(
            new Claim(ClaimTypes.NameIdentifier, "user-99"),
            new Claim("OrganisationId", "12"),
            new Claim("Role", "Admin"));

        sut.UserId.Should().Be("user-99");
        sut.OrganisationId.Should().Be(12);
        sut.Role.Should().Be(Role.Admin);
        sut.IsAdmin.Should().BeTrue();
    }

    // --- No HttpContext at all ---

    [Fact]
    public void WhenNoHttpContext_AllPropertiesAreNullAndNotAdmin()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var sut = new CurrentUserService(accessor);

        sut.UserId.Should().BeNull();
        sut.OrganisationId.Should().BeNull();
        sut.Role.Should().BeNull();
        sut.IsAdmin.Should().BeFalse();
    }

    // --- HttpContext present but unauthenticated (no user claims) ---

    [Fact]
    public void WhenUnauthenticatedUser_AllPropertiesAreNullAndNotAdmin()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext()); // empty ClaimsPrincipal
        var sut = new CurrentUserService(accessor);

        sut.UserId.Should().BeNull();
        sut.OrganisationId.Should().BeNull();
        sut.Role.Should().BeNull();
        sut.IsAdmin.Should().BeFalse();
    }
}
