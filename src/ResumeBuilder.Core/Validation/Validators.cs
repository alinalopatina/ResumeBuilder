using System.ComponentModel.DataAnnotations;
using ResumeBuilder.Core.Localization;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Text;

namespace ResumeBuilder.Core.Validation;

/// <summary>Контракт валидатора резюме (паттерн «Стратегия»: ручной, декларативный, составной).</summary>
public interface IResumeValidator
{
    /// <summary>Проверяет резюме и возвращает отчёт со <b>всеми</b> нарушениями (без исключения на первой ошибке).</summary>
    ValidationReport Validate(Resume resume);
}

/// <summary>Исключение с понятным сообщением, если резюме нельзя экспортировать.</summary>
public sealed class ResumeValidationException(ValidationReport report)
    : Exception(string.Join(Environment.NewLine, report.Errors.Select(e => e.Message)))
{
    /// <summary>Отчёт о валидации.</summary>
    public ValidationReport Report { get; } = report;
}

/// <summary>
/// <b>Модуль 1 — ручные проверки.</b> Обычные условные операторы и понятные сообщения.
/// Проверяет то, что неудобно выразить атрибутами: связи между полями (семантика и целостность).
/// </summary>
public sealed class ManualResumeValidator(ILocalizer localizer, Func<DateTime>? clock = null) : IResumeValidator
{
    private readonly Func<DateTime> _clock = clock ?? (() => DateTime.Today);

    /// <inheritdoc />
    public ValidationReport Validate(Resume resume)
    {
        ArgumentNullException.ThrowIfNull(resume);
        var issues = new List<ValidationIssue>();
        var today = _clock();
        var p = resume.Personal;

        // --- Задание 1.1: простые проверки параметров ---
        if (string.IsNullOrWhiteSpace(p.FullName))
            issues.Add(Issue("Personal.FullName", "Validation_FullNameRequired"));

        if (string.IsNullOrWhiteSpace(p.Email))
            issues.Add(Issue("Personal.Email", "Validation_EmailRequired"));
        else if (!p.Email.Contains('@'))
            issues.Add(Issue("Personal.Email", "Validation_EmailFormat"));

        if (p.BirthDate is { } birth)
        {
            var age = Attributes.AgeCalculator.FullYears(birth, today);
            if (age is < 14 or > 100)
                issues.Add(Issue("Personal.BirthDate", "Validation_MinAge"));
        }

        if (string.IsNullOrWhiteSpace(p.Phone) && string.IsNullOrWhiteSpace(p.Website))
            issues.Add(Issue("Personal.Phone", "Validation_NoContactForQr", ValidationSeverity.Warning));

        // --- Задание 1.2: валидация сложного объекта (списки, связи) ---
        if (resume.Experience.Count == 0 && resume.Education.Count == 0)
            issues.Add(Issue("Experience", "Validation_NoExperienceNoEducation"));

        if (resume.Skills.Count == 0)
            issues.Add(Issue("Skills", "Validation_NoSkills", ValidationSeverity.Warning));

        var duplicates = resume.Skills
            .GroupBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1 && g.Key.Length > 0)
            .Select(g => g.Key);
        foreach (var dup in duplicates)
            issues.Add(new ValidationIssue("Skills", localizer.Format("Validation_DuplicateSkill", dup), ValidationSeverity.Warning));

        if (string.IsNullOrWhiteSpace(resume.Summary))
            issues.Add(Issue("Summary", "Validation_SummaryEmpty", ValidationSeverity.Warning));
        else if (resume.Summary.Length > 1500)
            issues.Add(Issue("Summary", "Validation_SummaryTooLong", ValidationSeverity.Warning));

        // --- Задание 1.3: кастомная логика дат (полиморфный вызов Validate у каждого элемента) ---
        AddItemIssues(issues, resume.Experience, "Experience", today);
        AddItemIssues(issues, resume.Education, "Education", today);
        AddItemIssues(issues, resume.Skills, "Skills", today);

