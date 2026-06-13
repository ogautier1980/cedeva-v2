using System.ComponentModel.DataAnnotations;
using Cedeva.Website.Features.PublicRegistration.ViewModels;

namespace Cedeva.Tests.Validation;

/// <summary>
/// Data-annotation validation tests for the public-registration ViewModels
/// (ParentInformationViewModel, ChildInformationViewModel, SelectActivityViewModel,
/// SimpleRegistrationViewModel) which were 0% covered.
///
/// These models are validated by ASP.NET model binding via <see cref="Validator.TryValidateObject"/>;
/// ErrorMessage values are localization KEYS, so assertions check MemberNames, not message text.
/// The [ValidNationalRegisterNumber] attribute composes with [Required]: blank passes the NRN
/// attribute (Required decides), a non-blank value must pass the modulo-97 checksum.
/// Valid national register numbers: adult 85.06.15-133.80, child 16.07.08-164.10.
/// The public models use the framework [Phone] attribute (not a Belgian-specific validator).
/// </summary>
public class PublicRegistrationViewModelValidationTests
{
    private const string ValidAdultNrn = "85.06.15-133.80";
    private const string ValidChildNrn = "16.07.08-164.10";

    // --- helpers -----------------------------------------------------------

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    private static bool IsValid(object model) => Validate(model).Count == 0;

    private static bool HasErrorFor(IEnumerable<ValidationResult> results, string memberName) =>
        results.Any(r => r.MemberNames.Contains(memberName));

    private static ParentInformationViewModel ValidParentInfo() => new()
    {
        FirstName = "Paul",
        LastName = "Parent",
        Email = "paul.parent@test.be",
        PhoneNumber = "02 123 45 67",
        MobilePhoneNumber = "0470 00 00 00",
        NationalRegisterNumber = ValidAdultNrn,
        Street = "Rue de Test 1",
        PostalCode = "1000",
        City = "Bruxelles",
        ActivityId = 1
    };

    private static ChildInformationViewModel ValidChildInfo() => new()
    {
        FirstName = "Chloe",
        LastName = "Enfant",
        BirthDate = new DateTime(2016, 7, 8),
        NationalRegisterNumber = ValidChildNrn,
        ActivityId = 1,
        ParentId = 1
    };

    private static SelectActivityViewModel ValidSelectActivity() => new()
    {
        ActivityId = 5
    };

    private static SimpleRegistrationViewModel ValidSimpleRegistration() => new()
    {
        ActivityId = 1,
        ParentFirstName = "Paul",
        ParentLastName = "Parent",
        ParentEmail = "paul.parent@test.be",
        ParentPhoneNumber = "02 123 45 67",
        ParentMobilePhoneNumber = "0470 00 00 00",
        ParentStreet = "Rue de Test 1",
        ParentPostalCode = "1000",
        ParentCity = "Bruxelles",
        ParentNationalRegisterNumber = ValidAdultNrn,
        ChildFirstName = "Chloe",
        ChildLastName = "Enfant",
        ChildBirthDate = new DateTime(2016, 7, 8),
        ChildNationalRegisterNumber = ValidChildNrn
    };

    // =======================================================================
    // ParentInformationViewModel
    // =======================================================================

    [Fact]
    public void ParentInfo_FullyValid_Passes()
    {
        IsValid(ValidParentInfo()).Should().BeTrue();
    }

    [Fact]
    public void ParentInfo_OptionalLandline_Null_Passes()
    {
        var model = ValidParentInfo();
        model.PhoneNumber = null;
        IsValid(model).Should().BeTrue();
    }

    [Theory]
    [InlineData("85.06.15-133.80")]
    [InlineData("85061513380")]
    public void ParentInfo_AcceptsFormattedAndUnformattedNrn(string nrn)
    {
        var model = ValidParentInfo();
        model.NationalRegisterNumber = nrn;
        IsValid(model).Should().BeTrue();
    }

