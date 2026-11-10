using System.Globalization;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Localization;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Services;

namespace ResumeBuilder.Infrastructure.Office;

/// <summary>
/// Экспорт сущностей резюме в Excel (лаба «Excel/Word», модуль 1):
/// лист «Опыт работы» и лист «Навыки» с жирной шапкой, границами, закреплённой строкой,
/// автофильтром, форматами дат и итоговой формулой.
/// </summary>
/// <remarks>
/// Используется ClosedXML (MIT) — высокоуровневая обёртка над Open XML SDK.
/// Interop.Excel не используется: требует установленного Excel, не потокобезопасен и не работает в Docker.
/// </remarks>
public sealed class ExcelExperienceExporter(ILocalizer localizer, ILogger<ExcelExperienceExporter> logger) : IResumeExporter
{
    /// <summary>Цвет шапки таблиц.</summary>
    public static readonly XLColor HeaderFill = XLColor.FromHtml("#D9E2F3");

    /// <inheritdoc />
    public string FormatName => "Excel";

    /// <inheritdoc />
    public string FileExtension => ".xlsx";

    /// <inheritdoc />
    public void Export(Resume resume, Stream output, ExportOptions options)
    {
        using var op = OperationTimer.Start(logger, "ExportExcel");
        var culture = CultureInfo.GetCultureInfo(options.CultureName ?? resume.CultureName);
        string T(string key) => localizer.Get(key, culture);

        using var wb = new XLWorkbook();
        wb.Properties.Author = resume.Personal.FullName;
        wb.Properties.Title = $"{T("Pdf_Resume")} — {resume.Personal.FullName}";

        WriteExperienceSheet(wb.Worksheets.Add(SafeSheetName(T("Section_Experience"))), resume.Experience, T);
        WriteSkillsSheet(wb.Worksheets.Add(SafeSheetName(T("Section_Skills"))), resume.Skills, T);

        wb.SaveAs(output);
        logger.LogInformation("Excel сформирован: {Jobs} мест работы, {Skills} навыков", resume.Experience.Count, resume.Skills.Count);
    }

    /// <summary>Лист «Опыт работы».</summary>
    public static void WriteExperienceSheet(IXLWorksheet ws, IReadOnlyList<WorkExperience> items, Func<string, string> T)
    {
        string[] headers =
        [
            "№", T("Column_Company"), T("Column_Position"), T("Column_City"),
            T("Column_Start"), T("Column_End"), T("Column_Months"), T("Column_Description")
        ];
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];

        var today = DateTime.Today;
        var row = 2;
        foreach (var e in items.OrderByDescending(x => x.StartDate))
        {
            ws.Cell(row, 1).Value = row - 1;
            ws.Cell(row, 2).Value = e.Company;
            ws.Cell(row, 3).Value = e.Position;
            ws.Cell(row, 4).Value = e.City ?? string.Empty;
            ws.Cell(row, 5).Value = e.StartDate;
            if (e.EndDate is { } end) ws.Cell(row, 6).Value = end;
            else ws.Cell(row, 6).Value = T("Period_Present");
            ws.Cell(row, 7).Value = e.GetDurationMonths(today);
            ws.Cell(row, 8).Value = e.Description ?? string.Empty;
            row++;
        }

        var lastDataRow = row - 1;
        if (items.Count > 0)
        {
            // Итог — формулой Excel, а не числом: пользователь может править данные, сумма пересчитается
            ws.Cell(row, 6).Value = T("Report_Total");
            ws.Cell(row, 7).FormulaA1 = $"SUM(G2:G{lastDataRow})";
            ws.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
        }

        ws.Range(2, 5, Math.Max(2, lastDataRow), 6).Style.DateFormat.Format = "mm.yyyy";
        ApplyTableStyle(ws, headers.Length, items.Count > 0 ? row : 1);

        ws.Column(8).Width = 60;
        ws.Column(8).Style.Alignment.WrapText = true;
    }

    /// <summary>Лист «Навыки».</summary>
    public static void WriteSkillsSheet(IXLWorksheet ws, IReadOnlyList<Skill> skills, Func<string, string> T)
    {
        string[] headers = [T("Column_Skill"), T("Column_Category"), T("Column_Level")];
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];

        var row = 2;
        foreach (var s in skills.OrderBy(x => x.Category).ThenByDescending(x => x.Level))
        {
            ws.Cell(row, 1).Value = s.Name;
            ws.Cell(row, 2).Value = T($"SkillCategory_{s.Category}");
            ws.Cell(row, 3).Value = s.Level;
            row++;
        }

        if (skills.Count > 0)
        {
            // Условное форматирование: шкала данных по уровню навыка
            ws.Range(2, 3, row - 1, 3).AddConditionalFormat().DataBar(XLColor.FromHtml("#5B9BD5")).LowestValue().HighestValue();
        }
        ApplyTableStyle(ws, headers.Length, row - 1);
    }

    /// <summary>Базовое форматирование: жирная шапка с заливкой, границы, закреплённая строка, автофильтр, ширина столбцов.</summary>
    public static void ApplyTableStyle(IXLWorksheet ws, int columns, int lastRow)
    {
        var header = ws.Range(1, 1, 1, columns);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = HeaderFill;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        var table = ws.Range(1, 1, Math.Max(1, lastRow), columns);
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        table.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        if (lastRow > 1) ws.Range(1, 1, lastRow, columns).SetAutoFilter();
        ws.SheetView.FreezeRows(1);
        ws.Columns(1, columns).AdjustToContents(1, Math.Max(1, lastRow), 8, 50);
    }

    /// <summary>Имя листа Excel: не длиннее 31 символа и без символов : \ / ? * [ ].</summary>
    public static string SafeSheetName(string name)
    {
        var clean = new string(name.Where(ch => ":\\/?*[]".IndexOf(ch) < 0).ToArray());
        return clean.Length > 31 ? clean[..31] : clean;
    }
}
