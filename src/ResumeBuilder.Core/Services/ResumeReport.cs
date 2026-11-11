using System.Text;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Resources;

namespace ResumeBuilder.Core.Services;

/// <summary>Строка отчёта по одному месту работы.</summary>
public class ReportRow
{
    public string Company { get; set; } = "";
    public string Position { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime? End { get; set; }
    public int Months { get; set; }
    /// <summary>Доля от общего стажа, %.</summary>
    public double SharePercent { get; set; }
}

/// <summary>
/// Посчитанные данные для сводного отчёта.
/// Считаются ОДИН раз в ReportCalculator, а потом выводятся и в Excel, и в Word, и в txt —
/// так код подсчёта не дублируется.
/// </summary>
public class ResumeReport
{
    public string FullName { get; set; } = "";
    public string Position { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public int JobsCount { get; set; }
    public int TotalMonths { get; set; }
    public double AverageMonths { get; set; }
    public string LongestJob { get; set; } = "-";

    public int EducationCount { get; set; }

    public int SkillsCount { get; set; }
    public double AverageSkillLevel { get; set; }
    public string BestSkill { get; set; } = "-";
    /// <summary>Сколько навыков на каждом уровне: индекс 0 — уровень 1, ..., индекс 4 — уровень 5.</summary>
    public int[] SkillsByLevel { get; set; } = new int[5];

    /// <summary>Заполненность резюме, %.</summary>
    public int CompletenessPercent { get; set; }

    public List<ReportRow> Rows { get; set; } = new();
    public List<Skill> Skills { get; set; } = new();
    public string Conclusion { get; set; } = "";

    /// <summary>Показатели парами «название — значение» (одинаково для всех форматов).</summary>
    public List<(string Name, string Value)> GetSummary() => new()
    {
        (Messages.Report_JobsCount, JobsCount.ToString()),
        (Messages.Report_TotalExperience, Resume.FormatMonths(TotalMonths)),
        (Messages.Report_AverageMonths, AverageMonths.ToString("0.0")),
        (Messages.Report_LongestJob, LongestJob),
        (Messages.Report_EducationCount, EducationCount.ToString()),
        (Messages.Report_SkillsCount, SkillsCount.ToString()),
        (Messages.Report_AverageSkill, AverageSkillLevel.ToString("0.00")),
        (Messages.Report_BestSkill, BestSkill),
        (Messages.Report_Completeness, CompletenessPercent + " %")
    };

    /// <summary>Отчёт в виде текстовой таблицы — для .txt и печати.</summary>
    public string ToText()
    {
        var sb = new StringBuilder();
        string line = new string('=', 72);
        sb.AppendLine(line);
        sb.AppendLine(Messages.Report_Title.ToUpper());
        sb.AppendLine($"{FullName} — {Position}");
        sb.AppendLine($"{Messages.Report_Created}: {CreatedAt:g}");
        sb.AppendLine(line);
        foreach (var (name, value) in GetSummary())
            sb.AppendLine($"{name,-40} {value}");

        sb.AppendLine();
        sb.AppendLine(Messages.Section_Experience.ToUpper());
        sb.AppendLine(new string('-', 72));
        sb.AppendLine($"{Messages.Column_Company,-22} {Messages.Column_Position,-22} {Messages.Column_Months,8} {Messages.Column_Share,12}");
        sb.AppendLine(new string('-', 72));
        foreach (ReportRow r in Rows)
            sb.AppendLine($"{Cut(r.Company, 22),-22} {Cut(r.Position, 22),-22} {r.Months,8} {r.SharePercent,11:0.0}%");
        sb.AppendLine(new string('-', 72));
        sb.AppendLine($"{Messages.Report_Total,-45} {TotalMonths,8} {100,11:0.0}%");

        sb.AppendLine();
        sb.AppendLine(Messages.Section_Skills.ToUpper());
        foreach (Skill s in Skills)
            sb.AppendLine($"  {Cut(s.Name, 30),-30} {SkillBar(s.Level)}  {s.Level}/5");

        sb.AppendLine();
        sb.AppendLine(Messages.Report_Conclusion.ToUpper());
        sb.AppendLine(Conclusion);
        sb.AppendLine(line);
        return sb.ToString();
    }

    /// <summary>Уровень навыка значками: 3 → ●●●○○</summary>
    public static string SkillBar(int level)
    {
        level = Math.Clamp(level, 0, 5);
        return new string('●', level) + new string('○', 5 - level);
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";
}

/// <summary>Подсчёт статистики по резюме (агрегация: сумма, среднее, максимум, доли).</summary>
public static class ReportCalculator
{
    public static ResumeReport Calculate(Resume resume, DateTime today)
    {
        var report = new ResumeReport
        {
            FullName = resume.Personal.FullName,
            Position = resume.Personal.Position,
            CreatedAt = DateTime.Now
        };

        // Опыт работы
        int total = resume.GetTotalMonths(today);
        foreach (WorkExperience e in resume.Experience.OrderByDescending(x => x.StartDate))
        {
            int months = e.GetMonths(today);
            report.Rows.Add(new ReportRow
            {
                Company = e.Company,
                Position = e.Position,
                Start = e.StartDate,
                End = e.EndDate,
                Months = months,
                SharePercent = total == 0 ? 0 : Math.Round(100.0 * months / total, 1)
            });
        }
        report.JobsCount = report.Rows.Count;
        report.TotalMonths = total;
        report.AverageMonths = report.Rows.Count > 0 ? report.Rows.Average(r => r.Months) : 0;
        ReportRow? longest = report.Rows.OrderByDescending(r => r.Months).FirstOrDefault();
        if (longest != null)
            report.LongestJob = $"{longest.Company} ({longest.Months})";

        report.EducationCount = resume.Education.Count;

        // Навыки
        report.Skills = resume.Skills.OrderByDescending(s => s.Level).ThenBy(s => s.Name).ToList();
        report.SkillsCount = resume.Skills.Count;
        report.AverageSkillLevel = resume.Skills.Count > 0 ? resume.Skills.Average(s => s.Level) : 0;
        report.BestSkill = report.Skills.FirstOrDefault()?.Name ?? "-";
        foreach (Skill s in resume.Skills)
            if (s.Level >= 1 && s.Level <= 5)
                report.SkillsByLevel[s.Level - 1]++;

        report.CompletenessPercent = CalculateCompleteness(resume);
        report.Conclusion = MakeConclusion(report);
        return report;
    }

    /// <summary>Сколько процентов основных полей заполнено.</summary>
    public static int CalculateCompleteness(Resume r)
    {
        bool[] filled =
        {
            r.Personal.FullName != "",
            r.Personal.Position != "",
            r.Personal.Email != "",
            r.Personal.Phone != "",
            r.Personal.City != "",
            r.Personal.BirthDate != null,
            !string.IsNullOrEmpty(r.Personal.Website),
            r.About != "",
            r.Experience.Count > 0,
            r.Education.Count > 0,
            r.Skills.Count > 0
        };
        return (int)Math.Round(100.0 * filled.Count(f => f) / filled.Length);
    }

    /// <summary>Короткий вывод по резюме.</summary>
    private static string MakeConclusion(ResumeReport r)
    {
        string experience = r.TotalMonths < 12 ? Messages.Conclusion_Junior
            : r.TotalMonths < 36 ? Messages.Conclusion_Middle
            : Messages.Conclusion_Senior;
        string completeness = r.CompletenessPercent == 100
            ? Messages.Conclusion_Complete
            : string.Format(Messages.Conclusion_Incomplete, r.CompletenessPercent);
        return experience + " " + completeness;
    }
}
