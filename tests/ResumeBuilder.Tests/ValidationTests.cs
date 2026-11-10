using System.Globalization;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Resources;
using ResumeBuilder.Core.Validation;
using Xunit;

namespace ResumeBuilder.Tests;

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
}
