using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Services;
using ResumeBuilder.Core.Text;

namespace ResumeBuilder.Tests;

/// <summary>Регулярные выражения: проверка формата (модуль 2), группы и замены (модуль 3).</summary>
public class TextProcessingTests
{
    [Theory]
    [InlineData("alina.lopatina@gmail.com", true)]
    [InlineData("user+tag@mail.example.by", true)]
    [InlineData("wrong@mail", false)]
    [InlineData("@gmail.com", false)]
    [InlineData("two@@at.com", false)]
    public void EmailFormat_ValidatesWholeString(string email, bool expected) =>
        Assert.Equal(expected, RegexPatterns.EmailFormat().IsMatch(email));

    [Theory]
    [InlineData("+375 (29) 123-45-67", "+375 (29) 123-45-67")]
    [InlineData("+375291234567", "+375 (29) 123-45-67")]
    [InlineData("8 029 1234567", "+375 (29) 123-45-67")]
    [InlineData("8 (033) 765-43-21", "+375 (33) 765-43-21")]
    [InlineData("80441112233", "+375 (44) 111-22-33")]
    public void NormalizePhone_BelarusianNumbers_ToSingleFormat(string input, string expected) =>
        Assert.Equal(expected, TextFormatter.NormalizePhone(input));

    [Fact]
    public void NormalizePhone_ForeignNumber_KeepsDigitsOnly() =>
        Assert.Equal("+4915123456789", TextFormatter.NormalizePhone("+49 151 2345-6789"));

    [Fact]
    public void DatesToIso_SwapsGroups() =>
        Assert.Equal("с 2024-09-01 по 2028-06-30", TextFormatter.DatesToIso("с 01.09.2024 по 30.06.2028"));

    [Fact]
    public void MaskEmails_HidesMostOfAddress()
    {
        var masked = TextFormatter.MaskEmails("пишите: alina.lopatina@gmail.com");
        Assert.Equal("пишите: al***@g***.com", masked);
        Assert.DoesNotContain("lopatina", masked);
    }

    [Fact]
    public void MaskPhones_KeepsOperatorCodeAndLastDigits() =>
        Assert.Equal("+375 (29) ***-**-67", TextFormatter.MaskPhones("+375 (29) 123-45-67"));

    [Theory]
    [InlineData("Лопатина Алина Николаевна", "Алина Лопатина")]
    [InlineData("Иванов Пётр", "Пётр Иванов")]
    [InlineData("Madonna", "Madonna")]
    public void SwapLastFirstName_UsesNamedGroups(string input, string expected) =>
        Assert.Equal(expected, TextFormatter.SwapLastFirstName(input));

    [Fact]
    public void ToShortName_BuildsInitials() =>
        Assert.Equal("Лопатина А. Н.", TextFormatter.ToShortName("Лопатина  Алина Николаевна"));

    [Fact]
    public void CollapseSpaces_RemovesExtraWhitespaceAndSpaceBeforePunctuation() =>
        Assert.Equal("Много пробелов, и знаков!", TextFormatter.CollapseSpaces("  Много   пробелов ,  и   знаков !  "));

    [Fact]
    public void GitHubLogin_Lookbehind_ExcludesPrefix() =>
        Assert.Equal("alinalopatina", RegexPatterns.GitHubLogin().Match("профиль: https://github.com/alinalopatina").Value);

    [Fact]
    public void TelegramHandle_DoesNotMatchEmail()
    {
        var matches = RegexPatterns.TelegramHandle().Matches("tg: @alina_dev, mail: alina@mail.by");
        Assert.Single(matches);
        Assert.Equal("alina_dev", matches[0].Groups["handle"].Value);
    }

    [Fact]
    public void Extract_FindsAllContactTypesInFreeText()
    {
        const string text = """
            ФИО: Петров Иван
            Телефон: 8 (029) 765-43-21, e-mail: Ivan.Petrov@mail.ru
            https://github.com/ivan-petrov  @ivan_qa
            Дата: 31.02.2024 и 12.05.2003
            Опыт: 09.2022 – 06.2024; 07.2024 - н.в.
            Навыки: Selenium, Postman; SQL
            """;
        var r = new TextExtractor().Extract(text);

        Assert.Equal(new[] { "ivan.petrov@mail.ru" }, r.Emails);
        Assert.Equal(new[] { "+375 (29) 765-43-21" }, r.Phones);
        Assert.Equal("ivan-petrov", r.GitHubLogin);
        Assert.Contains("ivan_qa", r.TelegramHandles);
        Assert.Single(r.Dates); // 31.02 отброшена: формат верный, но такой даты нет
        Assert.Equal(new DateTime(2003, 5, 12), r.Dates[0]);
        Assert.Equal(2, r.WorkPeriods.Count);
        Assert.Null(r.WorkPeriods[1].End);
        Assert.Equal(new[] { "Selenium", "Postman", "SQL" }, r.Skills);
        Assert.Equal("Петров Иван", r.KeyValues["ФИО"]);
    }

    [Fact]
    public void ApplyTo_FillsOnlyEmptyFields()
    {
        var resume = new Resume { Personal = { Email = "keep@me.by" } };
        var extractor = new TextExtractor();
        var found = extractor.Extract("e-mail: other@mail.ru, тел. +375291234567\nНавыки: C#, Git");

        var changes = extractor.ApplyTo(found, resume);

        Assert.Equal("keep@me.by", resume.Personal.Email);
        Assert.Equal("+375 (29) 123-45-67", resume.Personal.Phone);
        Assert.Equal(2, resume.Skills.Count);
        Assert.Equal(3, changes);
    }

    [Fact]
    public void ExtractHrefs_FromHtml() =>
        Assert.Equal(new[] { "https://bntu.by", "https://github.com" },
            TextExtractor.ExtractHrefs("<a href=\"https://bntu.by\">x</a><a class='c' href='https://github.com'>y</a>"));

    [Fact]
    public void VCard_EscapesSpecialCharacters()
    {
        var vcard = QrPayloadBuilder.BuildVCard(new PersonalInfo
        {
            FullName = "Лопатина Алина",
            DesiredPosition = "Developer; C#, .NET",
            Email = "a@b.by",
            Phone = "80291234567"
        });
        Assert.Contains(@"TITLE:Developer\; C#\, .NET", vcard);
        Assert.Contains("TEL;TYPE=CELL:+375 (29) 123-45-67", vcard);
        Assert.StartsWith("BEGIN:VCARD", vcard);
        Assert.EndsWith("END:VCARD", vcard);
    }
}
