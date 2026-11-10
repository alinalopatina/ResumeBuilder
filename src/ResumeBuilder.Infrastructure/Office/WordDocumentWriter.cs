using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace ResumeBuilder.Infrastructure.Office;

/// <summary>
/// Тонкая обёртка над Open XML SDK для построения .docx: заголовки, абзацы, таблицы, картинки.
/// </summary>
/// <remarks>
/// Open XML SDK — официальная библиотека Microsoft, работает с файлом напрямую (это ZIP-архив с XML),
/// <b>не требует установленного Word</b> и работает в Docker, в отличие от Office Interop.
/// Порядок дочерних элементов (pPr, rPr, tblPr, tcPr) соблюдается по схеме ECMA-376,
/// иначе Word сообщает «файл повреждён».
/// </remarks>
public sealed class WordDocumentWriter : IDisposable
{
    private const string FontName = "Calibri";
    private const int ContentWidthTwips = 9638; // A4 (11906) − поля 2 × 1134
    private readonly WordprocessingDocument _document;
    private readonly MainDocumentPart _main;
    private readonly Body _body;
    private uint _imageCounter;

    /// <summary>Создаёт новый документ в потоке.</summary>
    public WordDocumentWriter(Stream output, string? title = null, string? author = null)
    {
        _document = WordprocessingDocument.Create(output, WordprocessingDocumentType.Document);
        _main = _document.AddMainDocumentPart();
        _main.Document = new Document(new Body());
        _body = _main.Document.Body!;

        _document.PackageProperties.Title = title;
        _document.PackageProperties.Creator = author ?? "ResumeBuilder";
        _document.PackageProperties.Created = DateTime.UtcNow;
    }

    /// <summary>Заголовок (уровень 1 — 16 пт, уровень 2 — 13 пт).</summary>
    public void Heading(string text, int level = 1, string color = "1F4E79") =>
        Paragraph(text, bold: true, sizePt: level == 1 ? 16 : 13, color: color, spacingBeforePt: level == 1 ? 6 : 12, spacingAfterPt: 4);

    /// <summary>Обычный абзац.</summary>
    public void Paragraph(string text, bool bold = false, bool italic = false, double sizePt = 11, string? color = null,
        JustificationValues? align = null, double spacingBeforePt = 0, double spacingAfterPt = 6)
    {
        var p = new Paragraph(ParagraphProps(align, spacingBeforePt, spacingAfterPt));
        AppendTextRuns(p, text, bold, italic, sizePt, color);
        _body.AppendChild(p);
    }

    /// <summary>Абзац «Подпись: значение» с жирной подписью.</summary>
    public void LabelValue(string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var p = new Paragraph(ParagraphProps(null, 0, 2));
        p.AppendChild(MakeRun($"{label}: ", bold: true));
        p.AppendChild(MakeRun(value));
        _body.AppendChild(p);
    }

