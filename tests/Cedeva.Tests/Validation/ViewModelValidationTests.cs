using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;
using Cedeva.Website.Features.Children.ViewModels;
using Cedeva.Website.Features.Parents.ViewModels;
using Cedeva.Website.Features.TeamMembers.ViewModels;

namespace Cedeva.Tests.Validation;

/// <summary>
/// Data-annotation validation tests for the Parent / TeamMember / Child ViewModels.
/// These models are validated by ASP.NET model binding via <see cref="Validator.TryValidateObject"/>;
/// ErrorMessage values are localization KEYS, so assertions check MemberNames, not message text.
/// Valid national register numbers (modulo-97): parent 85.06.15-133.80, child 16.07.08-164.10.
/// </summary>
public class ViewModelValidationTests
{
    // --- helpers -----------------------------------------------------------

    private const string ValidAdultNrn = "85.06.15-133.80";
    private const string ValidChildNrn = "16.07.08-164.10";

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    private static bool IsValid(object model) => Validate(model).Count == 0;

    private static bool HasErrorFor(IEnumerable<ValidationResult> results, string memberName) =>
        results.Any(r => r.MemberNames.Contains(memberName));

    private static ParentViewModel ValidParent() => new()
    {
        FirstName = "Paul",
        LastName = "Parent",
        Email = "paul.parent@test.be",
        MobilePhoneNumber = "0470000000",
        NationalRegisterNumber = ValidAdultNrn,
        Street = "Rue de Test",
        City = "Bruxelles",
        PostalCode = "1000",
        Country = Country.Belgium
    };

    private static TeamMemberViewModel ValidTeamMember() => new()
    {
        FirstName = "Tina",
        LastName = "Team",
        Email = "tina.team@test.be",
        MobilePhoneNumber = "0471234567",
        NationalRegisterNumber = ValidAdultNrn,
        BirthDate = new DateTime(1985, 6, 15),
        Street = "Rue de Test",
        City = "Bruxelles",
        PostalCode = "1000",
        Country = Country.Belgium,
        TeamRole = TeamRole.Animator,
        License = License.License,
        Status = Status.Volunteer
    };

    private static ChildViewModel ValidChild() => new()
    {
        FirstName = "Chloé",
        LastName = "Enfant",
        NationalRegisterNumber = ValidChildNrn,
        BirthDate = new DateTime(2016, 7, 8),
        ParentId = 1
    };

    // --- happy paths -------------------------------------------------------

    [Fact]
    public void Parent_FullyValid_Passes()
    {
        IsValid(ValidParent()).Should().BeTrue();
    }

    [Fact]
    public void TeamMember_FullyValid_Passes()
    {
        IsValid(ValidTeamMember()).Should().BeTrue();
    }

    [Fact]
    public void Child_FullyValid_Passes()
    {
        IsValid(ValidChild()).Should().BeTrue();
    }

    [Fact]
    public void Parent_OptionalLandline_Null_Passes()
    {
        var model = ValidParent();
        model.PhoneNumber = null;
        IsValid(model).Should().BeTrue();
    }

    [Theory]
    [InlineData("0470000000")]
    [InlineData("0471234567")]
    [InlineData("0481234567")]
    [InlineData("0491234567")]
    [InlineData("+32470000000")]
    [InlineData("0032470000000")]
    [InlineData("0470/00.00.00")]
    public void Parent_ValidBelgianMobile_Passes(string mobile)
    {
        var model = ValidParent();
        model.MobilePhoneNumber = mobile;
        IsValid(model).Should().BeTrue();
    }

    [Theory]
    [InlineData("0470000000")]
    [InlineData("0471234567")]
    [InlineData("+32481234567")]
    public void TeamMember_ValidBelgianMobile_Passes(string mobile)
    {
        var model = ValidTeamMember();
        model.MobilePhoneNumber = mobile;
        IsValid(model).Should().BeTrue();
    }

