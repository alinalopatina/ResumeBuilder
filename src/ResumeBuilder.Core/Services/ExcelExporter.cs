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
    private static readonly XLColor HeaderColor = XLColor.FromHtml("#D9E2F3");
    private static readonly XLColor TitleColor = XLColor.FromHtml("#1F4E79");

    /// <summary>
    /// Резюме в Excel, четыре листа: «Резюме» (личные данные и итоги),
    /// «Опыт работы», «Образование», «Навыки».
    /// </summary>
    public static void ExportResume(Resume resume, string path)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Author = resume.Personal.FullName;
        workbook.Properties.Title = Messages.Section_Resume;

        AddSummarySheet(workbook, resume);
        AddExperienceSheet(workbook, resume);
        AddEducationSheet(workbook, resume);
        AddSkillsSheet(workbook, resume);

        workbook.SaveAs(path);
        Log.Information("Excel сохранён: {File}", Path.GetFileName(path));
    }

    // ---------- Лист 1: личные данные и итоги ----------

    private static void AddSummarySheet(XLWorkbook workbook, Resume resume)
    {
        IXLWorksheet sheet = workbook.Worksheets.Add(Messages.Sheet_Summary);
        PersonalInfo p = resume.Personal;

        sheet.Cell(1, 1).Value = p.FullName;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(1, 1).Style.Font.FontColor = TitleColor;
        sheet.Cell(2, 1).Value = p.Position;
        sheet.Cell(2, 1).Style.Font.Italic = true;

        sheet.Cell(4, 1).Value = Messages.Column_Field;
        sheet.Cell(4, 2).Value = Messages.Column_Value;

        int row = 5;
        AddPair(sheet, ref row, Messages.Field_Email, p.Email);
        AddPair(sheet, ref row, Messages.Field_Phone, TextHelper.NormalizePhone(p.Phone));
        AddPair(sheet, ref row, Messages.Field_City, p.City);
        if (p.BirthDate != null)
        {
            sheet.Cell(row, 1).Value = Messages.Field_BirthDate;
            sheet.Cell(row, 2).Value = p.BirthDate.Value;
            sheet.Cell(row, 2).Style.DateFormat.Format = "dd.mm.yyyy";
            row++;
            AddPair(sheet, ref row, Messages.Field_Age, p.GetAge(DateTime.Today).ToString() ?? "");
        }
        AddPair(sheet, ref row, Messages.Field_Website, p.Website ?? "");

        // Итоги считаются формулами по другим листам — если поменять данные в Excel, итоги пересчитаются
        int jobs = resume.Experience.Count;
        sheet.Cell(row, 1).Value = Messages.Report_JobsCount;
        if (jobs > 0)
            sheet.Cell(row, 2).FormulaA1 = $"COUNTA('{Messages.Section_Experience}'!B2:B{jobs + 1})";
        else
            sheet.Cell(row, 2).Value = 0;
        row++;
        sheet.Cell(row, 1).Value = Messages.Report_TotalMonths;
        if (jobs > 0)
            sheet.Cell(row, 2).FormulaA1 = $"SUM('{Messages.Section_Experience}'!G2:G{jobs + 1})";
        else
            sheet.Cell(row, 2).Value = 0;
        row++;
        sheet.Cell(row, 1).Value = Messages.Report_SkillsCount;
        sheet.Cell(row, 2).Value = resume.Skills.Count;
        row++;
        sheet.Cell(row, 1).Value = Messages.Report_AverageSkill;
        if (resume.Skills.Count > 0)
            sheet.Cell(row, 2).FormulaA1 = $"ROUND(AVERAGE('{Messages.Section_Skills}'!C2:C{resume.Skills.Count + 1}),2)";
        else
            sheet.Cell(row, 2).Value = 0;
        row++;
        AddPair(sheet, ref row, Messages.Report_Completeness, ReportCalculator.CalculateCompleteness(resume) + " %");

        FormatTable(sheet, 4, row - 1, 2);
        sheet.Range(5, 1, row - 1, 1).Style.Font.Bold = true;

        // «О себе» — отдельным блоком с переносом строк
        row++;
        sheet.Cell(row, 1).Value = Messages.Section_About;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        row++;
        sheet.Cell(row, 1).Value = resume.About;
        sheet.Range(row, 1, row, 2).Merge();
        sheet.Range(row, 1, row, 2).Style.Alignment.WrapText = true;
        sheet.Range(row, 1, row, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        sheet.Row(row).Height = 60;

        sheet.Column(1).Width = 32;
        sheet.Column(2).Width = 45;
    }

    // ---------- Лист 2: опыт работы ----------

    private static void AddExperienceSheet(XLWorkbook workbook, Resume resume)
    {
        IXLWorksheet sheet = workbook.Worksheets.Add(Messages.Section_Experience);
        string[] headers =
        {
            "№", Messages.Column_Company, Messages.Column_Position, Messages.Column_Start,
            Messages.Column_End, Messages.Column_Current, Messages.Column_Months, Messages.Column_Years, Messages.Column_Description
        };
        WriteHeaders(sheet, headers);

        int row = 2;
        foreach (WorkExperience e in resume.Experience.OrderByDescending(x => x.StartDate))
        {
            sheet.Cell(row, 1).Value = row - 1;
            sheet.Cell(row, 2).Value = e.Company;
            sheet.Cell(row, 3).Value = e.Position;
            sheet.Cell(row, 4).Value = e.StartDate;
            if (e.EndDate != null)
                sheet.Cell(row, 5).Value = e.EndDate.Value;
            else
                sheet.Cell(row, 5).Value = Messages.Present;
            sheet.Cell(row, 6).Value = e.EndDate == null ? Messages.Yes : Messages.No;
            sheet.Cell(row, 7).Value = e.GetMonths(DateTime.Today);
            sheet.Cell(row, 8).FormulaA1 = $"ROUND(G{row}/12,1)";   // лет = месяцев / 12
            sheet.Cell(row, 9).Value = e.Description;
            row++;
        }
        int lastData = row - 1;

        if (resume.Experience.Count > 0)
        {
            sheet.Range(2, 4, lastData, 5).Style.DateFormat.Format = "mm.yyyy";

            // Строки итогов — формулами
            sheet.Cell(row, 3).Value = Messages.Report_Total;
            sheet.Cell(row, 7).FormulaA1 = $"SUM(G2:G{lastData})";
            sheet.Cell(row, 8).FormulaA1 = $"ROUND(G{row}/12,1)";
            sheet.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
            row++;
            sheet.Cell(row, 3).Value = Messages.Report_Average;
            sheet.Cell(row, 7).FormulaA1 = $"ROUND(AVERAGE(G2:G{lastData}),1)";
            sheet.Range(row, 1, row, headers.Length).Style.Font.Italic = true;
            row++;
        }

        FormatTable(sheet, 1, row - 1, headers.Length);
        if (resume.Experience.Count > 0)
            sheet.Range(1, 1, lastData, headers.Length).SetAutoFilter();
        sheet.Column(9).Width = 60;
        sheet.Column(9).Style.Alignment.WrapText = true;
    }

    // ---------- Лист 3: образование ----------

    private static void AddEducationSheet(XLWorkbook workbook, Resume resume)
    {
        IXLWorksheet sheet = workbook.Worksheets.Add(Messages.Section_Education);
        string[] headers =
        {
            "№", Messages.Column_Institution, Messages.Column_Specialty,
            Messages.Column_StartYear, Messages.Column_EndYear, Messages.Column_Status
        };
        WriteHeaders(sheet, headers);

        int row = 2;
        foreach (Education ed in resume.Education.OrderByDescending(x => x.StartYear))
        {
            sheet.Cell(row, 1).Value = row - 1;
            sheet.Cell(row, 2).Value = ed.Institution;
            sheet.Cell(row, 3).Value = ed.Specialty;
            sheet.Cell(row, 4).Value = ed.StartYear;
            if (ed.EndYear != null)
                sheet.Cell(row, 5).Value = ed.EndYear.Value;
            bool finished = ed.EndYear != null && ed.EndYear <= DateTime.Today.Year;
            sheet.Cell(row, 6).Value = finished ? Messages.Status_Finished : Messages.Status_Studying;
            row++;
        }

        FormatTable(sheet, 1, row - 1, headers.Length);
        sheet.Column(3).Width = 50;
        sheet.Column(3).Style.Alignment.WrapText = true;
    }

    // ---------- Лист 4: навыки ----------

    private static void AddSkillsSheet(XLWorkbook workbook, Resume resume)
    {
        IXLWorksheet sheet = workbook.Worksheets.Add(Messages.Section_Skills);
        string[] headers = { "№", Messages.Column_Skill, Messages.Column_Level, Messages.Column_Scale };
        WriteHeaders(sheet, headers);

        int row = 2;
        foreach (Skill s in resume.Skills.OrderByDescending(x => x.Level).ThenBy(x => x.Name))
        {
            sheet.Cell(row, 1).Value = row - 1;
            sheet.Cell(row, 2).Value = s.Name;
            sheet.Cell(row, 3).Value = s.Level;
            sheet.Cell(row, 4).Value = ResumeReport.SkillBar(s.Level);
            row++;
        }
        int lastData = row - 1;

        if (resume.Skills.Count > 0)
        {
            // Цветная полоска в ячейке по уровню навыка (условное форматирование)
            sheet.Range(2, 3, lastData, 3).AddConditionalFormat()
                .DataBar(XLColor.FromHtml("#5B9BD5")).LowestValue().HighestValue();

            sheet.Cell(row, 2).Value = Messages.Report_Average;
            sheet.Cell(row, 3).FormulaA1 = $"ROUND(AVERAGE(C2:C{lastData}),2)";
            sheet.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
            row++;
        }

        FormatTable(sheet, 1, row - 1, headers.Length);
        sheet.Column(4).Style.Font.FontColor = TitleColor;
    }

    // ---------- Сводный отчёт ----------

    /// <summary>Сводный отчёт: получает уже посчитанные данные, только рисует их.</summary>
    public static void ExportReport(ResumeReport report, string path)
    {
        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.Worksheets.Add(Messages.Report_Sheet);

        sheet.Cell(1, 1).Value = Messages.Report_Title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(1, 1).Style.Font.FontColor = TitleColor;
        sheet.Cell(2, 1).Value = $"{report.FullName} — {report.Position}";
        sheet.Cell(3, 1).Value = $"{Messages.Report_Created}: {report.CreatedAt:g}";
        sheet.Cell(3, 1).Style.Font.Italic = true;

        // Таблица показателей
        int row = 5;
        sheet.Cell(row, 1).Value = Messages.Column_Field;
        sheet.Cell(row, 2).Value = Messages.Column_Value;
        int summaryStart = row;
        row++;
        foreach (var (name, value) in report.GetSummary())
        {
            sheet.Cell(row, 1).Value = name;
            sheet.Cell(row, 2).Value = value;
            row++;
        }
        FormatTable(sheet, summaryStart, row - 1, 2, freeze: false);

        // Таблица по местам работы
        row++;
        int jobsStart = row;
        string[] headers = { Messages.Column_Company, Messages.Column_Position, Messages.Column_Start, Messages.Column_End, Messages.Column_Months, Messages.Column_Share };
        for (int i = 0; i < headers.Length; i++)
            sheet.Cell(row, i + 1).Value = headers[i];
        row++;
        int firstJob = row;
        foreach (ReportRow r in report.Rows)
        {
            sheet.Cell(row, 1).Value = r.Company;
            sheet.Cell(row, 2).Value = r.Position;
            sheet.Cell(row, 3).Value = r.Start;
            if (r.End != null)
                sheet.Cell(row, 4).Value = r.End.Value;
            else
                sheet.Cell(row, 4).Value = Messages.Present;
            sheet.Cell(row, 5).Value = r.Months;
            sheet.Cell(row, 6).Value = r.SharePercent / 100;   // хранится как доля, отображается в %
            row++;
        }
        if (report.Rows.Count > 0)
        {
            sheet.Range(firstJob, 3, row - 1, 4).Style.DateFormat.Format = "mm.yyyy";
            sheet.Cell(row, 1).Value = Messages.Report_Total;
            sheet.Cell(row, 5).FormulaA1 = $"SUM(E{firstJob}:E{row - 1})";
            sheet.Cell(row, 6).FormulaA1 = $"SUM(F{firstJob}:F{row - 1})";
            sheet.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
            sheet.Range(firstJob, 6, row, 6).Style.NumberFormat.Format = "0.0%";
            row++;
        }
        FormatTable(sheet, jobsStart, row - 1, headers.Length, freeze: false);

        // Распределение навыков по уровням
        row++;
        int skillsStart = row;
        sheet.Cell(row, 1).Value = Messages.Column_Level;
        sheet.Cell(row, 2).Value = Messages.Report_SkillsCount;
        row++;
        for (int level = 5; level >= 1; level--)
        {
            sheet.Cell(row, 1).Value = ResumeReport.SkillBar(level);
            sheet.Cell(row, 2).Value = report.SkillsByLevel[level - 1];
            row++;
        }
        FormatTable(sheet, skillsStart, row - 1, 2, freeze: false);

        // Вывод
        row++;
        sheet.Cell(row, 1).Value = Messages.Report_Conclusion;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        row++;
        sheet.Cell(row, 1).Value = report.Conclusion;
        sheet.Range(row, 1, row, headers.Length).Merge();
        sheet.Range(row, 1, row, headers.Length).Style.Alignment.WrapText = true;
        sheet.Row(row).Height = 45;

        sheet.Column(1).Width = 40;
        sheet.Columns(2, headers.Length).AdjustToContents();
        workbook.SaveAs(path);
        Log.Information("Отчёт Excel сохранён: {File}", Path.GetFileName(path));
    }

    // ---------- вспомогательные методы ----------

    private static void WriteHeaders(IXLWorksheet sheet, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
    }

    private static void AddPair(IXLWorksheet sheet, ref int row, string name, string value)
    {
        sheet.Cell(row, 1).Value = name;
        sheet.Cell(row, 2).Value = value;
        row++;
    }

    /// <summary>Жирная шапка с заливкой, границы, закреплённая строка шапки, ширина по содержимому.</summary>
    private static void FormatTable(IXLWorksheet sheet, int headerRow, int lastRow, int columns, bool freeze = true)
    {
        IXLRange header = sheet.Range(headerRow, 1, headerRow, columns);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = HeaderColor;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        IXLRange all = sheet.Range(headerRow, 1, Math.Max(lastRow, headerRow), columns);
        all.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        all.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        all.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        if (freeze)
        {
            sheet.SheetView.FreezeRows(headerRow);
            sheet.Columns(1, columns).AdjustToContents(headerRow, Math.Max(lastRow, headerRow), 6, 40);
        }
    }
}
