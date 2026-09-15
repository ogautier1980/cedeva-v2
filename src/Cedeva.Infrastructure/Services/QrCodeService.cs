using Cedeva.Core.Interfaces;
using QRCoder;

namespace Cedeva.Infrastructure.Services;

/// <inheritdoc cref="IQrCodeService"/>
public class QrCodeService : IQrCodeService
{
    public string GenerateDataUri(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(data);
        var bytes = pngQrCode.GetGraphic(pixelsPerModule: 10);

        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    public string GenerateImageTag(string content) =>
        $"<img src=\"{GenerateDataUri(content)}\" alt=\"QR paiement\" style=\"width:180px;height:180px;\">";
}
