using Cedeva.Infrastructure.Services.Financial;

namespace Cedeva.Tests.Services.Financial;

public class StructuredCommunicationServiceTests
{
    private readonly StructuredCommunicationService _sut = new();

    [Fact]
    public void Generate_ReturnsBelgianFormat()
    {
        var result = _sut.GenerateStructuredCommunication(123);

        // 123 padded to 0000000123, checksum = 123 % 97 = 26
        result.Should().Be("+++000/0000/12326+++");
    }

    [Fact]
    public void Generate_WhenModulo97IsZero_Uses97AsChecksum()
    {
        // 97 % 97 == 0 -> checksum must become 97
        var result = _sut.GenerateStructuredCommunication(97);

        result.Should().Be("+++000/0000/09797+++");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(97)]
    [InlineData(123)]
    [InlineData(999999)]
    [InlineData(2_147_483_647)] // int.MaxValue
    public void Generate_ThenExtract_RoundTripsBookingId(int bookingId)
    {
        var communication = _sut.GenerateStructuredCommunication(bookingId);

        _sut.ValidateStructuredCommunication(communication).Should().BeTrue();
        _sut.ExtractBookingIdFromCommunication(communication).Should().Be(bookingId);
    }

    [Fact]
    public void Validate_WithCorrectChecksum_ReturnsTrue()
    {
        _sut.ValidateStructuredCommunication("+++000/0000/12326+++").Should().BeTrue();
    }

    [Fact]
    public void Validate_WithWrongChecksum_ReturnsFalse()
    {
        // Same base number but checksum 99 instead of 26
        _sut.ValidateStructuredCommunication("+++000/0000/12399+++").Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123/0000/12326")]          // missing +++
    [InlineData("+++00/0000/12326+++")]     // wrong group lengths
    [InlineData("+++000/000/12326+++")]
    [InlineData("+++abc/0000/12326+++")]    // non-digits
    [InlineData("not a communication")]
    public void Validate_WithMalformedInput_ReturnsFalse(string input)
    {
        _sut.ValidateStructuredCommunication(input).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithNull_ReturnsFalse()
    {
        _sut.ValidateStructuredCommunication(null!).Should().BeFalse();
    }

    [Fact]
    public void Extract_WithInvalidCommunication_ReturnsNull()
    {
        _sut.ExtractBookingIdFromCommunication("+++000/0000/12399+++").Should().BeNull();
    }
}