    /// <summary>
    /// Таблица с жирной закрашенной шапкой, границами и повтором шапки на каждой странице.
    /// <paramref name="caption"/> записывается в свойство таблицы tblCaption («Замещающий текст → Заголовок»),
    /// по нему таблица надёжно находится при обратном импорте.
    /// </summary>
    public Table Table(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows,
        string? caption = null, IReadOnlyList<int>? relativeWidths = null, IReadOnlyList<string>? totals = null)
    {
        var widths = ColumnWidths(headers.Count, relativeWidths);
        var table = new Table();

        // tblPr: порядок по схеме — tblW, tblBorders, tblLayout, tblCaption
        var props = new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "8EAADB" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "8EAADB" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "8EAADB" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "8EAADB" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "8EAADB" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "8EAADB" }));
        if (caption is not null) props.AppendChild(new TableCaption { Val = caption });
        table.AppendChild(props);

        var grid = new TableGrid();
        foreach (var w in widths) grid.AppendChild(new GridColumn { Width = w.ToString() });
        table.AppendChild(grid);

        var headerRow = new TableRow(new TableRowProperties(new TableHeader()));
        for (var i = 0; i < headers.Count; i++)
            headerRow.AppendChild(Cell(headers[i], widths[i], bold: true, fill: "D9E2F3"));
        table.AppendChild(headerRow);

        foreach (var row in rows)
        {
            var tr = new TableRow();
            for (var i = 0; i < headers.Count; i++)
                tr.AppendChild(Cell(i < row.Count ? row[i] : string.Empty, widths[i]));
            table.AppendChild(tr);
        }

        if (totals is not null)
        {
            var tr = new TableRow();
            for (var i = 0; i < headers.Count; i++)
                tr.AppendChild(Cell(i < totals.Count ? totals[i] : string.Empty, widths[i], bold: true, fill: "F2F2F2"));
            table.AppendChild(tr);
        }

        _body.AppendChild(table);
        _body.AppendChild(new Paragraph(ParagraphProps(null, 0, 6))); // отступ после таблицы
        return table;
    }

    /// <summary>Вставляет PNG-изображение (например, QR-код) заданного размера в пикселях (96 dpi).</summary>
    public void Image(byte[] png, int widthPx, int heightPx, JustificationValues? align = null, string name = "image.png")
    {
        var imagePart = _main.AddImagePart(ImagePartType.Png);
        using (var ms = new MemoryStream(png)) imagePart.FeedData(ms);
        var relId = _main.GetIdOfPart(imagePart);

        const long emuPerPixel = 9525; // 914400 EMU на дюйм / 96 пикселей
        long cx = widthPx * emuPerPixel, cy = heightPx * emuPerPixel;
        var id = ++_imageCounter;

        var drawing = new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = id, Name = $"Picture {id}" },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = name },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U
            });

        _body.AppendChild(new Paragraph(ParagraphProps(align, 0, 6), new Run(drawing)));
    }

    /// <summary>Завершает документ: параметры страницы A4 и сохранение.</summary>
    public void Dispose()
    {
        _body.AppendChild(new SectionProperties(
            new PageSize { Width = 11906U, Height = 16838U },
            new PageMargin { Top = 1134, Right = 1134U, Bottom = 1134, Left = 1134U, Header = 708U, Footer = 708U, Gutter = 0U }));
        _main.Document.Save();
        _document.Dispose();
    }

    // ───────────── вспомогательные методы ─────────────

    private static int[] ColumnWidths(int count, IReadOnlyList<int>? relative)
    {
        relative ??= Enumerable.Repeat(1, count).ToArray();
        var sum = relative.Take(count).Sum();
        return Enumerable.Range(0, count).Select(i => ContentWidthTwips * (i < relative.Count ? relative[i] : 1) / Math.Max(sum, 1)).ToArray();
    }

    private static TableCell Cell(string text, int widthTwips, bool bold = false, string? fill = null)
    {
        // tcPr: tcW, затем shd
        var tcPr = new TableCellProperties(new TableCellWidth { Width = widthTwips.ToString(), Type = TableWidthUnitValues.Dxa });
        if (fill is not null) tcPr.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = fill });

        var cell = new TableCell(tcPr);
        // Многострочный текст ячейки — отдельные абзацы
        foreach (var line in (text ?? string.Empty).Split('\n'))
        {
            var p = new Paragraph(ParagraphProps(null, 0, 0));
            p.AppendChild(MakeRun(line.TrimEnd('\r'), bold, sizePt: 10));
            cell.AppendChild(p);
        }
        return cell;
    }

    private static ParagraphProperties ParagraphProps(JustificationValues? align, double beforePt, double afterPt)
    {
        // pPr: spacing, затем jc
        var pPr = new ParagraphProperties(new SpacingBetweenLines
        {
            Before = ((int)(beforePt * 20)).ToString(),
            After = ((int)(afterPt * 20)).ToString()
        });
        if (align is { } a) pPr.AppendChild(new Justification { Val = a });
        return pPr;
    }

    private static void AppendTextRuns(Paragraph p, string text, bool bold, bool italic, double sizePt, string? color)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var run = MakeRun(lines[i].TrimEnd('\r'), bold, italic, sizePt, color);
            if (i > 0) run.InsertAt(new Break(), run.RunProperties is null ? 0 : 1);
            p.AppendChild(run);
        }
    }

    private static Run MakeRun(string text, bool bold = false, bool italic = false, double sizePt = 11, string? color = null)
    {
        // rPr: rFonts, b, i, color, sz
        var rPr = new RunProperties(new RunFonts { Ascii = FontName, HighAnsi = FontName, ComplexScript = FontName, EastAsia = FontName });
        if (bold) rPr.AppendChild(new Bold());
        if (italic) rPr.AppendChild(new Italic());
        if (color is not null) rPr.AppendChild(new Color { Val = color });
        rPr.AppendChild(new FontSize { Val = ((int)(sizePt * 2)).ToString() });

        return new Run(rPr, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }
}
