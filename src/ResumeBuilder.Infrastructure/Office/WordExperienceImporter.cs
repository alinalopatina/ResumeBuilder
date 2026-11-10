using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Localization;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Text;

namespace ResumeBuilder.Infrastructure.Office;

/// <summary>
/// <b>«Обратная задача»</b> (лаба «Excel/Word», модуль 3, на повышенную оценку):
/// чтение таблицы мест работы из Word-документа обратно в список объектов.
/// </summary>
/// <remarks>
/// Сложности парсинга Word по сравнению с Excel и как они решены:
/// <list type="number">
/// <item>В документе может быть несколько таблиц → ищем по метке tblCaption, а если её стёрли — по заголовкам столбцов.</item>
/// <item>Пользователь мог переставить столбцы → столбцы сопоставляются по названиям (на русском и английском), а не по номерам.</item>
/// <item>Текст ячейки — набор абзацев и «прогонов» (run), Word режет слова на части → собираем текст из всех Text-элементов.</item>
/// <item>Даты — это просто текст → разбираем регулярным выражением, понимаем ММ.ГГГГ, ДД.ММ.ГГГГ, «н.в.», «present».</item>
/// <item>Ошибочная строка не должна ронять весь импорт → ошибки копятся в <see cref="ImportResult{T}.Errors"/>.</item>
/// </list>
/// </remarks>
public sealed partial class WordExperienceImporter(ILocalizer localizer, ILogger<WordExperienceImporter> logger) : IExperienceImporter
{
    private enum Col { Company, Position, City, Start, End, Description }

    [GeneratedRegex(@"^(?:(?<d>\d{1,2})\.)?(?<m>\d{1,2})\.(?<y>\d{4})$", RegexOptions.None, 1000)]
    private static partial Regex DateCell();

    [GeneratedRegex(@"^(?:н\.?\s?в\.?|по\s+настоящее\s+время|настоящее\s+время|present|now|—|-|)$", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex PresentCell();

    /// <inheritdoc />
    public ImportResult<WorkExperience> Import(Stream input)
    {
        var result = new ImportResult<WorkExperience>();
        using var doc = WordprocessingDocument.Open(input, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            result.Errors.Add(localizer["Import_NoBody"]);
            return result;
        }

        var table = FindExperienceTable(body, out var columns);
        if (table is null || columns is null)
        {
            result.Errors.Add(localizer["Import_TableNotFound"]);
            return result;
        }

        var rows = table.Elements<TableRow>().Skip(1).ToList(); // первая строка — шапка
        for (var i = 0; i < rows.Count; i++)
        {
            var cells = rows[i].Elements<TableCell>().Select(CellText).ToList();
            if (cells.All(string.IsNullOrWhiteSpace)) continue; // пустые строки пропускаем

            string Get(Col c) => columns.TryGetValue(c, out var idx) && idx < cells.Count ? cells[idx] : string.Empty;
            var rowNo = i + 2;

            var company = Get(Col.Company);
            var position = Get(Col.Position);
            if (string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(position))
            {
                result.Errors.Add(localizer.Format("Import_RowMissingFields", rowNo));
                continue;
            }

            if (!TryParseDate(Get(Col.Start), out var start) || start is null)
            {
                result.Errors.Add(localizer.Format("Import_RowBadDate", rowNo, Get(Col.Start)));
                continue;
            }

            var endText = Get(Col.End);
            DateTime? end = null;
            if (!PresentCell().IsMatch(endText.Trim()) && !TryParseDate(endText, out end))
            {
                result.Errors.Add(localizer.Format("Import_RowBadDate", rowNo, endText));
                continue;
            }

            result.Items.Add(new WorkExperience
            {
                Company = company,
                Position = position,
                City = NullIfEmpty(Get(Col.City)),
                StartDate = start.Value,
                EndDate = end,
                Description = NullIfEmpty(Get(Col.Description))
            });
        }

        logger.LogInformation("Импорт из Word: прочитано {Count} записей, ошибок {Errors}", result.Items.Count, result.Errors.Count);
        return result;
    }

    private Table? FindExperienceTable(Body body, out Dictionary<Col, int>? columns)
    {
        columns = null;
        var tables = body.Descendants<Table>().ToList();

        // 1) по метке tblCaption, которую ставит наш экспортёр
        var byCaption = tables.FirstOrDefault(t =>
            t.GetFirstChild<TableProperties>()?.GetFirstChild<TableCaption>()?.Val?.Value == WordResumeExporter.ExperienceTableCaption);

        // 2) любая таблица, в шапке которой есть «Компания»/«Company» и «Должность»/«Position»
        var candidates = byCaption is null ? tables : new List<Table> { byCaption }.Concat(tables).ToList();
        foreach (var t in candidates)
        {
            var header = t.Elements<TableRow>().FirstOrDefault();
            if (header is null) continue;
            var map = MapColumns(header.Elements<TableCell>().Select(CellText).ToList());
            if (map.ContainsKey(Col.Company) && map.ContainsKey(Col.Position) && map.ContainsKey(Col.Start))
            {
                columns = map;
                return t;
            }
        }
        return null;
    }

    /// <summary>Сопоставляет названия столбцов (на любом из поддерживаемых языков) с их номерами.</summary>
    private Dictionary<Col, int> MapColumns(IReadOnlyList<string> headers)
    {
        var keys = new Dictionary<Col, string>
        {
            [Col.Company] = "Column_Company",
            [Col.Position] = "Column_Position",
            [Col.City] = "Column_City",
            [Col.Start] = "Column_Start",
            [Col.End] = "Column_End",
            [Col.Description] = "Column_Description"
        };

        var map = new Dictionary<Col, int>();
        foreach (var (col, key) in keys)
        {
            var names = ResxLocalizer.SupportedCultures.Select(c => localizer.Get(key, c)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                if (names.Contains(headers[i].Trim()))
                {
                    map[col] = i;
                    break;
                }
            }
        }
        return map;
    }

    /// <summary>Текст ячейки: абзацы через перевод строки, лишние пробелы убраны.</summary>
    private static string CellText(TableCell cell) =>
        string.Join("\n", cell.Elements<Paragraph>()
            .Select(p => TextFormatter.CollapseSpaces(string.Concat(p.Descendants<Text>().Select(t => t.Text))))
            .Where(s => s.Length > 0));

    private static bool TryParseDate(string text, out DateTime? date)
    {
        date = null;
        var m = DateCell().Match(text.Trim());
        if (!m.Success) return false;
        var day = m.Groups["d"].Success ? int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture) : 1;
        var month = int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture);
        var year = int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture);
        if (month is < 1 or > 12 || year is < 1950 or > 2100 || day < 1 || day > DateTime.DaysInMonth(year, month)) return false;
        date = new DateTime(year, month, day);
        return true;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