    [Fact]
    public void ParentInfo_MissingRequiredFields_FailsWithMemberNames()
    {
        var results = Validate(new ParentInformationViewModel());

        HasErrorFor(results, nameof(ParentInformationViewModel.FirstName)).Should().BeTrue();
        HasErrorFor(results, nameof(ParentInformationViewModel.LastName)).Should().BeTrue();
        HasErrorFor(results, nameof(ParentInformationViewModel.Email)).Should().BeTrue();
        HasErrorFor(results, nameof(ParentInformationViewModel.MobilePhoneNumber)).Should().BeTrue();
        HasErrorFor(results, nameof(ParentInformationViewModel.NationalRegisterNumber)).Should().BeTrue();
        HasErrorFor(results, nameof(ParentInformationViewModel.Street)).Should().BeTrue();
        HasErrorFor(results, nameof(ParentInformationViewModel.PostalCode)).Should().BeTrue();
        HasErrorFor(results, nameof(ParentInformationViewModel.City)).Should().BeTrue();
    }

    [Fact]
    public void ParentInfo_OptionalLandline_NotInRequiredErrors()
    {
        // PhoneNumber is optional => no Required error even on an empty model.
        var results = Validate(new ParentInformationViewModel());
        HasErrorFor(results, nameof(ParentInformationViewModel.PhoneNumber)).Should().BeFalse();
    }

    [Theory]
    [InlineData("85061513381")] // wrong modulo-97 check number
    [InlineData("85991513380")] // impossible month
    [InlineData("notanumber11")]
    public void ParentInfo_InvalidNrn_Fails(string nrn)
    {
        var model = ValidParentInfo();
        model.NationalRegisterNumber = nrn;
        HasErrorFor(Validate(model), nameof(ParentInformationViewModel.NationalRegisterNumber)).Should().BeTrue();
    }

