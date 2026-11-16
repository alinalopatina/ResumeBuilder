using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Services;
using Xunit;

namespace ResumeBuilder.Tests;

/// <summary>Содержимое QR-кода (vCard) и сама картинка.</summary>
public class QrCodeTests
{
    [Fact]
    public void VCard_HasAllContacts()
    {
        PersonalInfo p = Resume.CreateSample().Personal;
        string vcard = QrCodeService.BuildVCard(p);

        Assert.StartsWith("BEGIN:VCARD", vcard);
        Assert.EndsWith("END:VCARD", vcard);
        Assert.Contains("FN:" + p.FullName, vcard);
        Assert.Contains("EMAIL:" + p.Email, vcard);
        Assert.Contains("TEL:+375 (29) 123-45-67", vcard);
        Assert.Contains("URL:https://github.com/maria-ivanova", vcard);
    }

    [Fact]
    public void VCard_NoPhoneAndSite_LinesSkipped()
    {
        var p = new PersonalInfo { FullName = "Тест", Email = "t@t.by" };
        string vcard = QrCodeService.BuildVCard(p);

        Assert.DoesNotContain("TEL:", vcard);
        Assert.DoesNotContain("URL:", vcard);
    }

    [Fact]
    public void CreatePng_ReturnsPngImage()
    {
        byte[] png = QrCodeService.CreatePng("https://github.com/maria-ivanova");

        // Любой PNG-файл начинается с байтов 89 50 4E 47 («‰PNG»)
        Assert.True(png.Length > 100);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
    }
}
