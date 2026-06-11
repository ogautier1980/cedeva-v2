using Cedeva.Core.Helpers;

namespace Cedeva.Tests.Helpers;

public class NationalRegisterNumberHelperTests
{
    [Theory]
    [InlineData("15.03.10-123.45", "15031012345")]
    [InlineData("15031012345", "15031012345")]
    [InlineData("15 03 10 123 45", "15031012345")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void StripFormatting_KeepsOnlyDigits(string? input, string expected)
    {
        NationalRegisterNumberHelper.StripFormatting(input).Should().Be(expected);
    }

    [Fact]
    public void Format_WithElevenDigits_AppliesBelgianMask()
    {
        NationalRegisterNumberHelper.Format("15031012345").Should().Be("15.03.10-123.45");
    }

    [Fact]
    public void Format_WithAlreadyFormatted_Reformats()
    {
        NationalRegisterNumberHelper.Format("15.03.10-123.45").Should().Be("15.03.10-123.45");
    }

    [Theory]
    [InlineData("123")]            // too short
    [InlineData("150310123456")]   // too long (12 digits)
    public void Format_WithInvalidLength_ReturnsInputUnchanged(string input)
    {
        NationalRegisterNumberHelper.Format(input).Should().Be(input);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Format_WithNullOrEmpty_ReturnsEmpty(string? input)
    {
        NationalRegisterNumberHelper.Format(input).Should().Be(string.Empty);
    }

    [Theory]
    [InlineData("15031012345", true)]
    [InlineData("15.03.10-123.45", true)]
    [InlineData("123", false)]
    [InlineData("150310123456", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_ChecksElevenDigits(string? input, bool expected)
    {
        NationalRegisterNumberHelper.IsValid(input).Should().Be(expected);
    }
}
