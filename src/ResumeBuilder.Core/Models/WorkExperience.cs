using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Serialization;
using ResumeBuilder.Core.Validation;

namespace ResumeBuilder.Core.Models;

/// <summary>
/// Место работы — основная сущность для экспорта в Excel/Word (лаба «Импорт-экспорт»).
/// </summary>
public class WorkExperience : ResumeItem
{
    /// <summary>Название компании.</summary>
    [Required(ErrorMessage = "Validation_CompanyRequired")]
    [StringLength(150, ErrorMessage = "Validation_TooLong")]
    public string Company { get; set; } = string.Empty;

    /// <summary>Должность.</summary>
    [Required(ErrorMessage = "Validation_PositionRequired")]
    [StringLength(150, ErrorMessage = "Validation_TooLong")]
    public string Position { get; set; } = string.Empty;

    /// <summary>Город.</summary>
    public string? City { get; set; }

    /// <summary>Дата начала работы.</summary>
    public DateTime StartDate { get; set; } = DateTime.Today.AddYears(-1);

    /// <summary>Дата окончания; <c>null</c> — работаю по настоящее время.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Обязанности и достижения.</summary>
    public string? Description { get; set; }

    /// <summary>Работает ли человек здесь сейчас (вычисляемое, не сериализуется).</summary>
    [JsonIgnore]
    public bool IsCurrent => EndDate is null;

    /// <inheritdoc />
    public override string SectionKey => "Section_Experience";

    /// <summary>Длительность работы в полных месяцах (не меньше 1).</summary>
    /// <param name="today">Дата, относительно которой считается «по настоящее время».</param>
    public int GetDurationMonths(DateTime today)
    {
        var end = EndDate ?? today;
        if (end < StartDate) return 0;
        var months = (end.Year - StartDate.Year) * 12 + end.Month - StartDate.Month;
        return Math.Max(1, months);
    }

    /// <inheritdoc />
    public override string GetSummary(CultureInfo culture)
    {
        var end = EndDate?.ToString("MM.yyyy", culture) ?? "…";
        return $"{Position} — {Company} ({StartDate.ToString("MM.yyyy", culture)} – {end})";
    }

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Validate(string path, DateTime today)
    {
        if (StartDate.Date > today.Date)
            yield return new ValidationIssue($"{path}.StartDate", "Validation_StartInFuture");

        if (EndDate is { } end && end.Date < StartDate.Date)
            yield return new ValidationIssue($"{path}.EndDate", "Validation_EndBeforeStart");

        if (EndDate is { } end2 && end2.Date > today.Date)
            yield return new ValidationIssue($"{path}.EndDate", "Validation_EndInFuture", ValidationSeverity.Warning);

        if (string.IsNullOrWhiteSpace(Description))
            yield return new ValidationIssue($"{path}.Description", "Validation_DescriptionEmpty", ValidationSeverity.Warning);
    }
}
