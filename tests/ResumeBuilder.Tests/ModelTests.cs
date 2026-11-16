using System.Globalization;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Resources;
using Xunit;

namespace ResumeBuilder.Tests;

/// <summary>Модели предметной области: расчёт стажа, возраста, описание, полиморфизм.</summary>
[Collection("Messages")]
public class ModelTests
{
    private static readonly DateTime Today = new(2026, 11, 10);

    public ModelTests()
    {
        Messages.Culture = new CultureInfo("ru-RU");
    }

    [Theory]
    [InlineData("2024-01-01", "2024-07-01", 6)]
    [InlineData("2024-01-15", "2025-01-10", 12)]
    [InlineData("2024-05-01", "2024-03-01", 0)]   // ошибка в датах — стаж не отрицательный
    public void GetMonths_CountsWholeMonths(string start, string end, int expected)
    {
        var job = new WorkExperience { StartDate = DateTime.Parse(start), EndDate = DateTime.Parse(end) };
        Assert.Equal(expected, job.GetMonths(Today));
    }

    [Fact]
    public void GetMonths_CurrentJob_CountsToToday()
    {
        var job = new WorkExperience { StartDate = new DateTime(2025, 11, 1), EndDate = null };
        Assert.Equal(12, job.GetMonths(Today));
    }

    [Fact]
    public void GetDescription_CurrentJob_ShowsPresent()
    {
        var job = new WorkExperience { Company = "EPAM", Position = "Стажёр", StartDate = new DateTime(2025, 7, 1) };
        string text = job.GetDescription();
        Assert.Contains("EPAM", text);
        Assert.Contains(Messages.Present, text);
    }

    [Theory]
    [InlineData("2026-03-13", 20)]   // день рождения ещё не наступил
    [InlineData("2026-03-14", 21)]   // день рождения сегодня
    public void GetAge_TakesBirthdayIntoAccount(string today, int expected)
    {
        var person = new PersonalInfo { BirthDate = new DateTime(2005, 3, 14) };
        Assert.Equal(expected, person.GetAge(DateTime.Parse(today)));
    }

    [Fact]
    public void GetAge_NoBirthDate_IsNull()
    {
        Assert.Null(new PersonalInfo().GetAge(Today));
    }

    [Fact]
    public void Education_EndBeforeStart_IsError()
    {
        var ed = new Education { Institution = "БНТУ", StartYear = 2024, EndYear = 2020 };
        var errors = new List<string>();
        ed.Validate(errors);
        Assert.Single(errors);
    }

    [Fact]
    public void Resume_TotalMonths_And_Format()
    {
        Resume resume = Resume.CreateSample();
        int months = resume.GetTotalMonths(Today);
        Assert.Equal(18, months);
        Assert.Equal("1 г. 6 мес.", Resume.FormatMonths(months));
    }

    [Fact]
    public void GetAllItems_Polymorphism()
    {
        Resume resume = Resume.CreateSample();
        List<ResumeItem> items = resume.GetAllItems();

        // 2 места работы + 1 учебное заведение + 4 навыка
        Assert.Equal(7, items.Count);
        // Через базовый тип вызывается своя реализация GetDescription у каждого наследника
        Assert.All(items, item => Assert.False(string.IsNullOrWhiteSpace(item.GetDescription())));
        Assert.Contains(items, item => item is Skill);
        Assert.Contains(items, item => item is Education);
    }
}
