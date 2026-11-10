using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Reports;
using ResumeBuilder.Infrastructure.Office;
using ResumeBuilder.Infrastructure.Pdf;

namespace ResumeBuilder.Infrastructure.Reports;

/// <summary>
/// Сводный отчёт → Excel. Числа записываются числами (а не строками), итоги — формулами SUM.
/// </summary>
public sealed class ExcelReportRenderer : IReportRenderer
{
    public string FormatName => "Excel";
    public string FileExtension => ".xlsx";

    public void Render(ReportDocument report, Stream output)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(ExcelExperienceExporter.SafeSheetName(report.Title));
        var row = 1;

        ws.Cell(row, 1).Value = report.Title;
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 16;
        row++;
        if (report.Subtitle is not null) ws.Cell(row++, 1).Value = report.Subtitle;
        if (report.GeneratedLabel is not null)
        {
            ws.Cell(row, 1).Value = report.GeneratedLabel;
            ws.Cell(row++, 1).Style.Font.Italic = true;
        }
        row++;

        var maxColumns = 2;
        foreach (var section in report.Sections)
        {
            ws.Cell(row, 1).Value = section.Heading;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 13;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#1F4E79");
            row++;

            foreach (var (name, value) in section.Metrics)
            {
                ws.Cell(row, 1).Value = name;
                ws.Cell(row, 2).Value = value;
                ws.Cell(row, 1).Style.Font.Bold = true;
                row++;
            }

            if (section.Table is { } table)
            {
                maxColumns = Math.Max(maxColumns, table.Headers.Count);
                var top = row;
                for (var c = 0; c < table.Headers.Count; c++) ws.Cell(row, c + 1).Value = table.Headers[c];
                var header = ws.Range(row, 1, row, table.Headers.Count);
                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = ExcelExperienceExporter.HeaderFill;
                row++;

                var firstData = row;
                foreach (var r in table.Rows)
                {
                    for (var c = 0; c < r.Length; c++) SetCell(ws.Cell(row, c + 1), r[c], table.NumberFormat);
                    row++;
                }
                var lastData = row - 1;

                if (table.Totals is { } totals)
                {
                    for (var c = 0; c < totals.Length; c++)
                    {
                        var cell = ws.Cell(row, c + 1);
                        // Для числовых итогов, где это сумма столбца, ставим формулу — отчёт «живой»
                        if (totals[c] is double && lastData >= firstData && IsSumColumn(table, c))
                            cell.FormulaA1 = $"SUM({cell.Address.ColumnLetter}{firstData}:{cell.Address.ColumnLetter}{lastData})";
                        else
                            SetCell(cell, totals[c], table.NumberFormat);
                    }
                    ws.Range(row, 1, row, table.Headers.Count).Style.Font.Bold = true;
                    row++;
                }

                var range = ws.Range(top, 1, row - 1, table.Headers.Count);
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }
            row++;
        }

        if (!string.IsNullOrWhiteSpace(report.Conclusion))
        {
            ws.Cell(row, 1).Value = report.ConclusionTitle;
            ws.Cell(row++, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Value = report.Conclusion;
            var conclusion = ws.Range(row, 1, row, maxColumns).Merge();
            conclusion.Style.Alignment.WrapText = true;
            conclusion.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            ws.Row(row).Height = 60;
        }

        ws.Columns(1, maxColumns).AdjustToContents(5, row, 10, 45);
        wb.SaveAs(output);
    }

    /// <summary>Итог по столбцу считается суммой, если это «количество/месяцы/доля» (а не среднее или максимум).</summary>
    private static bool IsSumColumn(ReportTable table, int column) =>
        table.Rows.Count > 0 && table.Totals is { } t && t[column] is double total &&
        Math.Abs(table.Rows.Sum(r => r[column] is double d ? d : 0) - total) < 0.6;

    private static void SetCell(IXLCell cell, object? value, string numberFormat)
    {
        switch (value)
        {
            case null: break;
            case double d:
                cell.Value = d;
                cell.Style.NumberFormat.Format = d == Math.Floor(d) ? "0" : numberFormat;
                break;
            case DateTime dt:
                cell.Value = dt;
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }
}

/// <summary>Сводный отчёт → Word (тот же <see cref="ReportDocument"/>, другой формат вывода).</summary>
public sealed class WordReportRenderer : IReportRenderer
{
    public string FormatName => "Word";
    public string FileExtension => ".docx";

