using QRCoder;
using ResumeBuilder.Core.Abstractions;

namespace ResumeBuilder.Infrastructure.Qr;

/// <summary>
/// Генерация QR-кода через библиотеку QRCoder.
/// <see cref="PngByteQRCode"/> не использует System.Drawing, поэтому работает и на Windows, и в Linux/Docker.
/// </summary>
public sealed class QrCodeService : IQrCodeGenerator
{
    /// <inheritdoc />
    public byte[] GeneratePng(string payload, int pixelsPerModule = 10)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);
        using var generator = new QRCodeGenerator();
        // Уровень коррекции Q — восстанавливается до 25 % повреждённого кода (надёжно при печати)
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q, forceUtf8: true);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }
}
