using System.Globalization;
using System.Text.RegularExpressions;
using ResumeBuilder.Core.Models;

namespace ResumeBuilder.Core.Text;

/// <summary>Найденный период работы.</summary>
/// <param name="Start">Начало (первое число месяца).</param>
/// <param name="End">Конец; <c>null</c> — «по настоящее время».</param>
/// <param name="SourceText">Исходный фрагмент текста.</param>
public sealed record ExtractedPeriod(DateTime Start, DateTime? End, string SourceText);

/// <summary>Результат «умной вставки»: всё, что удалось распознать в произвольном тексте.</summary>
public sealed class TextExtractionResult
{
    public List<string> Emails { get; } = [];
    public List<string> Phones { get; } = [];
    public List<string> Urls { get; } = [];
    public List<string> TelegramHandles { get; } = [];
    public string? GitHubLogin { get; set; }
    public List<DateTime> Dates { get; } = [];
    public List<ExtractedPeriod> WorkPeriods { get; } = [];
    public List<string> Skills { get; } = [];

    /// <summary>Пары «ключ: значение» из полуструктурированного текста (модуль 4.2).</summary>
    public Dictionary<string, string> KeyValues { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Есть ли хоть какой-то результат.</summary>
    public bool IsEmpty => Emails.Count + Phones.Count + Urls.Count + Dates.Count + Skills.Count + KeyValues.Count == 0;

    /// <summary>Человекочитаемый список найденного (для отображения в UI и консоли).</summary>
    public IEnumerable<string> Describe()
    {
        foreach (var e in Emails) yield return $"E-mail: {e}";
        foreach (var p in Phones) yield return $"Phone: {p}";
        foreach (var u in Urls) yield return $"URL: {u}";
        if (GitHubLogin is not null) yield return $"GitHub: {GitHubLogin}";
        foreach (var t in TelegramHandles) yield return $"Telegram: @{t}";
        foreach (var d in Dates) yield return $"Date: {d:yyyy-MM-dd}";
        foreach (var p in WorkPeriods) yield return $"Period: {p.Start:MM.yyyy} – {(p.End is null ? "…" : p.End.Value.ToString("MM.yyyy"))}";
        if (Skills.Count > 0) yield return $"Skills: {string.Join(", ", Skills)}";
        foreach (var kv in KeyValues) yield return $"{kv.Key} = {kv.Value}";
    }
}

/// <summary>
/// Поиск и извлечение структурированных данных из произвольного текста
/// (лаба «Регулярные выражения», модуль 2 — поиск по шаблону, извлечение данных).
/// </summary>
/// <remarks>
/// Применение в проекте: пользователь вставляет текст старого резюме или подписи из письма,
/// а программа сама находит e-mail, телефон, ссылки, периоды работы и навыки.
/// </remarks>
public sealed class TextExtractor
{
    /// <summary>Анализирует текст и возвращает всё найденное.</summary>
    public TextExtractionResult Extract(string? text)
    {
        var result = new TextExtractionResult();
        if (string.IsNullOrWhiteSpace(text)) return result;

        // 1. E-mail (Matches + Distinct — без повторов)
        result.Emails.AddRange(RegexPatterns.EmailSearch().Matches(text)
            .Select(m => m.Value.ToLowerInvariant()).Distinct());

        // 2. Телефоны — сразу нормализуем с помощью групп
        result.Phones.AddRange(RegexPatterns.BelarusPhone().Matches(text)
            .Select(m => TextFormatter.NormalizePhone(m.Value)).Distinct());

        // 3. Ссылки и логин GitHub (lookbehind)
        result.Urls.AddRange(RegexPatterns.UrlSearch().Matches(text)
            .Select(m => TextFormatter.NormalizeUrl(m.Value)).Distinct(StringComparer.OrdinalIgnoreCase));
        var gh = RegexPatterns.GitHubLogin().Match(text);
        if (gh.Success) result.GitHubLogin = gh.Value;

        // 4. Telegram-ники (негативный lookbehind отсекает e-mail)
        result.TelegramHandles.AddRange(RegexPatterns.TelegramHandle().Matches(text)
            .Select(m => m.Groups["handle"].Value).Distinct());

        // 5. Даты ДД.ММ.ГГГГ: формат проверяет RegEx, а реальность даты (31.02) — DateTime.TryParseExact
        foreach (Match m in RegexPatterns.DateDdMmYyyy().Matches(text))
        {
            if (DateTime.TryParseExact(m.Value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                result.Dates.Add(date);
        }

        // 6. Периоды работы ММ.ГГГГ – ММ.ГГГГ / н.в.
        foreach (Match m in RegexPatterns.WorkPeriod().Matches(text))
        {
            if (!DateTime.TryParseExact(m.Groups["from"].Value, "MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
                continue;
            DateTime? end = DateTime.TryParseExact(m.Groups["to"].Value, "MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var e)
                ? e
                : null;
            result.WorkPeriods.Add(new ExtractedPeriod(start, end, m.Value));
        }

        // 7. Навыки: строка после «Навыки:», разбитая по запятым / точкам с запятой
        var skillsLine = RegexPatterns.SkillsLine().Match(text);
        if (skillsLine.Success)
        {
            result.Skills.AddRange(Regex.Split(skillsLine.Value, @"\s*[,;•|]\s*", RegexOptions.None, TimeSpan.FromSeconds(1))
                .Select(s => s.Trim().TrimEnd('.'))
                .Where(s => s.Length is > 0 and <= 60)
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        // 8. Пары «Ключ: значение»
        foreach (Match m in RegexPatterns.KeyValueLine().Matches(text))
            result.KeyValues.TryAdd(m.Groups["key"].Value.Trim(), m.Groups["value"].Value.Trim());

        return result;
    }

    /// <summary>
    /// Переносит распознанные данные в резюме, <b>не затирая</b> уже заполненные поля.
    /// </summary>
    /// <returns>Количество заполненных полей / добавленных элементов.</returns>
    public int ApplyTo(TextExtractionResult found, Resume resume)
    {
        var changes = 0;
        var p = resume.Personal;

        if (string.IsNullOrWhiteSpace(p.Email) && found.Emails.Count > 0) { p.Email = found.Emails[0]; changes++; }
        if (string.IsNullOrWhiteSpace(p.Phone) && found.Phones.Count > 0) { p.Phone = found.Phones[0]; changes++; }
        if (string.IsNullOrWhiteSpace(p.Website))
        {
            var site = found.Urls.FirstOrDefault(u => u.Contains("github.com", StringComparison.OrdinalIgnoreCase))
                       ?? found.Urls.FirstOrDefault();
            if (site is not null) { p.Website = site; changes++; }
        }

        if (string.IsNullOrWhiteSpace(p.FullName) && TryGetValue(found, out var name, "ФИО", "Имя", "Name", "Full name"))
        { p.FullName = TextFormatter.CollapseSpaces(name); changes++; }
        if (string.IsNullOrWhiteSpace(p.City) && TryGetValue(found, out var city, "Город", "City"))
        { p.City = city; changes++; }
        if (string.IsNullOrWhiteSpace(p.DesiredPosition) && TryGetValue(found, out var pos, "Должность", "Position", "Желаемая должность"))
        { p.DesiredPosition = pos; changes++; }

        foreach (var skill in found.Skills)
        {
            if (resume.Skills.Any(s => s.Name.Equals(skill, StringComparison.OrdinalIgnoreCase))) continue;
            resume.Skills.Add(new Skill { Name = skill, Level = 3 });
            changes++;
        }

        return changes;
    }

    private static bool TryGetValue(TextExtractionResult found, out string value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (found.KeyValues.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
            {
                value = v;
                return true;
            }
        }
        value = string.Empty;
        return false;
    }

    /// <summary>Извлекает все href из HTML-фрагмента (задание 2.3).</summary>
    public static IReadOnlyList<string> ExtractHrefs(string html) =>
        RegexPatterns.HtmlHref().Matches(html).Select(m => m.Groups["url"].Value).ToList();
}
