using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ResumeBuilder.Core.Validation;

namespace ResumeBuilder.Core.Models;

/// <summary>Профессиональный навык с оценкой уровня 1–5.</summary>
public class Skill : ResumeItem
{
    /// <summary>Минимальный уровень навыка.</summary>
    public const int MinLevel = 1;

    /// <summary>Максимальный уровень навыка.</summary>
    public const int MaxLevel = 5;

    /// <summary>Название навыка (C#, Git, SQL...).</summary>
    [Required(ErrorMessage = "Validation_SkillNameRequired")]
    [StringLength(60, ErrorMessage = "Validation_TooLong")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Категория.</summary>
    public SkillCategory Category { get; set; } = SkillCategory.Programming;

    /// <summary>Уровень 1–5.</summary>
    [Range(MinLevel, MaxLevel, ErrorMessage = "Validation_SkillLevelRange")]
    public int Level { get; set; } = 3;

    /// <inheritdoc />
    public override string SectionKey => "Section_Skills";

    /// <inheritdoc />
    public override string GetSummary(CultureInfo culture) =>
        $"{Name} {new string('●', Math.Clamp(Level, 0, MaxLevel))}{new string('○', MaxLevel - Math.Clamp(Level, 0, MaxLevel))}";

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Validate(string path, DateTime today)
    {
        if (Level is < MinLevel or > MaxLevel)
            yield return new ValidationIssue($"{path}.Level", "Validation_SkillLevelRange");
    }
}

/// <summary>Владение иностранным языком.</summary>
public class LanguageSkill : ResumeItem
{
    /// <summary>Название языка.</summary>
    [Required(ErrorMessage = "Validation_LanguageRequired")]
    public string Language { get; set; } = string.Empty;

    /// <summary>Уровень по CEFR.</summary>
    public LanguageLevel Level { get; set; } = LanguageLevel.B1;

    /// <inheritdoc />
    public override string SectionKey => "Section_Languages";

    /// <inheritdoc />
    public override string GetSummary(CultureInfo culture) => $"{Language} — {Level}";
}
