using System.Text.Json.Serialization;

namespace ResumeBuilder.Core.Models;

/// <summary>Резюме целиком.</summary>
public class Resume
{
    /// <summary>Версия формата файла (на случай, если структура класса поменяется).</summary>
    public int Version { get; set; } = 1;

    public PersonalInfo Personal { get; set; } = new();
    public string About { get; set; } = "";
    public List<WorkExperience> Experience { get; set; } = new();
    public List<Education> Education { get; set; } = new();
    public List<Skill> Skills { get; set; } = new();

    /// <summary>Путь к файлу нужен только пока программа работает, поэтому не сохраняется.</summary>
    [JsonIgnore]
    public string? FilePath { get; set; }

    /// <summary>Общий стаж в месяцах (сумма по всем местам работы).</summary>
    public int GetTotalMonths(DateTime today) => Experience.Sum(e => e.GetMonths(today));

    /// <summary>Стаж в виде текста: «1 г. 6 мес.» / «1 y. 6 mo.».</summary>
    public static string FormatMonths(int months) =>
        string.Format(Resources.Messages.Format_YearsMonths, months / 12, months % 12);

    /// <summary>Все элементы резюме через базовый класс.</summary>
    public List<ResumeItem> GetAllItems()
    {
        var items = new List<ResumeItem>();
        items.AddRange(Experience);
        items.AddRange(Education);
        items.AddRange(Skills);
        return items;
    }

    /// <summary>Пример заполненного резюме для первого запуска.</summary>
    public static Resume CreateSample()
    {
        var r = new Resume();
        r.Personal.FullName = "Иванова Мария Сергеевна";
        r.Personal.Position = "Junior .NET Developer";
        r.Personal.Email = "maria.ivanova@example.com";
        r.Personal.Phone = "+375 (29) 123-45-67";
        r.Personal.City = "Минск";
        r.Personal.BirthDate = new DateTime(2005, 3, 14);
        r.Personal.Website = "https://github.com/maria-ivanova";
        r.About = "Студентка БНТУ. Пишу настольные приложения на C# и WPF, работаю с Git.";
        r.Experience.Add(new WorkExperience
        {
            Company = "EPAM Systems", Position = "Стажёр .NET",
            StartDate = new DateTime(2025, 7, 1), EndDate = new DateTime(2025, 9, 30),
            Description = "Разработка учебного сервиса на ASP.NET Core, unit-тесты."
        });
        r.Experience.Add(new WorkExperience
        {
            Company = "Фриланс", Position = "Разработчик WPF",
            StartDate = new DateTime(2024, 2, 1), EndDate = new DateTime(2025, 6, 30),
            Description = "Небольшие программы учёта для малого бизнеса."
        });
        r.Education.Add(new Education
        {
            Institution = "БНТУ", Specialty = "Программное обеспечение информационных систем и технологий",
            StartYear = 2024, EndYear = 2028
        });
        r.Skills.Add(new Skill { Name = "C#", Level = 4 });
        r.Skills.Add(new Skill { Name = "WPF", Level = 4 });
        r.Skills.Add(new Skill { Name = "SQL", Level = 3 });
        r.Skills.Add(new Skill { Name = "Git", Level = 4 });
        return r;
    }
}
