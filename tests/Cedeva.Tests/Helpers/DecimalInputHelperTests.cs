using Cedeva.Core.Helpers;

namespace Cedeva.Tests.Helpers;

public class DecimalInputHelperTests
{
    [Theory]
    [InlineData("38.50", 38.50)]
    [InlineData("38,50", 38.50)]
    [InlineData("38", 38)]
    [InlineData("0.01", 0.01)]
    [InlineData("1234.56", 1234.56)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData(" 38.50 ", 38.50)]
    public void TryParse_AcceptsBothSeparatorConventions(string input, decimal expected)
    {
        DecimalInputHelper.TryParse(input, out var result).Should().BeTrue();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    public void TryParse_RejectsBlankOrNonNumeric(string? input)
    {
        DecimalInputHelper.TryParse(input, out _).Should().BeFalse();
    }
}
