using System.Globalization;
using Microsoft.Extensions.Logging;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Reports;
using ResumeBuilder.Core.Services;

namespace ResumeBuilder.Infrastructure.Reports;

/// <summary>
/// Сводный отчёт: <b>один</b> подсчёт → <b>много</b> форматов.
/// </summary>
/// <remarks>
/// Ответ на вопрос лабы «как избежать дублирования кода агрегации для двух форматов»:
/// <code>
/// Resume ─► ResumeStatisticsService (считает) ─► ReportDocumentBuilder (раскладывает по разделам)
///        ─► ReportDocument ─┬► ExcelReportRenderer
///                           ├► WordReportRenderer
///                           ├► PdfReportRenderer
///                           └► TextReportRenderer
/// </code>
/// Чтобы добавить новый формат (например, HTML), достаточно написать ещё один <see cref="IReportRenderer"/> —
/// код подсчёта не меняется (принцип открытости/закрытости).
/// </remarks>
public sealed class ReportService(
    ResumeStatisticsService statistics,
    ReportDocumentBuilder documentBuilder,
    IEnumerable<IReportRenderer> renderers,
    ILogger<ReportService> logger,
    IAuditLogger audit)
{
    /// <summary>Доступные форматы.</summary>
    public IReadOnlyList<IReportRenderer> Renderers { get; } = renderers.ToList();

    /// <summary>Строит формат-независимый отчёт.</summary>
    public ReportDocument BuildReport(Resume resume, CultureInfo culture)
    {
        using var _ = OperationTimer.Start(logger, "BuildReport");
        var stats = statistics.Calculate(resume);
        logger.LogDebug("Статистика: стаж {Months} мес., навыков {Skills}, заполненность {Completeness}%",
            stats.TotalExperienceMonths, stats.SkillsCount, stats.CompletenessPercent);
        return documentBuilder.Build(stats, culture);
    }

    /// <summary>Сохраняет отчёт во всех форматах в папку; возвращает пути к файлам.</summary>
    public IReadOnlyList<string> ExportAll(Resume resume, CultureInfo culture, string folder, string baseName = "summary-report")
    {
        Directory.CreateDirectory(folder);
        var report = BuildReport(resume, culture); // ← посчитано ОДИН раз
        var files = new List<string>();

        foreach (var renderer in Renderers)
        {
            var path = Path.Combine(folder, baseName + renderer.FileExtension);
            using (OperationTimer.Start(logger, $"RenderReport{renderer.FormatName}"))
            using (var fs = File.Create(path))
            {
                renderer.Render(report, fs);
            }
            files.Add(path);
        }

        logger.LogInformation("Сводный отчёт сохранён в {Count} форматах в {Folder}", files.Count, folder);
        audit.Log("ExportReport", baseName, true);
        return files;
    }

    /// <summary>Отрисовывает отчёт в один формат по расширению.</summary>
    public void Export(Resume resume, CultureInfo culture, string path)
    {
        var renderer = Renderers.FirstOrDefault(r => path.EndsWith(r.FileExtension, StringComparison.OrdinalIgnoreCase))
                       ?? throw new NotSupportedException($"Формат {Path.GetExtension(path)} не поддерживается");
        var report = BuildReport(resume, culture);
        using var fs = File.Create(path);
        renderer.Render(report, fs);
        audit.Log("ExportReport", Path.GetFileName(path), true);
    }
}
