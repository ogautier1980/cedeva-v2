using System.ComponentModel.DataAnnotations;
using Cedeva.Website.Validation;

namespace Cedeva.Tests.Validation;

public class ValidNationalRegisterNumberAttributeTests
{
    private static ValidationResult? Validate(string? value) =>
        new ValidNationalRegisterNumberAttribute()
            .GetValidationResult(value, new ValidationContext(new object()));

    [Theory]
    [InlineData("85061513380")]
    [InlineData("85.06.15-133.80")]
    [InlineData("15091012183")]
    public void ValidNumber_Passes(string value) =>
        Validate(value).Should().BeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_Passes_SoItComposesWithRequired(string? value) =>
        Validate(value).Should().BeNull();

    [Theory]
    [InlineData("85061513381")] // wrong check number
    [InlineData("85991513380")] // impossible month
    [InlineData("12345")]       // wrong length
    [InlineData("notanumber")]
    public void InvalidNumber_Fails(string value) =>
        Validate(value).Should().NotBeNull();
}
