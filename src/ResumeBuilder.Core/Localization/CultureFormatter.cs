using System.Globalization;

namespace ResumeBuilder.Core.Localization;

/// <summary>
/// Культурно-зависимое форматирование данных резюме (лаба «Локализация», модуль 3):
/// даты, денежные суммы, проценты и склонение числительных.
/// </summary>
public static class CultureFormatter
{
    /// <summary>Период работы: «сент. 2022 — н. в.» (ru) / «Sep 2022 — present» (en).</summary>
    public static string FormatPeriod(DateTime start, DateTime? end, ILocalizer loc, CultureInfo culture)
    {
        var from = start.ToString("MMM yyyy", culture);
        var to = end?.ToString("MMM yyyy", culture) ?? loc.Get("Period_Present", culture);
        return $"{from} — {to}";
    }

    /// <summary>
    /// Денежная сумма по правилам культуры, но с явной валютой резюме.
    /// Разделители тысяч/дробной части и положение символа берутся из <paramref name="culture"/>,
    /// а сам символ — из кода валюты (иначе для ru-RU всегда был бы «₽»).
    /// </summary>
    /// <example>1500 BYN: ru-RU → «1 500 Br», en-US → «Br1,500», de-DE → «1.500 Br»</example>
    public static string FormatMoney(decimal amount, string currencyCode, CultureInfo culture)
    {
        var nfi = (NumberFormatInfo)culture.NumberFormat.Clone();
        nfi.CurrencySymbol = CurrencySymbol(currencyCode);
        nfi.CurrencyDecimalDigits = amount == decimal.Truncate(amount) ? 0 : 2;
        return amount.ToString("C", nfi);
    }

    /// <summary>Символ валюты по коду ISO 4217.</summary>
    public static string CurrencySymbol(string? code) => code?.ToUpperInvariant() switch
    {
        "BYN" => "Br",
        "USD" => "$",
        "EUR" => "€",
        "RUB" => "₽",
        "PLN" => "zł",
        null or "" => string.Empty,
        _ => code
    };

    /// <summary>Длительность: «2 года 3 месяца» / «2 years 3 months» с правильным склонением.</summary>
    public static string FormatDuration(int totalMonths, ILocalizer loc, CultureInfo culture)
    {
        var years = totalMonths / 12;
        var months = totalMonths % 12;
        var parts = new List<string>();
        if (years > 0) parts.Add($"{years} {Plural(years, "Plural_Year", loc, culture)}");
        if (months > 0 || years == 0) parts.Add($"{months} {Plural(months, "Plural_Month", loc, culture)}");
        return string.Join(" ", parts);
    }

    /// <summary>
    /// Выбор формы слова по числу. В русском три формы (1 год, 2 года, 5 лет),
    /// в английском — две (1 year, 2 years). Ключи ресурсов: <c>{baseKey}_One/_Few/_Many</c>.
    /// </summary>
    public static string Plural(int n, string baseKey, ILocalizer loc, CultureInfo culture)
    {
        var form = PluralForm(n, culture);
        return loc.Get($"{baseKey}_{form}", culture);
    }

    /// <summary>Правило множественного числа CLDR (упрощённо для ru/be/uk и остальных языков).</summary>
    public static string PluralForm(int n, CultureInfo culture)
    {
        n = Math.Abs(n);
        if (culture.TwoLetterISOLanguageName is "ru" or "be" or "uk")
        {
            if (n % 10 == 1 && n % 100 != 11) return "One";
            if (n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14) return "Few";
            return "Many";
        }
        return n == 1 ? "One" : "Many";
    }
}
