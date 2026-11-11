using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Resources;
using Serilog;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace ResumeBuilder.Core.Services;

/// <summary>
/// Экспорт в Word через Open XML SDK (официальная библиотека Microsoft, Word устанавливать не нужно).
/// Документ: заголовок, абзац «О себе», таблица мест работы, QR-код картинкой.
/// </summary>
public static class WordExporter
{
    /// <summary>Формат дат в таблице. Он фиксированный, чтобы таблицу можно было прочитать обратно (WordImporter).</summary>
    public const string DateFormat = "MM.yyyy";

    public static void ExportResume(Resume resume, string path)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        MainDocumentPart main = doc.AddMainDocumentPart();
        main.Document = new Document(new Body());
        Body body = main.Document.Body!;
        PersonalInfo p = resume.Personal;

        // Шапка
        body.Append(MakeParagraph(p.FullName, bold: true, size: 36, color: TitleColor));
        body.Append(MakeParagraph(p.Position, size: 28));

        // Контакты — таблицей из двух столбцов
        body.Append(MakeHeading(Messages.Section_Contacts));
        var contacts = new List<string[]>
        {
            new[] { Messages.Field_Phone, TextHelper.NormalizePhone(p.Phone) },
            new[] { Messages.Field_Email, p.Email },
            new[] { Messages.Field_City, p.City }
        };
        if (p.BirthDate != null)
            contacts.Add(new[] { Messages.Field_BirthDate, $"{p.BirthDate:dd.MM.yyyy} ({Messages.Field_Age}: {p.GetAge(DateTime.Today)})" });
        if (!string.IsNullOrWhiteSpace(p.Website))
            contacts.Add(new[] { Messages.Field_Website, p.Website });
        body.Append(MakeTable(new[] { Messages.Column_Field, Messages.Column_Value }, contacts));

        // Абзац с описанием
        if (!string.IsNullOrWhiteSpace(resume.About))
        {
            body.Append(MakeHeading(Messages.Section_About));
            body.Append(MakeParagraph(resume.About));
        }

        // Опыт работы. Первый столбец «Компания» — по нему WordImporter находит таблицу
        body.Append(MakeHeading(Messages.Section_Experience));
        body.Append(MakeParagraph($"{Messages.Report_TotalExperience}: {Resume.FormatMonths(resume.GetTotalMonths(DateTime.Today))}", italic: true));
        var jobs = resume.Experience.Select(e => new[]
        {
            e.Company,
            e.Position,
            e.StartDate.ToString(DateFormat),
            e.EndDate?.ToString(DateFormat) ?? Messages.Present,
            e.GetMonths(DateTime.Today).ToString(),
            e.Description
        }).ToList();
        body.Append(MakeTable(new[] { Messages.Column_Company, Messages.Column_Position, Messages.Column_Start, Messages.Column_End, Messages.Column_Months, Messages.Column_Description }, jobs));

        // Образование
        if (resume.Education.Count > 0)
        {
            body.Append(MakeHeading(Messages.Section_Education));
            var education = resume.Education.Select(ed => new[]
            {
                ed.Institution,
                ed.Specialty,
                ed.StartYear.ToString(),
                ed.EndYear?.ToString() ?? "",
                ed.EndYear != null && ed.EndYear <= DateTime.Today.Year ? Messages.Status_Finished : Messages.Status_Studying
            }).ToList();
            body.Append(MakeTable(new[] { Messages.Column_Institution, Messages.Column_Specialty, Messages.Column_StartYear, Messages.Column_EndYear, Messages.Column_Status }, education));
        }

        // Навыки
        if (resume.Skills.Count > 0)
        {
            body.Append(MakeHeading(Messages.Section_Skills));
            var skills = resume.Skills.OrderByDescending(s => s.Level).Select(s => new[]
            {
                s.Name, s.Level + "/5", ResumeReport.SkillBar(s.Level)
            }).ToList();
            body.Append(MakeTable(new[] { Messages.Column_Skill, Messages.Column_Level, Messages.Column_Scale }, skills));
        }

        // Изображение — QR-код
        byte[] qr = QrCodeService.CreatePng(QrCodeService.BuildVCard(p), 6);
        body.Append(MakeParagraph(Messages.QrCaption, italic: true, size: 18));
        body.Append(MakeImage(main, qr, 140, 140));

