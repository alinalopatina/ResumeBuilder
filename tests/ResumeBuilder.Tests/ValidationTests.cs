using System.Globalization;
using ResumeBuilder.Core.Builder;
using ResumeBuilder.Core.Localization;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Validation;

namespace ResumeBuilder.Tests;

/// <summary>Валидация: ручные проверки (модуль 1), DataAnnotations (модуль 2), отчёт (модуль 4.3).</summary>
public class ValidationTests
{
    private static readonly DateTime Today = new(2026, 11, 10);
    private readonly ILocalizer _loc = new ResxLocalizer(CultureInfo.GetCultureInfo("ru-RU"));

    private ManualResumeValidator Manual => new(_loc, () => Today);
    private AnnotationResumeValidator Annotations => new(_loc);

    [Fact]
    public void SampleResume_HasNoErrors()
    {
        var resume = SampleData.CreateSampleResume();
        var report = new CompositeResumeValidator(Annotations, Manual).Validate(resume);
        Assert.False(report.HasErrors, report.ToString());
    }

    [Fact]
    public void Manual_EmptyResume_ReportsRequiredFields()
    {
        var report = Manual.Validate(new Resume());
        Assert.Contains(report.Errors, i => i.Field == "Personal.FullName");
        Assert.Contains(report.Errors, i => i.Field == "Personal.Email");
        Assert.Contains(report.Errors, i => i.Field == "Experience");
    }

    [Fact]
    public void Manual_EndBeforeStart_IsError()
    {
        var resume = SampleData.CreateSampleResume();
        resume.Experience[0].EndDate = resume.Experience[0].StartDate.AddMonths(-2);

        var report = Manual.Validate(resume);

        Assert.Contains(report.Errors, i => i.Field == "Experience[0].EndDate" && i.Message == _loc["Validation_EndBeforeStart"]);
    }

    [Fact]
    public void Manual_DuplicateSkills_IsWarning()
    {
        var resume = SampleData.CreateSampleResume();
        resume.Skills.Add(new Skill { Name = "c#", Level = 2 });

        var report = Manual.Validate(resume);

        Assert.Contains(report.Warnings, i => i.Field == "Skills" && i.Message.Contains("c#", StringComparison.OrdinalIgnoreCase));
        Assert.False(report.HasErrors);
    }

    [Fact]
    public void Manual_EnsureValid_ThrowsWithReadableMessage()
    {
        var ex = Assert.Throws<ResumeValidationException>(() => Manual.EnsureValid(new Resume()));
        Assert.Contains(_loc["Validation_FullNameRequired"], ex.Message);
    }

    [Theory]
    [InlineData("not-an-email", "Validation_EmailFormat")]
    [InlineData("a@b", "Validation_EmailFormat")]
    public void Annotations_InvalidEmail_LocalizedMessage(string email, string key)
    {
        var resume = SampleData.CreateSampleResume();
        resume.Personal.Email = email;
        var report = Annotations.Validate(resume);
        Assert.Contains(report.Issues, i => i.Field == "Personal.Email" && i.Message == _loc[key]);
    }

    [Theory]
    [InlineData("12-34")]
    [InlineData("phone: abc")]
    public void Annotations_InvalidPhone(string phone)
    {
        var resume = SampleData.CreateSampleResume();
        resume.Personal.Phone = phone;
        Assert.Contains(Annotations.Validate(resume).Issues, i => i.Field == "Personal.Phone");
    }

    [Fact]
    public void Annotations_CustomAttributes_BirthDateAndName()
    {
        var resume = SampleData.CreateSampleResume();
        resume.Personal.BirthDate = DateTime.Today.AddDays(1);
        resume.Personal.FullName = "R2-D2 <script>";

        var issues = Annotations.Validate(resume).Issues;

        Assert.Contains(issues, i => i.Field == "Personal.BirthDate" && i.Message == _loc["Validation_BirthDateFuture"]);
        Assert.Contains(issues, i => i.Field == "Personal.FullName" && i.Message == _loc["Validation_FullNameFormat"]);
    }

    [Fact]
    public void Annotations_SkillLevelOutOfRange()
    {
        var resume = SampleData.CreateSampleResume();
        resume.Skills[0].Level = 9;
        Assert.Contains(Annotations.Validate(resume).Issues, i => i.Field == "Skills[0].Level");
    }

    [Fact]
    public void Report_GroupsByField()
    {
        var report = Manual.Validate(new Resume());
        var byField = report.ByField();
        Assert.True(byField.ContainsKey("Personal.Email"));
        Assert.All(byField, kv => Assert.All(kv.Value, i => Assert.Equal(kv.Key, i.Field)));
    }

    [Fact]
    public void Messages_AreLocalized_ToEnglish()
    {
        var en = new ResxLocalizer(CultureInfo.GetCultureInfo("en-US"));
        var report = new ManualResumeValidator(en, () => Today).Validate(new Resume());
        Assert.Contains(report.Errors, i => i.Message == "Full name is required");
    }
}