        // Пересечение периодов работы — предупреждение (совместительство возможно)
        var ordered = resume.Experience.OrderBy(e => e.StartDate).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            var prevEnd = ordered[i - 1].EndDate ?? today;
            if (ordered[i].StartDate < prevEnd.AddMonths(-1))
                issues.Add(new ValidationIssue("Experience",
                    localizer.Format("Validation_OverlappingJobs", ordered[i - 1].Company, ordered[i].Company),
                    ValidationSeverity.Warning));
        }

        return new ValidationReport(issues);
    }

    /// <summary>
    /// Вариант «с исключением» (задание 1.1): бросает <see cref="ResumeValidationException"/>,
    /// если в резюме есть ошибки (предупреждения не мешают).
    /// </summary>
    public void EnsureValid(Resume resume)
    {
        var report = Validate(resume);
        if (report.HasErrors) throw new ResumeValidationException(report);
    }

    private void AddItemIssues(List<ValidationIssue> issues, IEnumerable<ResumeItem> items, string section, DateTime today)
    {
        var index = 0;
        foreach (var item in items)
        {
            foreach (var issue in item.Validate($"{section}[{index}]", today))
                issues.Add(issue with { Message = localizer[issue.Message] });
            index++;
        }
    }

    private ValidationIssue Issue(string field, string key, ValidationSeverity severity = ValidationSeverity.Error) =>
        new(field, localizer[key], severity);
}

/// <summary>
/// <b>Модуль 2 — декларативная валидация.</b> Использует стандартный <see cref="Validator"/>
/// и атрибуты DataAnnotations, расставленные на свойствах моделей.
/// </summary>
public sealed class AnnotationResumeValidator(ILocalizer localizer) : IResumeValidator
{
    /// <inheritdoc />
    public ValidationReport Validate(Resume resume)
    {
        ArgumentNullException.ThrowIfNull(resume);
        var issues = new List<ValidationIssue>();

        ValidateObject(resume.Personal, "Personal", issues);
        for (var i = 0; i < resume.Experience.Count; i++) ValidateObject(resume.Experience[i], $"Experience[{i}]", issues);
        for (var i = 0; i < resume.Education.Count; i++) ValidateObject(resume.Education[i], $"Education[{i}]", issues);
        for (var i = 0; i < resume.Skills.Count; i++) ValidateObject(resume.Skills[i], $"Skills[{i}]", issues);
        for (var i = 0; i < resume.Languages.Count; i++) ValidateObject(resume.Languages[i], $"Languages[{i}]", issues);

        return new ValidationReport(issues);
    }

    /// <summary>Проверяет один объект и переводит сообщения (в атрибутах записаны ключи ресурсов).</summary>
    public IReadOnlyList<ValidationIssue> ValidateObject(object target, string path, List<ValidationIssue>? sink = null)
    {
        sink ??= [];
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(target, new ValidationContext(target), results, validateAllProperties: true);

        foreach (var r in results)
        {
            var member = r.MemberNames.FirstOrDefault() ?? string.Empty;
            var key = r.ErrorMessage ?? string.Empty;
            sink.Add(new ValidationIssue($"{path}.{member}", localizer[key]));
        }
        return sink;
    }
}

/// <summary>Объединяет несколько валидаторов и убирает дубли сообщений (паттерн «Компоновщик»).</summary>
public sealed class CompositeResumeValidator(params IResumeValidator[] validators) : IResumeValidator
{
    /// <inheritdoc />
    public ValidationReport Validate(Resume resume)
    {
        var all = validators.SelectMany(v => v.Validate(resume).Issues)
            .DistinctBy(i => (i.Field, i.Message))
            .ToList();
        return new ValidationReport(all);
    }
}

/// <summary>
/// Структурированный отчёт о валидации (модуль 4.3): все нарушения, сгруппированные по полям и серьёзности.
/// </summary>
public sealed class ValidationReport(IReadOnlyList<ValidationIssue> issues)
{
    /// <summary>Все нарушения.</summary>
    public IReadOnlyList<ValidationIssue> Issues { get; } = issues;

    /// <summary>Только ошибки.</summary>
    public IEnumerable<ValidationIssue> Errors => Issues.Where(i => i.Severity == ValidationSeverity.Error);

    /// <summary>Только предупреждения.</summary>
    public IEnumerable<ValidationIssue> Warnings => Issues.Where(i => i.Severity == ValidationSeverity.Warning);

    /// <summary>Есть ли ошибки.</summary>
    public bool HasErrors => Errors.Any();

    /// <summary>Резюме полностью корректно (нет ни ошибок, ни предупреждений).</summary>
    public bool IsPerfect => Issues.Count == 0;

    /// <summary>Нарушения, сгруппированные по полю.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ValidationIssue>> ByField() =>
        Issues.GroupBy(i => i.Field)
              .ToDictionary(g => g.Key, g => (IReadOnlyList<ValidationIssue>)g.ToList());

    /// <summary>Текстовое представление для консоли и журнала.</summary>
    public override string ToString() =>
        IsPerfect ? "OK" : string.Join(Environment.NewLine, Issues.OrderBy(i => i.Severity).Select(i => i.ToString()));
}
