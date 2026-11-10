using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Localization;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Services;

namespace ResumeBuilder.Infrastructure.Pdf;

/// <summary>
/// Экспорт резюме в PDF с QR-кодом (основная функция курсового проекта; лаба «Вывод данных на печать» —
/// PDF как самый надёжный формат для печати).
/// </summary>
public sealed class PdfResumeExporter(IQrCodeGenerator qr, ILocalizer localizer, ILogger<PdfResumeExporter> logger) : IResumeExporter
{
    static PdfResumeExporter()
    {
        // QuestPDF требует явно указать лицензию. Community — бесплатна для учебных проектов.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <inheritdoc />
    public string FormatName => "PDF";

    /// <inheritdoc />
    public string FileExtension => ".pdf";

    /// <inheritdoc />
    public void Export(Resume resume, Stream output, ExportOptions options)
    {
        using var op = OperationTimer.Start(logger, "ExportPdf");
        try
        {
            byte[]? qrPng = null;
            if (options.IncludeQrCode)
            {
                var payload = QrPayloadBuilder.Build(resume);
                qrPng = qr.GeneratePng(payload, 8);
                logger.LogDebug("QR-код сгенерирован: режим {QrMode}, {Length} символов", resume.QrMode, payload.Length);
            }

            var ctx = new ResumeRenderContext(resume, options, localizer, qrPng);
            PdfResumeTemplate template = resume.Template switch // полиморфизм: выбор наследника
            {
                ResumeTemplate.Classic => new ClassicPdfTemplate(ctx),
                _ => new ModernPdfTemplate(ctx)
            };

            template.GeneratePdf(output);
            logger.LogInformation("PDF сформирован: шаблон {Template}, маскирование {Masked}", resume.Template, options.MaskContacts);
        }
        catch (Exception ex)
        {
            op.Fail(ex);
            throw;
        }
    }
}
