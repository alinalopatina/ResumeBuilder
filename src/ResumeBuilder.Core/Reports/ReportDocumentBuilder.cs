using System.Globalization;
using ResumeBuilder.Core.Localization;

namespace ResumeBuilder.Core.Reports;

/// <summary>
/// Превращает посчитанную статистику в формат-независимый <see cref="ReportDocument"/>
/// на нужном языке. Связующее звено между агрегацией и рендерерами.
/// </summary>
public sealed class ReportDocumentBuilder(ILocalizer loc)
{
    /// <summary>Строит отчёт для указанной культуры.</summary>
    public ReportDocument Build(ResumeStatistics s, CultureInfo culture)
    {
        string T(string key) => loc.Get(key, culture);
        string Dur(int months) => CultureFormatter.FormatDuration(months, loc, culture);

        var doc = new ReportDocument
        {
            Title = T("Report_Title"),
            CultureName = culture.Name,
            Subtitle = $"{s.CandidateName} — {s.DesiredPosition}",
            GeneratedLabel = $"{T("Report_Generated")}: {DateTime.Now.ToString("g", culture)}",
            ConclusionTitle = T("Report_Conclusion"),
            Conclusion = BuildConclusion(s, culture)
        };

        // 1. Общие показатели
        var general = new ReportSection { Heading = T("Report_General") };
        general.Metrics.Add(new(T("Report_TotalExperience"), Dur(s.TotalExperienceMonths)));
        general.Metrics.Add(new(T("Report_JobsCount"), s.JobsCount.ToString(culture)));
        general.Metrics.Add(new(T("Report_AverageJob"), Dur((int)Math.Round(s.AverageJobMonths))));
        if (s.LongestJob is { } longest)
            general.Metrics.Add(new(T("Report_LongestJob"), $"{longest.Company} ({Dur(longest.Months)})"));
        general.Metrics.Add(new(T("Report_SkillsCount"), s.SkillsCount.ToString(culture)));
        general.Metrics.Add(new(T("Report_AverageSkill"), s.AverageSkillLevel.ToString("0.00", culture)));
        general.Metrics.Add(new(T("Report_Languages"), $"{s.LanguagesCount} ({T("Report_B2Plus")}: {s.LanguagesB2OrHigher})"));
        general.Metrics.Add(new(T("Report_Completeness"), (s.CompletenessPercent / 100.0).ToString("P0", culture)));
        doc.Sections.Add(general);

        // 2. Таблица по местам работы
        var jobsTable = new ReportTable
        {
            Headers = [T("Column_Company"), T("Column_Position"), T("Column_Start"), T("Column_End"), T("Column_Months"), T("Column_Share")],
            NumberFormat = "0.0"
        };
        foreach (var j in s.Jobs)
        {
            jobsTable.Rows.Add([j.Company, j.Position, j.Start.ToString("MM.yyyy", culture),
                j.End?.ToString("MM.yyyy", culture) ?? T("Period_Present"), (double)j.Months, j.SharePercent]);
        }
        jobsTable.Totals = [T("Report_Total"), null, null, null, (double)s.TotalExperienceMonths, s.Jobs.Count == 0 ? 0d : 100d];
        doc.Sections.Add(new ReportSection { Heading = T("Section_Experience"), Table = jobsTable });

        // 3. Таблица по категориям навыков
        var skillsTable = new ReportTable
        {
            Headers = [T("Column_Category"), T("Column_Count"), T("Column_AvgLevel"), T("Column_MaxLevel")],
            NumberFormat = "0.00"
        };
        foreach (var c in s.SkillsByCategory)
            skillsTable.Rows.Add([T($"SkillCategory_{c.Category}"), (double)c.Count, c.AverageLevel, (double)c.MaxLevel]);
        skillsTable.Totals = [T("Report_Total"), (double)s.SkillsCount, s.AverageSkillLevel, s.SkillsByCategory.Count == 0 ? 0d : s.SkillsByCategory.Max(c => c.MaxLevel)];
        doc.Sections.Add(new ReportSection { Heading = T("Section_Skills"), Table = skillsTable });

        return doc;
    }

    /// <summary>Формирует текстовый вывод по правилам (простая экспертная оценка резюме).</summary>
    private string BuildConclusion(ResumeStatistics s, CultureInfo culture)
    {
        var parts = new List<string>();
        var years = s.TotalExperienceMonths / 12.0;

        parts.Add(years switch
        {
            < 1.0 => loc.Get("Conclusion_Experience_Junior", culture),
            < 3.0 => loc.Get("Conclusion_Experience_Middle", culture),
            _ => loc.Get("Conclusion_Experience_Senior", culture)
        });

        if (s.AverageSkillLevel >= 4) parts.Add(loc.Get("Conclusion_Skills_Strong", culture));
        else if (s.SkillsCount < 3) parts.Add(loc.Get("Conclusion_Skills_Few", culture));

        if (s.CompletenessPercent < 80)
            parts.Add(string.Format(culture, loc.Get("Conclusion_Incomplete", culture), s.CompletenessPercent));
        else
            parts.Add(loc.Get("Conclusion_Complete", culture));

        return string.Join(" ", parts);
    }
}