    [Fact]
    public void ParentInfo_NrnOverLength_FailsLengthConstraint()
    {
        var model = ValidParentInfo();
        model.NationalRegisterNumber = new string('1', 16); // StringLength max 15
        HasErrorFor(Validate(model), nameof(ParentInformationViewModel.NationalRegisterNumber)).Should().BeTrue();
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@nodomain")]
    public void ParentInfo_InvalidEmail_Fails(string email)
    {
        var model = ValidParentInfo();
        model.Email = email;
        HasErrorFor(Validate(model), nameof(ParentInformationViewModel.Email)).Should().BeTrue();
    }

    [Fact]
    public void ParentInfo_InvalidMobilePhone_Fails()
    {
        var model = ValidParentInfo();
        model.MobilePhoneNumber = "not a phone!!!";
        HasErrorFor(Validate(model), nameof(ParentInformationViewModel.MobilePhoneNumber)).Should().BeTrue();
    }

    [Fact]
    public void ParentInfo_InvalidLandline_Fails()
    {
        var model = ValidParentInfo();
        model.PhoneNumber = "not a phone!!!";
        HasErrorFor(Validate(model), nameof(ParentInformationViewModel.PhoneNumber)).Should().BeTrue();
    }

    [Fact]
    public void ParentInfo_OverLongFirstName_FailsLengthConstraint()
    {
        var model = ValidParentInfo();
        model.FirstName = new string('a', 101); // StringLength max 100
        HasErrorFor(Validate(model), nameof(ParentInformationViewModel.FirstName)).Should().BeTrue();
    }

    [Fact]
    public void ParentInfo_OverLongPostalCode_FailsLengthConstraint()
    {
        var model = ValidParentInfo();
        model.PostalCode = new string('9', 11); // StringLength max 10
        HasErrorFor(Validate(model), nameof(ParentInformationViewModel.PostalCode)).Should().BeTrue();
    }

    [Fact]
    public void ParentInfo_OverLongEmail_FailsLengthConstraint()
    {
        var model = ValidParentInfo();
        model.Email = new string('a', 200) + "@test.be"; // StringLength max 200
        HasErrorFor(Validate(model), nameof(ParentInformationViewModel.Email)).Should().BeTrue();
    }

    // =======================================================================
    // ChildInformationViewModel
    // =======================================================================

    [Fact]
    public void ChildInfo_FullyValid_Passes()
    {
        IsValid(ValidChildInfo()).Should().BeTrue();
    }

    [Theory]
    [InlineData("16.07.08-164.10")]
    [InlineData("16070816410")]
    public void ChildInfo_AcceptsFormattedAndUnformattedNrn(string nrn)
    {
        var model = ValidChildInfo();
        model.NationalRegisterNumber = nrn;
        IsValid(model).Should().BeTrue();
    }

    [Fact]
    public void ChildInfo_MissingRequiredFields_FailsWithMemberNames()
    {
        var results = Validate(new ChildInformationViewModel());

        HasErrorFor(results, nameof(ChildInformationViewModel.FirstName)).Should().BeTrue();
        HasErrorFor(results, nameof(ChildInformationViewModel.LastName)).Should().BeTrue();
        HasErrorFor(results, nameof(ChildInformationViewModel.NationalRegisterNumber)).Should().BeTrue();
    }

    [Theory]
    [InlineData("16070816411")] // wrong modulo-97 check number
    [InlineData("16990816410")] // impossible month
    public void ChildInfo_InvalidNrn_Fails(string nrn)
    {
        var model = ValidChildInfo();
        model.NationalRegisterNumber = nrn;
        HasErrorFor(Validate(model), nameof(ChildInformationViewModel.NationalRegisterNumber)).Should().BeTrue();
    }

    [Fact]
    public void ChildInfo_OverLongLastName_FailsLengthConstraint()
    {
        var model = ValidChildInfo();
        model.LastName = new string('b', 101); // StringLength max 100
        HasErrorFor(Validate(model), nameof(ChildInformationViewModel.LastName)).Should().BeTrue();
    }

    [Fact]
    public void ChildInfo_BooleanFlags_DoNotAffectValidity()
    {
        var model = ValidChildInfo();
        model.IsDisadvantagedEnvironment = true;
        model.IsMildDisability = true;
        model.IsSevereDisability = true;
        IsValid(model).Should().BeTrue();
    }

    // =======================================================================
    // SelectActivityViewModel
    // =======================================================================

    [Fact]
    public void SelectActivity_WithActivityId_Passes()
    {
        IsValid(ValidSelectActivity()).Should().BeTrue();
    }

    [Fact]
    public void SelectActivity_MissingActivityId_Fails()
    {
        var model = new SelectActivityViewModel { ActivityId = null };
        HasErrorFor(Validate(model), nameof(SelectActivityViewModel.ActivityId)).Should().BeTrue();
    }

    // =======================================================================
    // SimpleRegistrationViewModel
    // =======================================================================

    [Fact]
    public void SimpleRegistration_FullyValid_Passes()
    {
        IsValid(ValidSimpleRegistration()).Should().BeTrue();
    }

    [Fact]
    public void SimpleRegistration_OptionalParentNrnNull_Passes()
    {
        // ParentNationalRegisterNumber is optional (no [Required]) — null must pass.
        var model = ValidSimpleRegistration();
        model.ParentNationalRegisterNumber = null;
        IsValid(model).Should().BeTrue();
    }

    [Fact]
    public void SimpleRegistration_OptionalParentMobileNull_Passes()
    {
        var model = ValidSimpleRegistration();
        model.ParentMobilePhoneNumber = null;
        IsValid(model).Should().BeTrue();
    }

    [Fact]
    public void SimpleRegistration_MissingRequiredFields_FailsWithMemberNames()
    {
        var results = Validate(new SimpleRegistrationViewModel());

        HasErrorFor(results, nameof(SimpleRegistrationViewModel.ParentFirstName)).Should().BeTrue();
        HasErrorFor(results, nameof(SimpleRegistrationViewModel.ParentLastName)).Should().BeTrue();
        HasErrorFor(results, nameof(SimpleRegistrationViewModel.ParentEmail)).Should().BeTrue();
        HasErrorFor(results, nameof(SimpleRegistrationViewModel.ParentPhoneNumber)).Should().BeTrue();
        HasErrorFor(results, nameof(SimpleRegistrationViewModel.ParentStreet)).Should().BeTrue();
        HasErrorFor(results, nameof(SimpleRegistrationViewModel.ParentPostalCode)).Should().BeTrue();
        HasErrorFor(results, nameof(SimpleRegistrationViewModel.ParentCity)).Should().BeTrue();
        HasErrorFor(results, nameof(SimpleRegistrationViewModel.ChildFirstName)).Should().BeTrue();
        HasErrorFor(results, nameof(SimpleRegistrationViewModel.ChildLastName)).Should().BeTrue();
        HasErrorFor(results, nameof(SimpleRegistrationViewModel.ChildNationalRegisterNumber)).Should().BeTrue();
    }

    [Fact]
    public void SimpleRegistration_OptionalFieldsNotInRequiredErrors()
    {
        var results = Validate(new SimpleRegistrationViewModel());
        HasErrorFor(results, nameof(SimpleRegistrationViewModel.ParentMobilePhoneNumber)).Should().BeFalse();
        HasErrorFor(results, nameof(SimpleRegistrationViewModel.ParentNationalRegisterNumber)).Should().BeFalse();
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@nodomain")]
    public void SimpleRegistration_InvalidParentEmail_Fails(string email)
    {
        var model = ValidSimpleRegistration();
        model.ParentEmail = email;
        HasErrorFor(Validate(model), nameof(SimpleRegistrationViewModel.ParentEmail)).Should().BeTrue();
    }

    [Fact]
    public void SimpleRegistration_InvalidParentPhone_Fails()
    {
        var model = ValidSimpleRegistration();
        model.ParentPhoneNumber = "not a phone!!!";
        HasErrorFor(Validate(model), nameof(SimpleRegistrationViewModel.ParentPhoneNumber)).Should().BeTrue();
    }

    [Theory]
    [InlineData("85061513381")] // wrong modulo-97 check number
    [InlineData("85991513380")] // impossible month
    public void SimpleRegistration_InvalidParentNrn_Fails(string nrn)
    {
        var model = ValidSimpleRegistration();
        model.ParentNationalRegisterNumber = nrn;
        HasErrorFor(Validate(model), nameof(SimpleRegistrationViewModel.ParentNationalRegisterNumber)).Should().BeTrue();
    }

    [Theory]
    [InlineData("16070816411")] // wrong modulo-97 check number
    [InlineData("16990816410")] // impossible month
    public void SimpleRegistration_InvalidChildNrn_Fails(string nrn)
    {
        var model = ValidSimpleRegistration();
        model.ChildNationalRegisterNumber = nrn;
        HasErrorFor(Validate(model), nameof(SimpleRegistrationViewModel.ChildNationalRegisterNumber)).Should().BeTrue();
    }

    [Fact]
    public void SimpleRegistration_OverLongParentFirstName_FailsLengthConstraint()
    {
        var model = ValidSimpleRegistration();
        model.ParentFirstName = new string('a', 101); // StringLength max 100
        HasErrorFor(Validate(model), nameof(SimpleRegistrationViewModel.ParentFirstName)).Should().BeTrue();
    }

    [Fact]
    public void SimpleRegistration_OverLongParentEmail_FailsLengthConstraint()
    {
        var model = ValidSimpleRegistration();
        model.ParentEmail = new string('a', 255) + "@test.be"; // StringLength max 255
        HasErrorFor(Validate(model), nameof(SimpleRegistrationViewModel.ParentEmail)).Should().BeTrue();
    }

    [Fact]
    public void SimpleRegistration_OverLongChildLastName_FailsLengthConstraint()
    {
        var model = ValidSimpleRegistration();
        model.ChildLastName = new string('b', 101); // StringLength max 100
        HasErrorFor(Validate(model), nameof(SimpleRegistrationViewModel.ChildLastName)).Should().BeTrue();
    }

    [Fact]
    public void SimpleRegistration_BooleanFlagsAndQuestionAnswers_DoNotAffectValidity()
    {
        var model = ValidSimpleRegistration();
        model.IsDisadvantagedEnvironment = true;
        model.IsMildDisability = true;
        model.IsSevereDisability = true;
        model.QuestionAnswers = new Dictionary<int, string> { [1] = "Answer A", [2] = "Answer B" };
        IsValid(model).Should().BeTrue();
    }
}