    public void Render(ReportDocument report, Stream output)
    {
        var culture = CultureInfo.GetCultureInfo(report.CultureName);
        using var w = new WordDocumentWriter(output, report.Title);

        w.Paragraph(report.Title, bold: true, sizePt: 20, color: "1F4E79", spacingAfterPt: 2);
        if (report.Subtitle is not null) w.Paragraph(report.Subtitle, sizePt: 13, spacingAfterPt: 2);
        if (report.GeneratedLabel is not null) w.Paragraph(report.GeneratedLabel, italic: true, sizePt: 9, color: "7F7F7F");

        foreach (var section in report.Sections)
        {
            w.Heading(section.Heading, 2);
            foreach (var (name, value) in section.Metrics) w.LabelValue(name, value);

            if (section.Table is { } table)
            {
                var rows = table.Rows.Select(r => (IReadOnlyList<string>)r.Select(v => table.FormatCell(v, culture)).ToArray());
                var totals = table.Totals?.Select(v => table.FormatCell(v, culture)).ToArray();
                w.Table(table.Headers, rows, totals: totals);
            }
        }

        if (!string.IsNullOrWhiteSpace(report.Conclusion))
        {
            w.Heading(report.ConclusionTitle, 2);
            w.Paragraph(report.Conclusion, align: DocumentFormat.OpenXml.Wordprocessing.JustificationValues.Both);
        }
    }
}

/// <summary>Сводный отчёт → PDF (QuestPDF). Готов к печати.</summary>
public sealed class PdfReportRenderer : IReportRenderer
{
    public string FormatName => "PDF";
    public string FileExtension => ".pdf";

    public void Render(ReportDocument report, Stream output)
    {
        QuestPdfSetup.EnsureConfigured();
        var culture = CultureInfo.GetCultureInfo(report.CultureName);
        QuestPDF.Fluent.Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

            page.Header().Column(h =>
            {
                h.Item().Text(report.Title).FontSize(18).Bold().FontColor("#1F4E79");
                if (report.Subtitle is not null) h.Item().Text(report.Subtitle).FontSize(12);
                if (report.GeneratedLabel is not null) h.Item().Text(report.GeneratedLabel).FontSize(8).FontColor(Palette.Grey);
                h.Item().PaddingVertical(4).LineHorizontal(1).LineColor("#1F4E79");
            });

            page.Content().Column(col =>
            {
                col.Spacing(6);
                foreach (var section in report.Sections)
                {
                    col.Item().PaddingTop(6).Text(section.Heading).FontSize(13).Bold().FontColor("#1F4E79");
                    foreach (var (name, value) in section.Metrics)
                        col.Item().Text(t => { t.Span($"{name}: ").SemiBold(); t.Span(value); });

                    if (section.Table is { } table)
                    {
                        col.Item().Table(tbl =>
                        {
                            tbl.ColumnsDefinition(cd => { foreach (var _ in table.Headers) cd.RelativeColumn(); });
                            tbl.Header(hd =>
                            {
                                foreach (var head in table.Headers)
                                    hd.Cell().Background("#D9E2F3").Border(0.5f).BorderColor("#8EAADB").Padding(3).Text(head).Bold();
                            });
                            foreach (var r in table.Rows)
                                foreach (var v in r)
                                    tbl.Cell().Border(0.5f).BorderColor("#8EAADB").Padding(3).Text(table.FormatCell(v, culture));
                            if (table.Totals is { } totals)
                                foreach (var v in totals)
                                    tbl.Cell().Background("#F2F2F2").Border(0.5f).BorderColor("#8EAADB").Padding(3).Text(table.FormatCell(v, culture)).Bold();
                        });
                    }
                }

                if (!string.IsNullOrWhiteSpace(report.Conclusion))
                {
                    col.Item().PaddingTop(8).Text(report.ConclusionTitle).FontSize(13).Bold().FontColor("#1F4E79");
                    col.Item().Background("#F7F9FC").Padding(8).Text(report.Conclusion);
                }
            });

            page.Footer().AlignCenter().Text(t => { t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
        })).GeneratePdf(output);
    }
}

