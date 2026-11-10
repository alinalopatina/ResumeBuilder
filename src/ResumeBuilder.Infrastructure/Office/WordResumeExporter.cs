using System.Globalization;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Localization;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Services;
using ResumeBuilder.Infrastructure.Pdf;

namespace ResumeBuilder.Infrastructure.Office;

/// <summary>
/// Экспорт резюме в Word (лаба «Excel/Word», модуль 2): абзацы с описанием,
/// таблица мест работы и изображение (QR-код).
/// </summary>
public sealed class WordResumeExporter(IQrCodeGenerator qr, ILocalizer localizer, ILogger<WordResumeExporter> logger) : IResumeExporter
{
    /// <summary>Значение tblCaption у таблицы опыта — метка для обратного импорта.</summary>
    public const string ExperienceTableCaption = "ResumeBuilder.Experience";

    /// <summary>Формат дат в таблице — фиксированный, чтобы таблицу можно было прочитать обратно.</summary>
    public const string DateFormat = "MM.yyyy";

    /// <inheritdoc />
    public string FormatName => "Word";

    /// <inheritdoc />
    public string FileExtension => ".docx";

    /// <inheritdoc />
    public void Export(Resume resume, Stream output, ExportOptions options)
    {
        using var op = OperationTimer.Start(logger, "ExportWord");
        try
        {
            var qrPng = options.IncludeQrCode ? qr.GeneratePng(QrPayloadBuilder.Build(resume), 6) : null;
            var ctx = new ResumeRenderContext(resume, options, localizer, qrPng);

            using var w = new WordDocumentWriter(output, $"{ctx.T("Pdf_Resume")} — {ctx.DisplayName}", ctx.DisplayName);

            // 1. Шапка и контакты
            w.Paragraph(ctx.DisplayName, bold: true, sizePt: 22, color: "1F4E79", spacingAfterPt: 2);
            w.Paragraph(resume.Personal.DesiredPosition, sizePt: 14, color: "595959", spacingAfterPt: 8);
            w.LabelValue(ctx.T("Field_Phone"), ctx.Phone);
            w.LabelValue(ctx.T("Field_Email"), ctx.Email);
            w.LabelValue(ctx.T("Field_Website"), resume.Personal.Website);
            w.LabelValue(ctx.T("Field_City"), resume.Personal.City);
            w.LabelValue(ctx.T("Field_Age"), ctx.Age);
            w.LabelValue(ctx.T("Field_Salary"), ctx.Salary);

            // 2. Параграф с описанием
            if (!string.IsNullOrWhiteSpace(resume.Summary))
            {
                w.Heading(ctx.T("Section_Summary"), 2);
                w.Paragraph(resume.Summary, align: JustificationValues.Both);
            }

            // 3. Таблица опыта (сущность, которая потом читается обратно)
            if (resume.Experience.Count > 0)
            {
                w.Heading(ctx.T("Section_Experience"), 2);
                var rows = resume.Experience
                    .OrderByDescending(e => e.StartDate)
                    .Select(e => (IReadOnlyList<string>)new[]
                    {
                        e.Company,
                        e.Position,
                        e.City ?? string.Empty,
                        e.StartDate.ToString(DateFormat, CultureInfo.InvariantCulture),
                        e.EndDate?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? ctx.T("Period_Present"),
                        e.Description ?? string.Empty
                    });
                w.Table(ExperienceHeaders(ctx), rows, ExperienceTableCaption, [3, 3, 2, 2, 2, 6]);
            }

            // 4. Образование, навыки, языки — списком
            if (resume.Education.Count > 0)
            {
                w.Heading(ctx.T("Section_Education"), 2);
                foreach (var ed in resume.Education)
                    w.Paragraph($"• {ed.GetSummary(ctx.Culture)}" + (string.IsNullOrWhiteSpace(ed.Degree) ? "" : $" — {ed.Degree}"));
            }

            if (resume.Skills.Count > 0)
            {
                w.Heading(ctx.T("Section_Skills"), 2);
                foreach (var g in resume.Skills.GroupBy(s => s.Category))
                    w.LabelValue(ctx.T($"SkillCategory_{g.Key}"),
                        string.Join(", ", g.OrderByDescending(s => s.Level).Select(s => $"{s.Name} ({s.Level}/{Skill.MaxLevel})")));
            }

            if (resume.Languages.Count > 0)
            {
                w.Heading(ctx.T("Section_Languages"), 2);
                w.Paragraph(string.Join("; ", resume.Languages.Select(l => $"{l.Language} — {ctx.T($"LanguageLevel_{l.Level}")}")));
            }

            // 5. Изображение — QR-код
            if (qrPng is not null)
            {
                w.Paragraph(ctx.T(resume.QrMode == QrContentMode.VCard ? "Pdf_QrVCard" : "Pdf_QrWebsite"),
                    italic: true, sizePt: 9, color: "7F7F7F", align: JustificationValues.Right, spacingBeforePt: 12);
                w.Image(qrPng, 140, 140, JustificationValues.Right, "qr.png");
            }

            logger.LogInformation("Word-документ сформирован: {Jobs} мест работы", resume.Experience.Count);
        }
        catch (Exception ex)
        {
            op.Fail(ex);
            throw;
        }
    }

    /// <summary>Заголовки таблицы опыта на языке документа.</summary>
    public static IReadOnlyList<string> ExperienceHeaders(ResumeRenderContext ctx) =>
    [
        ctx.T("Column_Company"), ctx.T("Column_Position"), ctx.T("Column_City"),
        ctx.T("Column_Start"), ctx.T("Column_End"), ctx.T("Column_Description")
    ];
}
