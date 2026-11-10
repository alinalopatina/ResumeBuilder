using System.Globalization;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Builder;
using ResumeBuilder.Core.Localization;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Reports;
using ResumeBuilder.Infrastructure.Logging;
using ResumeBuilder.Infrastructure.Office;
using ResumeBuilder.Infrastructure.Pdf;
using ResumeBuilder.Infrastructure.Qr;
using ResumeBuilder.Infrastructure.Reports;

namespace ResumeBuilder.Tests;

/// <summary>Агрегация отчёта, экспорт в Excel/Word/PDF и обратный импорт из Word.</summary>
public class ReportAndExportTests
{
    private static readonly DateTime Today = new(2026, 11, 10);
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
    private readonly ILocalizer _loc = new ResxLocalizer(Ru);

    private static Resume TwoJobs() => new ResumeFluentBuilder()
        .WithName("Тестов Тест").WithPosition("Dev").WithContacts("t@t.by", "+375291112233")
        .AddExperience("A", "Dev", new DateTime(2024, 1, 1), new DateTime(2025, 1, 1), "x")   // 12 мес.
        .AddExperience("B", "Dev", new DateTime(2025, 5, 1), null, "y")                         // 18 мес. до 11.2026
        .AddSkill("C#", 5).AddSkill("SQL", 3).AddSkill("Git", 4, SkillCategory.Tools)
        .Build();

    [Fact]
    public void Statistics_AggregatesCorrectly()
    {
        var s = new ResumeStatisticsService(() => Today).Calculate(TwoJobs());

        Assert.Equal(2, s.JobsCount);
        Assert.Equal(30, s.TotalExperienceMonths);
        Assert.Equal(15, s.AverageJobMonths);
        Assert.Equal("B", s.LongestJob!.Company);
        Assert.Equal(60.0, s.Jobs.Single(j => j.Company == "B").SharePercent);
        Assert.Equal(4.0, s.AverageSkillLevel);
        var prog = s.SkillsByCategory.Single(c => c.Category == SkillCategory.Programming);
        Assert.Equal(2, prog.Count);
        Assert.Equal(4.0, prog.AverageLevel);
    }

