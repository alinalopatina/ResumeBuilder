using System.Globalization;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Localization;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Text;

namespace ResumeBuilder.Infrastructure.Pdf;

/// <summary>
/// Всё, что нужно шаблону для отрисовки: резюме, параметры, язык, готовый QR-код.
/// Общий для PDF и Word, чтобы правила отображения (маскирование, формат дат) были в одном месте.
/// </summary>
public sealed class ResumeRenderContext
{
    public ResumeRenderContext(Resume resume, ExportOptions options, ILocalizer localizer, byte[]? qrPng)
    {
        Resume = resume;
        Options = options;
        Localizer = localizer;
        QrPng = qrPng;
        Culture = CultureInfo.GetCultureInfo(options.CultureName ?? resume.CultureName);
    }

    public Resume Resume { get; }
    public ExportOptions Options { get; }
    public ILocalizer Localizer { get; }
    public CultureInfo Culture { get; }
    public byte[]? QrPng { get; }

    /// <summary>Локализованная строка на языке резюме.</summary>
    public string T(string key) => Localizer.Get(key, Culture);

    /// <summary>E-mail с учётом режима маскирования.</summary>
    public string Email => Options.MaskContacts
        ? TextFormatter.MaskEmails(Resume.Personal.Email)
        : Resume.Personal.Email;

    /// <summary>Телефон в едином формате и с учётом маскирования.</summary>
    public string Phone
    {
        get
        {
            var normalized = TextFormatter.NormalizePhone(Resume.Personal.Phone);
            return Options.MaskContacts ? TextFormatter.MaskPhones(normalized) : normalized;
        }
    }

    /// <summary>Имя: для английского резюме — в порядке «Имя Фамилия».</summary>
    public string DisplayName => Culture.TwoLetterISOLanguageName == "en"
        ? TextFormatter.SwapLastFirstName(Resume.Personal.FullName)
        : TextFormatter.CollapseSpaces(Resume.Personal.FullName);

    /// <summary>Период работы по правилам культуры.</summary>
    public string Period(WorkExperience e) => CultureFormatter.FormatPeriod(e.StartDate, e.EndDate, Localizer, Culture);

    /// <summary>Длительность работы («1 год 3 месяца»).</summary>
    public string Duration(WorkExperience e) =>
        CultureFormatter.FormatDuration(e.GetDurationMonths(DateTime.Today), Localizer, Culture);

    /// <summary>Зарплата по правилам культуры или <c>null</c>.</summary>
    public string? Salary => Resume.Personal.DesiredSalary is { } s
        ? CultureFormatter.FormatMoney(s, Resume.Personal.SalaryCurrency, Culture)
        : null;

    /// <summary>Возраст для отображения, например «21 год».</summary>
    public string? Age
    {
        get
        {
            if (Resume.Personal.BirthDate is not { } b) return null;
            var years = Core.Validation.Attributes.AgeCalculator.FullYears(b, DateTime.Today);
            return $"{years} {CultureFormatter.Plural(years, "Plural_Year", Localizer, Culture)}";
        }
    }

    /// <summary>Строка со звёздочками/точками уровня навыка.</summary>
    public static string LevelDots(int level) =>
        new string('●', Math.Clamp(level, 0, Skill.MaxLevel)) + new string('○', Skill.MaxLevel - Math.Clamp(level, 0, Skill.MaxLevel));
}
