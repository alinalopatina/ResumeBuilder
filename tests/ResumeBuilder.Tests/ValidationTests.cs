using System.Globalization;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Resources;
using ResumeBuilder.Core.Validation;
using Xunit;

namespace ResumeBuilder.Tests;

/// <summary>
/// Проверка резюме. Перед каждым тестом (конструктор) выставляется русский язык сообщений.
/// </summary>
[Collection("Messages")]
public class ValidationTests
{
    public ValidationTests()
    {
        Messages.Culture = new CultureInfo("ru-RU");
    }

    [Fact]
    public void SampleResume_IsValid()
    {
        Assert.Empty(ResumeValidator.Validate(Resume.CreateSample()));
    }

    [Fact]
    public void EmptyResume_HasErrors()
    {
        List<string> errors = ResumeValidator.ValidateManual(new Resume());
        Assert.Contains(Messages.Error_FullNameRequired, errors);
        Assert.Contains(Messages.Error_EmailRequired, errors);
        Assert.Contains(Messages.Error_NoExperience, errors);
    }

    [Fact]
    public void EndDateBeforeStart_IsError()
    {
        Resume resume = Resume.CreateSample();
        resume.Experience[0].EndDate = resume.Experience[0].StartDate.AddMonths(-1);
        Assert.Contains(ResumeValidator.ValidateManual(resume), e => e.Contains("EPAM"));
    }

    [Fact]
    public void Attributes_FindWrongEmailAndPhone()
    {
        Resume resume = Resume.CreateSample();
        resume.Personal.Email = "not-email";
        resume.Personal.Phone = "123";
        List<string> errors = ResumeValidator.ValidateByAttributes(resume);
        Assert.Contains(Messages.Error_EmailFormat, errors);
        Assert.Contains(Messages.Error_PhoneFormat, errors);
    }

    [Fact]
    public void Attributes_SkillLevelOutOfRange()
    {
        Resume resume = Resume.CreateSample();
        resume.Skills[0].Level = 10;
        Assert.Contains(Messages.Error_SkillLevel, ResumeValidator.ValidateByAttributes(resume));
    }

    [Fact]
    public void Messages_AreTranslated()
    {
        Messages.Culture = new CultureInfo("en-US");
        Assert.Equal("Full name is required", Messages.Error_FullNameRequired);
        Messages.Culture = new CultureInfo("ru-RU");
        Assert.Equal("Укажите ФИО", Messages.Error_FullNameRequired);
    }

    // ---------- Пример TDD: тест написан ДО кода ----------
    // Шаг 1 (красный): тест падал, потому что ValidateManual не проверял повторы навыков.
    // Шаг 2 (зелёный): в ValidateManual добавлена проверка — тест прошёл.
    // Шаг 3 (рефакторинг): сравнение без учёта регистра и пробелов, тесты по-прежнему зелёные.

    [Fact]
    public void DuplicateSkill_IsError()
    {
        Resume resume = Resume.CreateSample();
        resume.Skills.Add(new Skill { Name = " c# ", Level = 2 });   // «C#» уже есть в примере

        List<string> errors = ResumeValidator.ValidateManual(resume);

        Assert.Contains(string.Format(Messages.Error_DuplicateSkill, "C#"), errors);
    }

    [Fact]
    public void DifferentSkills_NoDuplicateError()
    {
        Resume resume = Resume.CreateSample();
        resume.Skills.Add(new Skill { Name = "Docker", Level = 2 });

        List<string> errors = ResumeValidator.ValidateManual(resume);

        Assert.DoesNotContain(errors, e => e.Contains("Docker"));
    }

    [Theory]
    [InlineData(2010, false)]   // 16 лет — можно
    [InlineData(2020, true)]    // 6 лет — ошибка
    [InlineData(1900, true)]    // больше 100 лет — ошибка
    public void Age_IsChecked(int birthYear, bool expectError)
    {
        Resume resume = Resume.CreateSample();
        resume.Personal.BirthDate = new DateTime(birthYear, 1, 1);

        bool hasError = ResumeValidator.ValidateManual(resume).Contains(Messages.Error_Age);

        Assert.Equal(expectError, hasError);
    }
}
