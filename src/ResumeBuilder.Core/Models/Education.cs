using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ResumeBuilder.Core.Validation;

namespace ResumeBuilder.Core.Models;

/// <summary>Запись об образовании.</summary>
public class Education : ResumeItem
{
    /// <summary>Учебное заведение.</summary>
    [Required(ErrorMessage = "Validation_InstitutionRequired")]
    public string Institution { get; set; } = string.Empty;

    /// <summary>Степень / уровень (бакалавр, магистр, курсы).</summary>
    public string? Degree { get; set; }

    /// <summary>Специальность.</summary>
    public string? Specialty { get; set; }

    /// <summary>Год поступления.</summary>
    [Range(1950, 2100, ErrorMessage = "Validation_YearRange")]
    public int StartYear { get; set; } = DateTime.Today.Year - 4;

    /// <summary>Год окончания; <c>null</c> — учусь сейчас.</summary>
    [Range(1950, 2100, ErrorMessage = "Validation_YearRange")]
    public int? EndYear { get; set; }

    /// <inheritdoc />
    public override string SectionKey => "Section_Education";

    /// <inheritdoc />
    public override string GetSummary(CultureInfo culture)
    {
        var years = EndYear is null ? $"{StartYear}–…" : $"{StartYear}–{EndYear}";
        var specialty = string.IsNullOrWhiteSpace(Specialty) ? string.Empty : $", {Specialty}";
        return $"{Institution}{specialty} ({years})";
    }

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Validate(string path, DateTime today)
    {
        if (EndYear is { } end && end < StartYear)
            yield return new ValidationIssue($"{path}.EndYear", "Validation_EndBeforeStart");

        if (StartYear > today.Year)
            yield return new ValidationIssue($"{path}.StartYear", "Validation_StartInFuture");
    }
}
