using System.Globalization;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Resources;
using ResumeBuilder.Core.Services;
using Xunit;

namespace ResumeBuilder.Tests;

/// <summary>Подсчёт статистики для сводного отчёта.</summary>
[Collection("Messages")]
public class ReportTests
{
    private static readonly DateTime Today = new(2026, 11, 10);
    private readonly Resume _sample;

    /// <summary>Перед каждым тестом: русский язык и новое резюме-пример.</summary>
    public ReportTests()
    {
        Messages.Culture = new CultureInfo("ru-RU");
        _sample = Resume.CreateSample();
    }

    [Fact]
    public void Calculate_SampleResume()
    {
        ResumeReport report = ReportCalculator.Calculate(_sample, Today);

        // EPAM: 07.2025–09.2025 = 2 мес., фриланс: 02.2024–06.2025 = 16 мес.
        Assert.Equal(2, report.JobsCount);
        Assert.Equal(18, report.TotalMonths);
        Assert.Equal(9.0, report.AverageMonths);
        Assert.Equal(3.75, report.AverageSkillLevel);
        Assert.Equal(88.9, report.Rows.Single(r => r.Company == "Фриланс").SharePercent);
        Assert.Equal(3, report.SkillsByLevel[3]);   // три навыка на уровне 4
        Assert.Equal(100, report.CompletenessPercent);
    }

    [Fact]
    public void Shares_SumTo100Percent()
    {
        ResumeReport report = ReportCalculator.Calculate(_sample, Today);
        Assert.Equal(100.0, report.Rows.Sum(r => r.SharePercent), precision: 1);
    }

    [Fact]
    public void EmptyResume_NoDivisionByZero()
    {
        ResumeReport report = ReportCalculator.Calculate(new Resume(), Today);

        Assert.Equal(0, report.JobsCount);
        Assert.Equal(0, report.AverageMonths);
        Assert.Equal(0, report.AverageSkillLevel);
        Assert.Equal("-", report.BestSkill);
        Assert.True(report.CompletenessPercent < 100);
    }

    [Fact]
    public void Conclusion_DependsOnExperience()
    {
        ResumeReport junior = ReportCalculator.Calculate(_sample, Today);   // 18 мес.
        Assert.StartsWith(Messages.Conclusion_Middle, junior.Conclusion);

        _sample.Experience.Clear();
        ResumeReport empty = ReportCalculator.Calculate(_sample, Today);
        Assert.StartsWith(Messages.Conclusion_Junior, empty.Conclusion);
    }

    [Fact]
    public void ToText_ContainsNameAndTable()
    {
        string text = ReportCalculator.Calculate(_sample, Today).ToText();

        Assert.Contains(_sample.Personal.FullName, text);
        Assert.Contains("EPAM Systems", text);
        Assert.Contains(Messages.Report_Total, text);
    }
}