    [Theory]
    [InlineData("85.06.15-133.80")]
    [InlineData("85061513380")]
    public void Parent_AcceptsFormattedAndUnformattedNrn(string nrn)
    {
        var model = ValidParent();
        model.NationalRegisterNumber = nrn;
        IsValid(model).Should().BeTrue();
    }

    [Fact]
    public void Parent_ValidLandline_Passes()
    {
        var model = ValidParent();
        model.PhoneNumber = "02/123.45.67";
        IsValid(model).Should().BeTrue();
    }

    // --- missing required fields -------------------------------------------

    [Fact]
    public void Parent_MissingRequiredFields_FailsWithMemberNames()
    {
        var model = new ParentViewModel(); // all required strings default to string.Empty
        var results = Validate(model);

        HasErrorFor(results, nameof(ParentViewModel.FirstName)).Should().BeTrue();
        HasErrorFor(results, nameof(ParentViewModel.LastName)).Should().BeTrue();
        HasErrorFor(results, nameof(ParentViewModel.Email)).Should().BeTrue();
        HasErrorFor(results, nameof(ParentViewModel.MobilePhoneNumber)).Should().BeTrue();
        HasErrorFor(results, nameof(ParentViewModel.NationalRegisterNumber)).Should().BeTrue();
        HasErrorFor(results, nameof(ParentViewModel.Street)).Should().BeTrue();
        HasErrorFor(results, nameof(ParentViewModel.City)).Should().BeTrue();
        HasErrorFor(results, nameof(ParentViewModel.PostalCode)).Should().BeTrue();
    }

    [Fact]
    public void TeamMember_MissingRequiredFields_FailsWithMemberNames()
    {
        var model = new TeamMemberViewModel();
        var results = Validate(model);

        HasErrorFor(results, nameof(TeamMemberViewModel.FirstName)).Should().BeTrue();
        HasErrorFor(results, nameof(TeamMemberViewModel.LastName)).Should().BeTrue();
        HasErrorFor(results, nameof(TeamMemberViewModel.Email)).Should().BeTrue();
        HasErrorFor(results, nameof(TeamMemberViewModel.MobilePhoneNumber)).Should().BeTrue();
        HasErrorFor(results, nameof(TeamMemberViewModel.NationalRegisterNumber)).Should().BeTrue();
        HasErrorFor(results, nameof(TeamMemberViewModel.Street)).Should().BeTrue();
        HasErrorFor(results, nameof(TeamMemberViewModel.City)).Should().BeTrue();
        HasErrorFor(results, nameof(TeamMemberViewModel.PostalCode)).Should().BeTrue();
    }

    [Fact]
    public void Child_MissingRequiredFields_FailsWithMemberNames()
    {
        var model = new ChildViewModel();
        var results = Validate(model);

        HasErrorFor(results, nameof(ChildViewModel.FirstName)).Should().BeTrue();
        HasErrorFor(results, nameof(ChildViewModel.LastName)).Should().BeTrue();
        HasErrorFor(results, nameof(ChildViewModel.NationalRegisterNumber)).Should().BeTrue();
    }

    // --- invalid national register number ----------------------------------

    [Theory]
    [InlineData("85061513381")] // wrong check number (modulo-97)
    [InlineData("85991513380")] // impossible month
    [InlineData("notanumber11")]
    public void Parent_InvalidNrn_Fails(string nrn)
    {
        var model = ValidParent();
        model.NationalRegisterNumber = nrn;
        HasErrorFor(Validate(model), nameof(ParentViewModel.NationalRegisterNumber)).Should().BeTrue();
    }

    [Theory]
    [InlineData("16070816411")] // wrong check number
    [InlineData("16990816410")] // impossible month
    public void Child_InvalidNrn_Fails(string nrn)
    {
        var model = ValidChild();
        model.NationalRegisterNumber = nrn;
        HasErrorFor(Validate(model), nameof(ChildViewModel.NationalRegisterNumber)).Should().BeTrue();
    }

    [Fact]
    public void Nrn_TooShort_FailsLengthConstraint()
    {
        var model = ValidParent();
        model.NationalRegisterNumber = "12345"; // below MinimumLength 11
        HasErrorFor(Validate(model), nameof(ParentViewModel.NationalRegisterNumber)).Should().BeTrue();
    }

