using System.Diagnostics;
using System.Globalization;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ResumeBuilder.Core.Models;

namespace ResumeBuilder.Infrastructure.Office;

/// <summary>Результат замера одной стратегии экспорта.</summary>
public sealed record ExcelBenchmarkResult(string Strategy, int Rows, long ElapsedMs, long AllocatedMb, long FileKb);

/// <summary>
/// Сценарий <b>«Оптимизация»</b> (лаба «Excel/Word», модуль 3, на повышенную оценку):
/// экспорт 10 000–50 000 строк тремя способами с замером времени и памяти.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item><b>Naive</b> — ClosedXML, запись и стилизация каждой ячейки отдельно (как пишут «в лоб»).</item>
/// <item><b>Batch</b> — ClosedXML <c>InsertData</c> + один стиль на весь диапазон: меньше объектов-стилей.</item>
/// <item><b>Streaming</b> — Open XML SDK <see cref="OpenXmlWriter"/> (SAX-подход): строки пишутся прямо в поток,
/// в памяти не строится модель всего листа. Самый быстрый и экономный, но без «удобного» форматирования.</item>
/// </list>
/// </remarks>
public static class ExcelExportBenchmark
{
    /// <summary>Запускает все стратегии и возвращает результаты.</summary>
    public static IReadOnlyList<ExcelBenchmarkResult> Run(IReadOnlyList<WorkExperience> data, string outputFolder)
    {
        Directory.CreateDirectory(outputFolder);
        return
        [
            Measure("1. ClosedXML: по ячейкам + стиль каждой ячейки", data, Path.Combine(outputFolder, "bench_naive.xlsx"), Naive),
            Measure("2. ClosedXML: InsertData + стиль диапазона", data, Path.Combine(outputFolder, "bench_batch.xlsx"), Batch),
            Measure("3. OpenXmlWriter: потоковая запись (SAX)", data, Path.Combine(outputFolder, "bench_stream.xlsx"), Streaming)
        ];
    }

    private static ExcelBenchmarkResult Measure(string name, IReadOnlyList<WorkExperience> data, string path, Action<IReadOnlyList<WorkExperience>, Stream> action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var sw = Stopwatch.StartNew();

        using (var fs = File.Create(path)) action(data, fs);

        sw.Stop();
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        return new ExcelBenchmarkResult(name, data.Count, sw.ElapsedMilliseconds, allocated / (1024 * 1024), new FileInfo(path).Length / 1024);
    }

    private static void Naive(IReadOnlyList<WorkExperience> data, Stream output)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Data");
        string[] headers = ["Company", "Position", "City", "Start", "End", "Description"];
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        for (var i = 0; i < data.Count; i++)
        {
            var e = data[i];
            object?[] values = [e.Company, e.Position, e.City, e.StartDate, e.EndDate, e.Description];
            for (var c = 0; c < values.Length; c++)
            {
                var cell = ws.Cell(i + 2, c + 1);
                cell.Value = XLCellValue.FromObject(values[c]);
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin; // стиль на каждую ячейку — дорого
                if (values[c] is DateTime) cell.Style.DateFormat.Format = "mm.yyyy";
            }
        }
        ws.Columns().AdjustToContents(); // проход по всем ячейкам — тоже дорого
        wb.SaveAs(output);
    }

    private static void Batch(IReadOnlyList<WorkExperience> data, Stream output)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Data");
        string[] headers = ["Company", "Position", "City", "Start", "End", "Description"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];

        ws.Cell(2, 1).InsertData(data.Select(e => new { e.Company, e.Position, e.City, e.StartDate, e.EndDate, e.Description }));

        var all = ws.Range(1, 1, data.Count + 1, headers.Length);
        all.Style.Border.OutsideBorder = XLBorderStyleValues.Thin; // один стиль на диапазон
        all.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        ws.Range(2, 4, data.Count + 1, 5).Style.DateFormat.Format = "mm.yyyy";
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, Math.Min(100, data.Count + 1)); // ширина по первым 100 строкам
        wb.SaveAs(output);
    }

    private static void Streaming(IReadOnlyList<WorkExperience> data, Stream output)
    {
        using var doc = SpreadsheetDocument.Create(output, SpreadsheetDocumentType.Workbook);
        var wbPart = doc.AddWorkbookPart();
        var wsPart = wbPart.AddNewPart<WorksheetPart>();

        using (var writer = OpenXmlWriter.Create(wsPart))
        {
            writer.WriteStartElement(new Worksheet());
            writer.WriteStartElement(new SheetData());

            WriteRow(writer, ["Company", "Position", "City", "Start", "End", "Description"]);
            foreach (var e in data)
            {
                WriteRow(writer,
                [
                    e.Company, e.Position, e.City ?? string.Empty,
                    e.StartDate.ToString("MM.yyyy", CultureInfo.InvariantCulture),
                    e.EndDate?.ToString("MM.yyyy", CultureInfo.InvariantCulture) ?? string.Empty,
                    e.Description ?? string.Empty
                ]);
            }

            writer.WriteEndElement(); // SheetData
            writer.WriteEndElement(); // Worksheet
        }

        wbPart.Workbook = new Workbook(new Sheets(new Sheet { Id = wbPart.GetIdOfPart(wsPart), SheetId = 1U, Name = "Data" }));
        wbPart.Workbook.Save();
    }

    private static void WriteRow(OpenXmlWriter writer, string[] values)
    {
        writer.WriteStartElement(new Row());
        foreach (var v in values)
            writer.WriteElement(new Cell { DataType = CellValues.InlineString, InlineString = new InlineString(new Text(v)) });
        writer.WriteEndElement();
    }
}