/// <summary>
/// Сводный отчёт → текстовая таблица (лаба «Вывод данных на печать»: красивый вывод в консоль и в .txt).
/// </summary>
public sealed class TextReportRenderer : IReportRenderer
{
    public string FormatName => "Text";
    public string FileExtension => ".txt";

    public void Render(ReportDocument report, Stream output)
    {
        // UTF-8 с BOM — чтобы «Блокнот» и Excel корректно показали кириллицу
        using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        writer.Write(RenderToString(report));
    }

    /// <summary>Отчёт в виде текста с псевдографическими таблицами.</summary>
    public string RenderToString(ReportDocument report)
    {
        var culture = CultureInfo.GetCultureInfo(report.CultureName);
        var sb = new StringBuilder();
        var line = new string('═', 78);

        sb.AppendLine(line);
        sb.AppendLine(Center(report.Title.ToUpper(culture), 78));
        if (report.Subtitle is not null) sb.AppendLine(Center(report.Subtitle, 78));
        if (report.GeneratedLabel is not null) sb.AppendLine(Center(report.GeneratedLabel, 78));
        sb.AppendLine(line);

        foreach (var section in report.Sections)
        {
            sb.AppendLine();
            sb.AppendLine($"■ {section.Heading}");
            if (section.Metrics.Count > 0)
            {
                var width = section.Metrics.Max(m => m.Key.Length);
                foreach (var (name, value) in section.Metrics)
                    sb.AppendLine($"  {name.PadRight(width)} : {value}");
            }

            if (section.Table is { } table)
                AppendTable(sb, table, culture);
        }

        if (!string.IsNullOrWhiteSpace(report.Conclusion))
        {
            sb.AppendLine();
            sb.AppendLine($"■ {report.ConclusionTitle}");
            foreach (var l in WordWrap(report.Conclusion, 76)) sb.AppendLine($"  {l}");
        }
        sb.AppendLine(line);
        return sb.ToString();
    }

    private static void AppendTable(StringBuilder sb, ReportTable table, CultureInfo culture)
    {
        var cells = table.Rows.Select(r => r.Select(v => table.FormatCell(v, culture)).ToArray()).ToList();
        var totals = table.Totals?.Select(v => table.FormatCell(v, culture)).ToArray();
        var widths = table.Headers.Select((h, i) =>
            Math.Min(28, new[] { h.Length }
                .Concat(cells.Select(r => i < r.Length ? r[i].Length : 0))
                .Concat(totals is null ? Array.Empty<int>() : new[] { i < totals.Length ? totals[i].Length : 0 })
                .Max())).ToArray();

        string Border(char l, char m, char r) => l + string.Join(m, widths.Select(w => new string('─', w + 2))) + r;
        string Row(IReadOnlyList<string> values) =>
            "│" + string.Join("│", widths.Select((w, i) =>
            {
                var v = i < values.Count ? values[i] : string.Empty;
                if (v.Length > w) v = v[..(w - 1)] + "…";
                var numeric = double.TryParse(v.Replace(' ', ' ').Replace(" ", ""), NumberStyles.Any, culture, out _);
                return " " + (numeric ? v.PadLeft(w) : v.PadRight(w)) + " ";
            })) + "│";

        sb.AppendLine(Border('┌', '┬', '┐'));
        sb.AppendLine(Row(table.Headers));
        sb.AppendLine(Border('├', '┼', '┤'));
        foreach (var r in cells) sb.AppendLine(Row(r));
        if (totals is not null)
        {
            sb.AppendLine(Border('├', '┼', '┤'));
            sb.AppendLine(Row(totals));
        }
        sb.AppendLine(Border('└', '┴', '┘'));
    }

    private static string Center(string text, int width) =>
        text.Length >= width ? text : new string(' ', (width - text.Length) / 2) + text;

    private static IEnumerable<string> WordWrap(string text, int width)
    {
        var current = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length + word.Length + 1 > width && current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }
        if (current.Length > 0) yield return current.ToString();
    }
}
