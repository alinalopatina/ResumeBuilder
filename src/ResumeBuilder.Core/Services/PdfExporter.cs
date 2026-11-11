using System.Diagnostics;
using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Resources;
using Serilog;

namespace ResumeBuilder.Core.Services;

/// <summary>Экспорт резюме в PDF (библиотека QuestPDF) с QR-кодом.</summary>
public static class PdfExporter
{
    private static ILogger Logger => Log.ForContext(typeof(PdfExporter));

    private const string Accent = "#1F4E79";

    public static void Export(Resume resume, string path, bool includeQr, bool hideContacts = false)
    {
        // Проверка параметров метода (валидация, модуль 1)
        ArgumentNullException.ThrowIfNull(resume);
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Не указан путь к файлу", nameof(path));
        Logger.Debug("Экспорт в PDF: QR-код {IncludeQr}, скрыть контакты {HideContacts}", includeQr, hideContacts);

        var timer = Stopwatch.StartNew();
        // Бесплатная лицензия для учебных проектов
        QuestPDF.Settings.License = LicenseType.Community;

        byte[]? qr = includeQr ? QrCodeService.CreatePng(QrCodeService.BuildVCard(resume.Personal)) : null;
        PersonalInfo p = resume.Personal;
        string email = hideContacts ? TextHelper.MaskEmails(p.Email) : p.Email;
        string phone = TextHelper.NormalizePhone(p.Phone);
        CultureInfo culture = CultureInfo.CurrentUICulture;

        Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    col.Spacing(4);

                    // Шапка: имя и контакты слева, QR-код справа
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(head =>
                        {
                            head.Item().Text(p.FullName).FontSize(22).Bold().FontColor(Accent);
                            head.Item().Text(p.Position).FontSize(14);
                            head.Item().PaddingTop(6).Text($"{Messages.Field_Phone}: {phone}");
                            head.Item().Text($"{Messages.Field_Email}: {email}");
                            if (!string.IsNullOrWhiteSpace(p.City))
                                head.Item().Text($"{Messages.Field_City}: {p.City}");
                            if (p.BirthDate != null)
                                head.Item().Text($"{Messages.Field_BirthDate}: {p.BirthDate.Value.ToString("d", culture)} ({Messages.Field_Age}: {p.GetAge(DateTime.Today)})");
                            if (!string.IsNullOrWhiteSpace(p.Website))
                                head.Item().Text($"{Messages.Field_Website}: {p.Website}");
                        });
                        if (qr != null)
                            row.ConstantItem(100).Image(qr);
                    });

                    if (!string.IsNullOrWhiteSpace(resume.About))
                    {
                        Section(col, Messages.Section_About);
                        col.Item().Text(resume.About);
                    }

                    if (resume.Experience.Count > 0)
                    {
                        Section(col, Messages.Section_Experience);
                        col.Item().Text($"{Messages.Report_TotalExperience}: {Resume.FormatMonths(resume.GetTotalMonths(DateTime.Today))}").Italic();
                        foreach (WorkExperience e in resume.Experience.OrderByDescending(x => x.StartDate))
                        {
                            string end = e.EndDate?.ToString("MMMM yyyy", culture) ?? Messages.Present;
                            col.Item().Text($"{e.Position} — {e.Company}").Bold();
                            col.Item().Text($"{e.StartDate.ToString("MMMM yyyy", culture)} – {end} ({Resume.FormatMonths(e.GetMonths(DateTime.Today))})").FontColor("#757575");
                            if (!string.IsNullOrWhiteSpace(e.Description))
                                col.Item().PaddingBottom(4).Text(e.Description);
                        }
                    }

                    if (resume.Education.Count > 0)
                    {
                        Section(col, Messages.Section_Education);
                        foreach (Education ed in resume.Education)
                            col.Item().Text(ed.GetDescription());
                    }

                    if (resume.Skills.Count > 0)
                    {
                        Section(col, Messages.Section_Skills);
                        foreach (Skill s in resume.Skills.OrderByDescending(x => x.Level))
                        {
                            int level = s.Level;
                            col.Item().Text($"{s.Name}  {ResumeReport.SkillBar(level)}");
                        }
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf(path);

        Logger.Information("PDF сохранён: {File}, время {ExecutionTimeMs} мс", Path.GetFileName(path), timer.ElapsedMilliseconds);
    }

    private static void Section(ColumnDescriptor col, string title)
    {
        col.Item().PaddingTop(10).Text(title.ToUpper()).FontSize(13).Bold().FontColor(Accent);
        col.Item().LineHorizontal(1).LineColor(Accent);
    }
}