        main.Document.Save();
        Log.Information("Word сохранён: {File}", Path.GetFileName(path));
    }

    /// <summary>Сводный отчёт: те же посчитанные данные, что и в Excel.</summary>
    public static void ExportReport(ResumeReport report, string path)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        MainDocumentPart main = doc.AddMainDocumentPart();
        main.Document = new Document(new Body());
        Body body = main.Document.Body!;

        body.Append(MakeParagraph(Messages.Report_Title, bold: true, size: 32, color: TitleColor));
        body.Append(MakeParagraph($"{report.FullName} — {report.Position}", size: 26));
        body.Append(MakeParagraph($"{Messages.Report_Created}: {report.CreatedAt:g}", italic: true, size: 18));

        body.Append(MakeHeading(Messages.Report_General));
        var summary = report.GetSummary().Select(x => new[] { x.Name, x.Value }).ToList();
        body.Append(MakeTable(new[] { Messages.Column_Field, Messages.Column_Value }, summary));

        body.Append(MakeHeading(Messages.Section_Experience));
        var rows = report.Rows.Select(r => new[]
        {
            r.Company, r.Position, r.Start.ToString(DateFormat), r.End?.ToString(DateFormat) ?? Messages.Present,
            r.Months.ToString(), r.SharePercent.ToString("0.0") + " %"
        }).ToList();
        rows.Add(new[] { Messages.Report_Total, "", "", "", report.TotalMonths.ToString(), report.Rows.Count > 0 ? "100 %" : "0 %" });
        body.Append(MakeTable(new[] { Messages.Column_Company, Messages.Column_Position, Messages.Column_Start, Messages.Column_End, Messages.Column_Months, Messages.Column_Share }, rows));

        body.Append(MakeHeading(Messages.Section_Skills));
        var levels = new List<string[]>();
        for (int level = 5; level >= 1; level--)
            levels.Add(new[] { ResumeReport.SkillBar(level), report.SkillsByLevel[level - 1].ToString() });
        body.Append(MakeTable(new[] { Messages.Column_Level, Messages.Report_SkillsCount }, levels));

        body.Append(MakeHeading(Messages.Report_Conclusion));
        body.Append(MakeParagraph(report.Conclusion));

        main.Document.Save();
        Log.Information("Отчёт Word сохранён: {File}", Path.GetFileName(path));
    }

    // ---------- вспомогательные методы ----------

    private const string TitleColor = "1F4E79";

    private static Paragraph MakeHeading(string text) =>
        MakeParagraph(text, bold: true, size: 26, color: TitleColor);

    private static Paragraph MakeParagraph(string text, bool bold = false, int size = 22, string? color = null, bool italic = false)
    {
        // size — в половинах пункта: 22 = 11 pt. Порядок элементов в RunProperties важен для Word
        var runProps = new RunProperties(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" });
        if (bold) runProps.Append(new Bold());
        if (italic) runProps.Append(new Italic());
        if (color != null) runProps.Append(new Color { Val = color });
        runProps.Append(new FontSize { Val = size.ToString() });
        return new Paragraph(new Run(runProps, new Text(text) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }));
    }

    private static Table MakeTable(string[] headers, List<string[]> rows)
    {
        var table = new Table();
        table.Append(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

        // Шапка таблицы — жирная, с серо-голубой заливкой
        var headerRow = new TableRow();
        foreach (string h in headers)
            headerRow.Append(new TableCell(
                new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "D9E2F3" }),
                MakeParagraph(h, bold: true)));
        table.Append(headerRow);

        foreach (string[] values in rows)
        {
            var tr = new TableRow();
            foreach (string v in values)
                tr.Append(new TableCell(MakeParagraph(v)));
            table.Append(tr);
        }
        return table;
    }

    private static Paragraph MakeImage(MainDocumentPart main, byte[] png, int widthPx, int heightPx)
    {
        ImagePart imagePart = main.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(png))
            imagePart.FeedData(stream);
        string relId = main.GetIdOfPart(imagePart);

        long cx = widthPx * 9525L; // 9525 EMU = 1 пиксель
        long cy = heightPx * 9525L;

        var drawing = new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = 1U, Name = "QR" },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = "qr.png" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })));

        return new Paragraph(new Run(drawing));
    }
}
