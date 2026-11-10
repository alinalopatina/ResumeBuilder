using System.Text;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Resources;

namespace ResumeBuilder.Core.Services;

/// <summary>Строка таблицы отчёта: одно место работы.</summary>
public class ReportRow
{
    public string Company { get; set; } = "";
    public string Position { get; set; } = "";
    public int Months { get; set; }
}

/// <summary>
/// Посчитанные данные для сводного отчёта.
/// Считаются ОДИН раз в ReportCalculator, а потом выводятся и в Excel, и в Word —
/// так код подсчёта не дублируется.
/// </summary>
public class ResumeReport
{
    public string FullName { get; set; } = "";
    public int JobsCount { get; set; }
    public int TotalMonths { get; set; }
    public double AverageMonths { get; set; }
    public int SkillsCount { get; set; }
    public double AverageSkillLevel { get; set; }
    public string BestSkill { get; set; } = "";
    public List<ReportRow> Rows { get; set; } = new();

    /// <summary>Показатели парами «название — значение» (одинаково для всех форматов).</summary>
    public List<(string Name, string Value)> GetSummary() => new()
    {
        (Messages.Report_JobsCount, JobsCount.ToString()),
        (Messages.Report_TotalMonths, TotalMonths.ToString()),
        (Messages.Report_AverageMonths, AverageMonths.ToString("0.0")),
        (Messages.Report_SkillsCount, SkillsCount.ToString()),
        (Messages.Report_AverageSkill, AverageSkillLevel.ToString("0.00")),
        (Messages.Report_BestSkill, BestSkill)
    };

    /// <summary>Отчёт в виде текстовой таблицы — для консоли, .txt и печати.</summary>
    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Messages.Report_Title.ToUpper());
        sb.AppendLine(FullName);
        sb.AppendLine(new string('=', 60));
        foreach (var (name, value) in GetSummary())
            sb.AppendLine($"{name,-35} {value}");
        sb.AppendLine(new string('-', 60));
        sb.AppendLine($"{Messages.Column_Company,-25} {Messages.Column_Position,-25} {Messages.Column_Months,8}");
        sb.AppendLine(new string('-', 60));
        foreach (ReportRow r in Rows)
            sb.AppendLine($"{Cut(r.Company, 25),-25} {Cut(r.Position, 25),-25} {r.Months,8}");
        sb.AppendLine(new string('=', 60));
        return sb.ToString();
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";
}

/// <summary>Подсчёт статистики по резюме (агрегация: сумма, среднее, максимум).</summary>
public static class ReportCalculator
{
    public static ResumeReport Calculate(Resume resume, DateTime today)
    {
        var report = new ResumeReport { FullName = resume.Personal.FullName };

        foreach (WorkExperience e in resume.Experience.OrderByDescending(x => x.StartDate))
            report.Rows.Add(new ReportRow { Company = e.Company, Position = e.Position, Months = e.GetMonths(today) });

        report.JobsCount = report.Rows.Count;
        report.TotalMonths = report.Rows.Sum(r => r.Months);
        report.AverageMonths = report.Rows.Count > 0 ? report.Rows.Average(r => r.Months) : 0;

        report.SkillsCount = resume.Skills.Count;
        report.AverageSkillLevel = resume.Skills.Count > 0 ? resume.Skills.Average(s => s.Level) : 0;
        report.BestSkill = resume.Skills.OrderByDescending(s => s.Level).FirstOrDefault()?.Name ?? "-";

        return report;
    }
}
