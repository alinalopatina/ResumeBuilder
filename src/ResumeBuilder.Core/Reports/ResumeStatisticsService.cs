using ResumeBuilder.Core.Models;

namespace ResumeBuilder.Core.Reports;

/// <summary>Статистика по категории навыков.</summary>
public sealed record SkillCategoryStat(SkillCategory Category, int Count, double AverageLevel, int MaxLevel);

/// <summary>Статистика по одному месту работы.</summary>
public sealed record JobStat(string Company, string Position, DateTime Start, DateTime? End, int Months, double SharePercent);

/// <summary>Агрегированные (посчитанные) показатели резюме. Только числа — никакого оформления.</summary>
public sealed class ResumeStatistics
{
    public required string CandidateName { get; init; }
    public required string DesiredPosition { get; init; }
    public DateTime CalculatedAt { get; init; }

    public int JobsCount { get; init; }
    public int TotalExperienceMonths { get; init; }
    public double AverageJobMonths { get; init; }
    public JobStat? LongestJob { get; init; }
    public IReadOnlyList<JobStat> Jobs { get; init; } = [];

    public int SkillsCount { get; init; }
    public double AverageSkillLevel { get; init; }
    public IReadOnlyList<SkillCategoryStat> SkillsByCategory { get; init; } = [];
    public IReadOnlyList<string> TopSkills { get; init; } = [];

    public int EducationCount { get; init; }
    public int LanguagesCount { get; init; }
    public int LanguagesB2OrHigher { get; init; }

    /// <summary>Заполненность резюме в процентах (0–100).</summary>
    public int CompletenessPercent { get; init; }
}

/// <summary>
/// <b>Агрегация данных</b> для сводного отчёта (лаба «Excel/Word», модуль 3; лаба «Печать» — этап «Анализ и вычисления»).
/// Не знает ни про Excel, ни про Word — только считает.
/// </summary>
public sealed class ResumeStatisticsService(Func<DateTime>? clock = null)
{
    private readonly Func<DateTime> _clock = clock ?? (() => DateTime.Today);

    /// <summary>Считает статистику по резюме.</summary>
    public ResumeStatistics Calculate(Resume resume)
    {
        ArgumentNullException.ThrowIfNull(resume);
        var today = _clock();

        var jobsRaw = resume.Experience
            .Select(e => (e, months: e.GetDurationMonths(today)))
            .OrderByDescending(x => x.e.StartDate)
            .ToList();
        var total = jobsRaw.Sum(x => x.months);
        var jobs = jobsRaw
            .Select(x => new JobStat(x.e.Company, x.e.Position, x.e.StartDate, x.e.EndDate, x.months,
                total == 0 ? 0 : Math.Round(100.0 * x.months / total, 1)))
            .ToList();

        var byCategory = resume.Skills
            .GroupBy(s => s.Category)
            .OrderBy(g => g.Key)
            .Select(g => new SkillCategoryStat(g.Key, g.Count(), Math.Round(g.Average(s => s.Level), 2), g.Max(s => s.Level)))
            .ToList();

        return new ResumeStatistics
        {
            CandidateName = resume.Personal.FullName,
            DesiredPosition = resume.Personal.DesiredPosition,
            CalculatedAt = today,
            JobsCount = jobs.Count,
            TotalExperienceMonths = total,
            AverageJobMonths = jobs.Count == 0 ? 0 : Math.Round(jobs.Average(j => j.Months), 1),
            LongestJob = jobs.MaxBy(j => j.Months),
            Jobs = jobs,
            SkillsCount = resume.Skills.Count,
            AverageSkillLevel = resume.Skills.Count == 0 ? 0 : Math.Round(resume.Skills.Average(s => s.Level), 2),
            SkillsByCategory = byCategory,
            TopSkills = resume.Skills.OrderByDescending(s => s.Level).ThenBy(s => s.Name).Take(5).Select(s => s.Name).ToList(),
            EducationCount = resume.Education.Count,
            LanguagesCount = resume.Languages.Count,
            LanguagesB2OrHigher = resume.Languages.Count(l => l.Level >= LanguageLevel.B2),
            CompletenessPercent = CalculateCompleteness(resume)
        };
    }

    /// <summary>Процент заполненности: доля заполненных ключевых полей и разделов.</summary>
    public static int CalculateCompleteness(Resume r)
    {
        bool[] checks =
        [
            !string.IsNullOrWhiteSpace(r.Personal.FullName),
            !string.IsNullOrWhiteSpace(r.Personal.DesiredPosition),
            !string.IsNullOrWhiteSpace(r.Personal.Email),
            !string.IsNullOrWhiteSpace(r.Personal.Phone),
            !string.IsNullOrWhiteSpace(r.Personal.City),
            !string.IsNullOrWhiteSpace(r.Personal.Website),
            r.Personal.BirthDate is not null,
            !string.IsNullOrWhiteSpace(r.Summary),
            r.Experience.Count > 0,
            r.Education.Count > 0,
            r.Skills.Count >= 3,
            r.Languages.Count > 0
        ];
        return (int)Math.Round(100.0 * checks.Count(c => c) / checks.Length);
    }
}
