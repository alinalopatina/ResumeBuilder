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

        body.Append(MakeParagraph(p.FullName, bold: true, size: 32));
        body.Append(MakeParagraph(p.Position, size: 26));
        body.Append(MakeParagraph($"{Messages.Field_Phone}: {TextHelper.NormalizePhone(p.Phone)}   {Messages.Field_Email}: {p.Email}"));

        // Абзац с описанием
        body.Append(MakeParagraph(Messages.Section_About, bold: true, size: 26));
        body.Append(MakeParagraph(resume.About));

        // Таблица
        body.Append(MakeParagraph(Messages.Section_Experience, bold: true, size: 26));
        var rows = resume.Experience.Select(e => new[]
        {
            e.Company,
            e.Position,
            e.StartDate.ToString(DateFormat),
            e.EndDate?.ToString(DateFormat) ?? Messages.Present,
            e.Description
        }).ToList();
        body.Append(MakeTable(new[] { Messages.Column_Company, Messages.Column_Position, Messages.Column_Start, Messages.Column_End, Messages.Column_Description }, rows));

        // Изображение — QR-код
        byte[] qr = QrCodeService.CreatePng(QrCodeService.BuildVCard(p), 6);
        body.Append(MakeParagraph(Messages.QrCaption, size: 18));
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

        body.Append(MakeParagraph(Messages.Report_Title, bold: true, size: 32));
        body.Append(MakeParagraph(report.FullName, size: 26));
        foreach (var (name, value) in report.GetSummary())
            body.Append(MakeParagraph($"{name}: {value}"));

        var rows = report.Rows.Select(r => new[] { r.Company, r.Position, r.Months.ToString() }).ToList();
        body.Append(MakeTable(new[] { Messages.Column_Company, Messages.Column_Position, Messages.Column_Months }, rows));
        main.Document.Save();
    }

    // ---------- вспомогательные методы ----------

    private static Paragraph MakeParagraph(string text, bool bold = false, int size = 22)
    {
        // size — в половинах пункта: 22 = 11 pt
        var runProps = new RunProperties(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" });
        if (bold) runProps.Append(new Bold());
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

        var headerRow = new TableRow();
        foreach (string h in headers)
            headerRow.Append(new TableCell(MakeParagraph(h, bold: true)));
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
