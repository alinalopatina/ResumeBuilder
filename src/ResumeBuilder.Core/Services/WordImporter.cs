using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Resources;
using Serilog;

namespace ResumeBuilder.Core.Services;

/// <summary>
/// «Обратная задача»: читает таблицу мест работы из Word-документа обратно в программу.
/// Таблица ищется по заголовку первого столбца («Компания» или «Company»).
/// Строки с ошибками пропускаются, а ошибки собираются в список.
/// </summary>
public static class WordImporter
{
    public static List<WorkExperience> ImportExperience(string path, List<string> errors)
    {
        var result = new List<WorkExperience>();
        using WordprocessingDocument doc = WordprocessingDocument.Open(path, false);
        Body? body = doc.MainDocumentPart?.Document?.Body;
        if (body == null)
        {
            errors.Add(Messages.Import_TableNotFound);
            return result;
        }

        // Название столбца может быть на русском или на английском
        var companyHeaders = new[]
        {
            Messages.ResourceManager.GetString("Column_Company", new CultureInfo("ru-RU")),
            Messages.ResourceManager.GetString("Column_Company", new CultureInfo("en-US"))
        };

        Table? table = body.Descendants<Table>()
            .FirstOrDefault(t => companyHeaders.Contains(FirstCellText(t)));
        if (table == null)
        {
            errors.Add(Messages.Import_TableNotFound);
            return result;
        }

        List<TableRow> rows = table.Elements<TableRow>().Skip(1).ToList(); // первая строка — шапка
        for (int i = 0; i < rows.Count; i++)
        {
            List<string> cells = rows[i].Elements<TableCell>().Select(c => c.InnerText.Trim()).ToList();
            int rowNumber = i + 2;
            if (cells.Count < 4 || cells[0] == "")
            {
                errors.Add(string.Format(Messages.Import_BadRow, rowNumber));
                continue;
            }

            if (!DateTime.TryParseExact(cells[2], WordExporter.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime start))
            {
                errors.Add(string.Format(Messages.Import_BadDate, rowNumber, cells[2]));
                continue;
            }

            DateTime? end = null;
            if (DateTime.TryParseExact(cells[3], WordExporter.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime endDate))
                end = endDate;

            result.Add(new WorkExperience
            {
                Company = cells[0],
                Position = cells[1],
                StartDate = start,
                EndDate = end,
                Description = cells.Count > 4 ? cells[4] : ""
            });
        }

        Log.Information("Импорт из Word: {Count} записей, ошибок {Errors}", result.Count, errors.Count);
        return result;
    }

    private static string FirstCellText(Table table) =>
        table.Elements<TableRow>().FirstOrDefault()?.Elements<TableCell>().FirstOrDefault()?.InnerText.Trim() ?? "";
}
