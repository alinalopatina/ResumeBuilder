using System.Globalization;
using ResumeBuilder.Core.Localization;

namespace ResumeBuilder.Tests;

/// <summary>Локализация строк (.resx) и культурно-зависимое форматирование.</summary>
public class LocalizationTests
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private readonly ResxLocalizer _loc = new(Ru);

    [Fact]
    public void Strings_LoadedForEachCulture()
    {
        Assert.Equal("Опыт работы", _loc.Get("Section_Experience", Ru));
        Assert.Equal("Work experience", _loc.Get("Section_Experience", En));
    }

    [Fact]
    public void MissingCulture_FallsBackToNeutral() =>
        Assert.Equal("Опыт работы", _loc.Get("Section_Experience", De)); // немецкого .resx нет → нейтральный (русский)

    [Fact]
    public void UnknownKey_IsVisible() =>
        Assert.Equal("[No_Such_Key]", _loc["No_Such_Key"]);

    [Fact]
    public void ChangingCulture_RaisesEvent()
    {
        var raised = false;
        _loc.CultureChanged += (_, _) => raised = true;
        _loc.Culture = En;
        Assert.True(raised);
        Assert.Equal("Skills", _loc["Section_Skills"]);
    }

    [Theory]
    [InlineData(1, "One")]
    [InlineData(2, "Few")]
    [InlineData(5, "Many")]
    [InlineData(11, "Many")]
    [InlineData(21, "One")]
    [InlineData(22, "Few")]
    [InlineData(112, "Many")]
    public void RussianPluralRules(int n, string expected) =>
        Assert.Equal(expected, CultureFormatter.PluralForm(n, Ru));

    [Fact]
    public void Duration_IsDeclined()
    {
        Assert.Equal("1 год 1 месяц", CultureFormatter.FormatDuration(13, _loc, Ru));
        Assert.Equal("2 года 3 месяца", CultureFormatter.FormatDuration(27, _loc, Ru));
        Assert.Equal("5 лет", CultureFormatter.FormatDuration(60, _loc, Ru));
        Assert.Equal("1 year 1 month", CultureFormatter.FormatDuration(13, _loc, En));
        Assert.Equal("0 месяцев", CultureFormatter.FormatDuration(0, _loc, Ru));
    }

    [Fact]
    public void Money_UsesCultureSeparators_AndResumeCurrency()
    {
        var ru = CultureFormatter.FormatMoney(1500m, "BYN", Ru).Replace(' ', ' ').Replace(' ', ' ');
        var en = CultureFormatter.FormatMoney(1500m, "BYN", En);
        var de = CultureFormatter.FormatMoney(1234.5m, "EUR", De).Replace(' ', ' ');

        Assert.Equal("1 500 Br", ru);
        Assert.Equal("Br1,500", en);
        Assert.Equal("1.234,50 €", de);
    }
}