    [Fact]
    public void ReportDocument_IsLocalized()
    {
        var stats = new ResumeStatisticsService(() => Today).Calculate(TwoJobs());
        var builder = new ReportDocumentBuilder(_loc);

        var ru = builder.Build(stats, Ru);
        var en = builder.Build(stats, CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("Сводный отчёт по резюме", ru.Title);
        Assert.Equal("Resume summary report", en.Title);
        Assert.Contains(ru.Sections[0].Metrics, m => m.Value == "2 года 6 месяцев");
        Assert.Contains(en.Sections[0].Metrics, m => m.Value == "2 years 6 months");
        Assert.False(string.IsNullOrWhiteSpace(ru.Conclusion));
    }

    [Fact]
    public void AllRenderers_ProduceNonEmptyFiles_FromSameReport()
    {
        var stats = new ResumeStatisticsService(() => Today).Calculate(TwoJobs());
        var report = new ReportDocumentBuilder(_loc).Build(stats, Ru);
        IReportRenderer[] renderers = [new ExcelReportRenderer(), new WordReportRenderer(), new PdfReportRenderer(), new TextReportRenderer()];

        foreach (var r in renderers)
        {
            using var ms = new MemoryStream();
            r.Render(report, ms);
            Assert.True(ms.Length > 100, $"{r.FormatName}: {ms.Length} байт");
        }
    }

    [Fact]
    public void TextRenderer_ContainsTableAndTotals()
    {
        var stats = new ResumeStatisticsService(() => Today).Calculate(TwoJobs());
        var text = new TextReportRenderer().RenderToString(new ReportDocumentBuilder(_loc).Build(stats, Ru));
        Assert.Contains("┌", text);
        Assert.Contains("Итого", text);
        Assert.Contains("Тестов Тест", text);
    }

    [Fact]
    public void Excel_HasBoldHeader_FrozenRow_AndSumFormula()
    {
        var exporter = new ExcelExperienceExporter(_loc, NullLogger<ExcelExperienceExporter>.Instance);
        using var ms = new MemoryStream();
        exporter.Export(TwoJobs(), ms, ExportOptions.Default);
        ms.Position = 0;

        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet(1);
        Assert.Equal("Опыт работы", ws.Name);
        Assert.Equal("Компания", ws.Cell(1, 2).GetString());
        Assert.True(ws.Cell(1, 2).Style.Font.Bold);
        Assert.Equal(1, ws.SheetView.SplitRow);
        Assert.StartsWith("SUM(", ws.Cell(4, 7).FormulaA1);
    }

    [Fact]
    public void Word_ExportThenImport_RoundTrip()
    {
        var original = SampleData.CreateSampleResume();
        var exporter = new WordResumeExporter(new QrCodeService(), _loc, NullLogger<WordResumeExporter>.Instance);
        var importer = new WordExperienceImporter(_loc, NullLogger<WordExperienceImporter>.Instance);

        using var ms = new MemoryStream();
        exporter.Export(original, ms, ExportOptions.Default);
        ms.Position = 0;
        var result = importer.Import(ms);

        Assert.Empty(result.Errors);
        Assert.Equal(original.Experience.Count, result.Items.Count);
        foreach (var e in original.Experience)
        {
            var back = result.Items.Single(x => x.Company == e.Company);
            Assert.Equal(e.Position, back.Position);
            Assert.Equal(new DateTime(e.StartDate.Year, e.StartDate.Month, 1), back.StartDate);
            Assert.Equal(e.EndDate is null, back.EndDate is null);
            Assert.Equal(e.Description, back.Description);
        }
    }

    [Fact]
    public void Word_EnglishDocument_IsAlsoImportable()
    {
        var exporter = new WordResumeExporter(new QrCodeService(), _loc, NullLogger<WordResumeExporter>.Instance);
        var importer = new WordExperienceImporter(_loc, NullLogger<WordExperienceImporter>.Instance);
        using var ms = new MemoryStream();
        exporter.Export(TwoJobs(), ms, ExportOptions.Default with { CultureName = "en-US", IncludeQrCode = false });
        ms.Position = 0;

        var result = importer.Import(ms);

        Assert.Equal(2, result.Items.Count);
        Assert.Null(result.Items.Single(i => i.Company == "B").EndDate);
    }

    [Theory]
    [InlineData(ResumeTemplate.Classic)]
    [InlineData(ResumeTemplate.Modern)]
    public void Pdf_IsGenerated_ForEveryTemplate(ResumeTemplate template)
    {
        var resume = SampleData.CreateSampleResume();
        resume.Template = template;
        var exporter = new PdfResumeExporter(new QrCodeService(), _loc, NullLogger<PdfResumeExporter>.Instance);

        using var ms = new MemoryStream();
        exporter.Export(resume, ms, ExportOptions.Default with { MaskContacts = true });

        var header = System.Text.Encoding.ASCII.GetString(ms.ToArray(), 0, 5);
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void Qr_ProducesPng()
    {
        var png = new QrCodeService().GeneratePng("https://github.com/alinalopatina");
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
    }

    [Fact]
    public void ReportService_CalculatesOnce_RendersToAllFormats()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rb-tests-" + Guid.NewGuid().ToString("N"));
        var service = new ReportService(new ResumeStatisticsService(() => Today), new ReportDocumentBuilder(_loc),
            [new ExcelReportRenderer(), new WordReportRenderer(), new TextReportRenderer()],
            NullLogger<ReportService>.Instance, NullAuditLogger.Instance);
        try
        {
            var files = service.ExportAll(TwoJobs(), Ru, folder);
            Assert.Equal(3, files.Count);
            Assert.All(files, f => Assert.True(new FileInfo(f).Length > 0));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
