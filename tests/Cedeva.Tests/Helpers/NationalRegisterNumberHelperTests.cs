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
    // Valid — born before 2000 (plain modulo-97 check)
    [InlineData("85.06.15-133.80", true)]
    [InlineData("85061513380", true)]
    [InlineData("90032223645", true)]
    // Valid — born in/after 2000 (check computed with the leading "2")
    [InlineData("15091012183", true)]
    [InlineData("18042520265", true)]
    // Invalid check number (off by one)
    [InlineData("85061513381", false)]
    [InlineData("15091012184", false)]
    // Impossible birth-date fragment
    [InlineData("85991513380", false)]
    // Wrong length / empty
    [InlineData("123", false)]
    [InlineData("150310123456", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_ValidatesChecksumDateAndLength(string? input, bool expected)
    {
        NationalRegisterNumberHelper.IsValid(input).Should().Be(expected);
    }
}
