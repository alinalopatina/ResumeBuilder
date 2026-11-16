using System.ComponentModel.DataAnnotations;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Resources;
using ResumeBuilder.Core.Services;

namespace ResumeBuilder.Core.Validation;

/// <summary>
/// Проверка резюме двумя способами:
/// 1) ручные проверки обычными if (ValidateManual);
/// 2) атрибуты DataAnnotations и стандартный Validator (ValidateByAttributes).
/// </summary>
public static class ResumeValidator
{
    /// <summary>Способ 1: ручные проверки.</summary>
    /// <param name="resume">Проверяемое резюме.</param>
    /// <returns>Список текстов ошибок; пустой, если ошибок нет.</returns>
    /// <exception cref="ArgumentNullException">Если <paramref name="resume"/> равно null.</exception>
    public static List<string> ValidateManual(Resume resume)
    {
        ArgumentNullException.ThrowIfNull(resume);
        var errors = new List<string>();
        PersonalInfo p = resume.Personal;

        if (string.IsNullOrWhiteSpace(p.FullName))
            errors.Add(Messages.Error_FullNameRequired);

        if (string.IsNullOrWhiteSpace(p.Email))
            errors.Add(Messages.Error_EmailRequired);
        else if (!TextHelper.IsValidEmail(p.Email))
            errors.Add(Messages.Error_EmailFormat);

        if (!string.IsNullOrWhiteSpace(p.Phone) && !TextHelper.IsValidPhone(p.Phone))
            errors.Add(Messages.Error_PhoneFormat);

        int? age = p.GetAge(DateTime.Today);
        if (age != null && (age < 14 || age > 100))
            errors.Add(Messages.Error_Age);

        if (resume.Experience.Count == 0 && resume.Education.Count == 0)
            errors.Add(Messages.Error_NoExperience);

        // Один и тот же навык не должен повторяться (добавлено по TDD, см. ValidationTests)
        var duplicates = resume.Skills
            .Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .GroupBy(s => s.Name.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1);
        foreach (var group in duplicates)
            errors.Add(string.Format(Messages.Error_DuplicateSkill, group.First().Name.Trim()));

        // Каждый элемент проверяет себя сам (полиморфный вызов Validate)
        foreach (ResumeItem item in resume.GetAllItems())
            item.Validate(errors);

        return errors;
    }

    /// <summary>Способ 2: проверка по атрибутам [Required], [EmailAddress], [Range], [BelarusPhone]...</summary>
    public static List<string> ValidateByAttributes(Resume resume)
    {
        ArgumentNullException.ThrowIfNull(resume);
        var errors = new List<string>();
        Check(resume.Personal, errors);
        foreach (Skill skill in resume.Skills)
            Check(skill, errors);
        return errors;
    }

    /// <summary>Оба способа вместе, без повторов.</summary>
    public static List<string> Validate(Resume resume) =>
        ValidateByAttributes(resume).Concat(ValidateManual(resume)).Distinct().ToList();

    private static void Check(object obj, List<string> errors)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(obj, new ValidationContext(obj), results, validateAllProperties: true);
        foreach (ValidationResult r in results)
            errors.Add(r.ErrorMessage ?? "");
    }
}
