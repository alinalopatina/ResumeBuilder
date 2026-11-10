using QRCoder;
using ResumeBuilder.Core.Models;

namespace ResumeBuilder.Core.Services;

/// <summary>Создание QR-кода (библиотека QRCoder).</summary>
public static class QrCodeService
{
    /// <summary>Возвращает картинку PNG с QR-кодом.</summary>
    public static byte[] CreatePng(string text, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using QRCodeData data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q, forceUtf8: true);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }

    /// <summary>
    /// Что кладём в QR-код: визитку vCard. Если отсканировать телефоном,
    /// он предложит сохранить контакт.
    /// </summary>
    public static string BuildVCard(PersonalInfo p)
    {
        var lines = new List<string>
        {
            "BEGIN:VCARD",
            "VERSION:3.0",
            "FN:" + p.FullName,
            "TITLE:" + p.Position,
            "EMAIL:" + p.Email
        };
        if (!string.IsNullOrWhiteSpace(p.Phone)) lines.Add("TEL:" + TextHelper.NormalizePhone(p.Phone));
        if (!string.IsNullOrWhiteSpace(p.Website)) lines.Add("URL:" + p.Website);
        lines.Add("END:VCARD");
        return string.Join("\n", lines);
    }
}
