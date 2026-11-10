using ResumeBuilder.Core.Models;

namespace ResumeBuilder.Core.Builder;

/// <summary>Демонстрационные данные (для первого запуска, тестов и защиты проекта).</summary>
public static class SampleData
{
    /// <summary>Пример заполненного резюме.</summary>
    public static Resume CreateSampleResume() =>
        new ResumeFluentBuilder()
            .WithName("Иванова Мария Сергеевна")
            .WithPosition("Junior .NET Developer")
            .WithContacts("maria.ivanova@example.com", "+375 (29) 123-45-67", "https://github.com/maria-ivanova", "Минск")
            .WithBirthDate(new DateTime(2005, 3, 14))
            .WithSalary(2500m, "BYN")
            .WithSummary("Студентка 3 курса ФИТР БНТУ. Разрабатываю настольные приложения на C# и WPF, " +
                         "пишу unit-тесты, работаю с Git. Ищу стажировку или позицию Junior-разработчика.")
            .AddExperience("EPAM Systems", "Стажёр .NET", new DateTime(2025, 7, 1), new DateTime(2025, 9, 30),
                "Разработка внутреннего сервиса учёта задач на ASP.NET Core; покрытие кода тестами xUnit.", "Минск")
            .AddExperience("Фриланс", "Разработчик WPF-приложений", new DateTime(2024, 2, 1), new DateTime(2025, 6, 30),
                "Создание настольных приложений для малого бизнеса: учёт заказов, генерация отчётов в Excel.", "Минск")
            .AddExperience("Учебная лаборатория БНТУ", "Ассистент", new DateTime(2025, 10, 1), null,
                "Помощь преподавателям в подготовке лабораторных работ по C#.", "Минск")
            .AddEducation("Белорусский национальный технический университет", "Программное обеспечение информационных систем и технологий",
                2024, 2028, "Бакалавр")
            .AddEducation("Stepik", "Алгоритмы и структуры данных", 2023, 2023, "Онлайн-курс")
            .AddSkill("C#", 4)
            .AddSkill(".NET / WPF", 4)
            .AddSkill("SQL", 3)
            .AddSkill("Git", 4, SkillCategory.Tools)
            .AddSkill("Docker", 2, SkillCategory.Tools)
            .AddSkill("Visual Studio", 4, SkillCategory.Tools)
            .AddSkill("Командная работа", 5, SkillCategory.SoftSkills)
            .AddSkill("Английский для IT", 3, SkillCategory.SoftSkills)
            .AddLanguage("Русский", LanguageLevel.Native)
            .AddLanguage("Белорусский", LanguageLevel.C1)
            .AddLanguage("Английский", LanguageLevel.B2)
            .UseTemplate(ResumeTemplate.Modern)
            .Build();

    /// <summary>
    /// Генерирует большой набор мест работы для замера производительности экспорта
    /// (лаба «Excel/Word», модуль 3 «Оптимизация»).
    /// </summary>
    public static List<WorkExperience> GenerateExperience(int count, int seed = 42)
    {
        var rnd = new Random(seed);
        string[] companies = ["EPAM", "iTechArt", "Wargaming", "Itransition", "ScienceSoft", "IBA Group", "Andersen", "SoftTeco"];
        string[] positions = ["Junior Developer", "Middle Developer", "QA Engineer", "Business Analyst", "DevOps", "Team Lead"];
        var list = new List<WorkExperience>(count);
        for (var i = 0; i < count; i++)
        {
            var start = new DateTime(2000, 1, 1).AddMonths(rnd.Next(0, 300));
            var end = rnd.Next(10) == 0 ? (DateTime?)null : start.AddMonths(rnd.Next(1, 60));
            list.Add(new WorkExperience
            {
                Company = $"{companies[rnd.Next(companies.Length)]} #{i + 1}",
                Position = positions[rnd.Next(positions.Length)],
                City = "Минск",
                StartDate = start,
                EndDate = end,
                Description = $"Задачи проекта №{rnd.Next(1, 1000)}"
            });
        }
        return list;
    }
}