    // --- invalid phone / mobile --------------------------------------------

    [Theory]
    [InlineData("12345")]        // not a Belgian number at all
    [InlineData("0612345678")]   // 06 prefix is not a valid Belgian mobile
    [InlineData("0212345678")]   // landline, not mobile
    [InlineData("0460000000")]   // 046 not in [789] range
    public void Parent_InvalidMobile_Fails(string mobile)
    {
        var model = ValidParent();
        model.MobilePhoneNumber = mobile;
        HasErrorFor(Validate(model), nameof(ParentViewModel.MobilePhoneNumber)).Should().BeTrue();
    }

    [Theory]
    [InlineData("0612345678")]
    [InlineData("0212345678")]
    public void TeamMember_InvalidMobile_Fails(string mobile)
    {
        var model = ValidTeamMember();
        model.MobilePhoneNumber = mobile;
        HasErrorFor(Validate(model), nameof(TeamMemberViewModel.MobilePhoneNumber)).Should().BeTrue();
    }

    [Theory]
    [InlineData("not-a-phone")]
    [InlineData("123")]
    public void Parent_InvalidLandline_Fails(string landline)
    {
        var model = ValidParent();
        model.PhoneNumber = landline;
        HasErrorFor(Validate(model), nameof(ParentViewModel.PhoneNumber)).Should().BeTrue();
    }

    // --- string length boundaries ------------------------------------------

    [Fact]
    public void Parent_OverLongFirstName_FailsLengthConstraint()
    {
        var model = ValidParent();
        model.FirstName = new string('a', 101); // StringLength max 100
        HasErrorFor(Validate(model), nameof(ParentViewModel.FirstName)).Should().BeTrue();
    }

    [Fact]
    public void Parent_TooShortFirstName_FailsLengthConstraint()
    {
        var model = ValidParent();
        model.FirstName = "a"; // below MinimumLength 2
        HasErrorFor(Validate(model), nameof(ParentViewModel.FirstName)).Should().BeTrue();
    }

    [Fact]
    public void TeamMember_OverLongPostalCode_FailsLengthConstraint()
    {
        var model = ValidTeamMember();
        model.PostalCode = new string('9', 11); // StringLength max 10
        HasErrorFor(Validate(model), nameof(TeamMemberViewModel.PostalCode)).Should().BeTrue();
    }

    [Fact]
    public void Child_OverLongLastName_FailsLengthConstraint()
    {
        var model = ValidChild();
        model.LastName = new string('b', 101);
        HasErrorFor(Validate(model), nameof(ChildViewModel.LastName)).Should().BeTrue();
    }

    // --- email -------------------------------------------------------------

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@nodomain")]
    public void Parent_InvalidEmail_Fails(string email)
    {
        var model = ValidParent();
        model.Email = email;
        HasErrorFor(Validate(model), nameof(ParentViewModel.Email)).Should().BeTrue();
    }

    // --- TeamMember-specific: DailyCompensation range ----------------------

    [Fact]
    public void TeamMember_DailyCompensation_Null_Passes()
    {
        var model = ValidTeamMember();
        model.DailyCompensation = null;
        IsValid(model).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    public void TeamMember_DailyCompensation_WithinRange_Passes(int amount)
    {
        var model = ValidTeamMember();
        model.DailyCompensation = amount;
        IsValid(model).Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    public void TeamMember_DailyCompensation_OutOfRange_Fails(int amount)
    {
        var model = ValidTeamMember();
        model.DailyCompensation = amount;
        HasErrorFor(Validate(model), nameof(TeamMemberViewModel.DailyCompensation)).Should().BeTrue();
    }

    [Fact]
    public void TeamMember_OverLongLicenseUrl_FailsLengthConstraint()
    {
        var model = ValidTeamMember();
        model.LicenseUrl = new string('x', 256); // StringLength max 255
        HasErrorFor(Validate(model), nameof(TeamMemberViewModel.LicenseUrl)).Should().BeTrue();
    }
}
