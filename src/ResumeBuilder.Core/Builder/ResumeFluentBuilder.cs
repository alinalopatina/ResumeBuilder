using ResumeBuilder.Core.Models;

namespace ResumeBuilder.Core.Builder;

/// <summary>
/// Паттерн <b>«Строитель» (Builder)</b> с текучим интерфейсом — пошаговое конструирование резюме.
/// Используется в тестах, демо-данных и консольной утилите.
/// </summary>
/// <example>
/// <code>
/// var resume = new ResumeFluentBuilder()
///     .WithName("Лопатина Алина Николаевна")
///     .WithPosition("Junior .NET Developer")
///     .WithContacts("alina@example.com", "+375 29 123-45-67")
///     .AddSkill("C#", 4)
///     .Build();
/// </code>
/// </example>
public sealed class ResumeFluentBuilder
{
    private readonly Resume _resume = new();

    public ResumeFluentBuilder WithName(string fullName)
    {
        _resume.Personal.FullName = fullName;
        return this;
    }

    public ResumeFluentBuilder WithPosition(string position)
    {
        _resume.Personal.DesiredPosition = position;
        return this;
    }

    public ResumeFluentBuilder WithContacts(string email, string? phone = null, string? website = null, string? city = null)
    {
        _resume.Personal.Email = email;
        _resume.Personal.Phone = phone;
        _resume.Personal.Website = website;
        _resume.Personal.City = city;
        return this;
    }

    public ResumeFluentBuilder WithBirthDate(DateTime birthDate)
    {
        _resume.Personal.BirthDate = birthDate;
        return this;
    }

    public ResumeFluentBuilder WithSalary(decimal amount, string currency = "BYN")
    {
        _resume.Personal.DesiredSalary = amount;
        _resume.Personal.SalaryCurrency = currency;
        return this;
    }

    public ResumeFluentBuilder WithSummary(string summary)
    {
        _resume.Summary = summary;
        return this;
    }

    public ResumeFluentBuilder AddExperience(string company, string position, DateTime start, DateTime? end = null,
        string? description = null, string? city = null)
    {
        _resume.Experience.Add(new WorkExperience
        {
            Company = company,
            Position = position,
            StartDate = start,
            EndDate = end,
            Description = description,
            City = city
        });
        return this;
    }

    public ResumeFluentBuilder AddEducation(string institution, string specialty, int startYear, int? endYear = null, string? degree = null)
    {
        _resume.Education.Add(new Education
        {
            Institution = institution,
            Specialty = specialty,
            StartYear = startYear,
            EndYear = endYear,
            Degree = degree
        });
        return this;
    }

    public ResumeFluentBuilder AddSkill(string name, int level, SkillCategory category = SkillCategory.Programming)
    {
        _resume.Skills.Add(new Skill { Name = name, Level = level, Category = category });
        return this;
    }

    public ResumeFluentBuilder AddLanguage(string language, LanguageLevel level)
    {
        _resume.Languages.Add(new LanguageSkill { Language = language, Level = level });
        return this;
    }

    public ResumeFluentBuilder UseTemplate(ResumeTemplate template)
    {
        _resume.Template = template;
        return this;
    }

    public ResumeFluentBuilder InCulture(string cultureName)
    {
        _resume.CultureName = cultureName;
        return this;
    }

    /// <summary>Возвращает готовое резюме.</summary>
    public Resume Build()
    {
        _resume.UpdatedAt = DateTime.Now;
        return _resume;
    }
}
