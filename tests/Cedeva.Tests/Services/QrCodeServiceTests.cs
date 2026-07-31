using Cedeva.Infrastructure.Services;

namespace Cedeva.Tests.Services;

public class QrCodeServiceTests
{
    [Fact]
    public void GenerateDataUri_ReturnsPngDataUri()
    {
        var sut = new QrCodeService();

        var result = sut.GenerateDataUri("https://example.test/pay?bookingId=42");

        result.Should().StartWith("data:image/png;base64,");
        result.Length.Should().BeGreaterThan("data:image/png;base64,".Length);
    }

    [Fact]
    public void GenerateDataUri_DifferentContent_ProducesDifferentImages()
    {
        var sut = new QrCodeService();

        var first = sut.GenerateDataUri("https://example.test/pay?bookingId=1");
        var second = sut.GenerateDataUri("https://example.test/pay?bookingId=2");

        first.Should().NotBe(second);
    }
}
