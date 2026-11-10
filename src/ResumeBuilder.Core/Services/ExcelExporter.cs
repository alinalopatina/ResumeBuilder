using ClosedXML.Excel;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Resources;
using Serilog;

namespace ResumeBuilder.Core.Services;

/// <summary>
/// Экспорт в Excel через ClosedXML.
/// Interop.Excel не используется: ему нужен установленный Excel, и он не работает в Docker.
/// </summary>
public static class ExcelExporter
{
    /// <summary>Таблица мест работы: жирная шапка, границы, закреплённая первая строка.</summary>
    public static void ExportExperience(Resume resume, string path)
    {
        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.Worksheets.Add(Messages.Section_Experience);

        string[] headers = { Messages.Column_Company, Messages.Column_Position, Messages.Column_Start, Messages.Column_End, Messages.Column_Months, Messages.Column_Description };
        for (int i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];

        int row = 2;
        foreach (WorkExperience e in resume.Experience)
        {
            sheet.Cell(row, 1).Value = e.Company;
            sheet.Cell(row, 2).Value = e.Position;
            sheet.Cell(row, 3).Value = e.StartDate;
            if (e.EndDate != null)
                sheet.Cell(row, 4).Value = e.EndDate.Value;
            else
                sheet.Cell(row, 4).Value = Messages.Present;
            sheet.Cell(row, 5).Value = e.GetMonths(DateTime.Today);
            sheet.Cell(row, 6).Value = e.Description;
            row++;
        }
        sheet.Range(2, 3, Math.Max(row - 1, 2), 4).Style.DateFormat.Format = "mm.yyyy";

        FormatTable(sheet, headers.Length, row - 1);
        workbook.SaveAs(path);
        Log.Information("Excel сохранён: {File}", Path.GetFileName(path));
    }

    /// <summary>Сводный отчёт: получает уже посчитанные данные, только рисует их.</summary>
    public static void ExportReport(ResumeReport report, string path)
    {
        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.Worksheets.Add(Messages.Report_Sheet);

        sheet.Cell(1, 1).Value = Messages.Report_Title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = report.FullName;

        int row = 4;
        foreach (var (name, value) in report.GetSummary())
        {
            sheet.Cell(row, 1).Value = name;
            sheet.Cell(row, 2).Value = value;
            row++;
        }

        row++;
        int tableStart = row;
        sheet.Cell(row, 1).Value = Messages.Column_Company;
        sheet.Cell(row, 2).Value = Messages.Column_Position;
        sheet.Cell(row, 3).Value = Messages.Column_Months;
        sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
        sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.LightGray;
        row++;
        foreach (ReportRow r in report.Rows)
        {
            sheet.Cell(row, 1).Value = r.Company;
            sheet.Cell(row, 2).Value = r.Position;
            sheet.Cell(row, 3).Value = r.Months;
            row++;
        }
        var table = sheet.Range(tableStart, 1, row - 1, 3);
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(path);
    }

    private static void FormatTable(IXLWorksheet sheet, int columns, int lastRow)
    {
        IXLRange header = sheet.Range(1, 1, 1, columns);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;

        IXLRange all = sheet.Range(1, 1, Math.Max(lastRow, 1), columns);
        all.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        all.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }
}
