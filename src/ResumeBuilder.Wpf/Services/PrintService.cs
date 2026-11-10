using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Services;
using Res = ResumeBuilder.Wpf.Properties.Resources;

namespace ResumeBuilder.Wpf.Services;

/// <summary>
/// Печать резюме: 1) готовим документ для бумаги (FlowDocument),
/// 2) показываем стандартный диалог выбора принтера, 3) отправляем на печать.
/// </summary>
public static class PrintService
{
    public static bool Print(Resume resume)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
            return false;

        FlowDocument document = CreateDocument(resume);
        document.PageWidth = dialog.PrintableAreaWidth;
        document.PageHeight = dialog.PrintableAreaHeight;
        document.PagePadding = new Thickness(50);
        document.ColumnWidth = double.PositiveInfinity;

        IDocumentPaginatorSource source = document;
        dialog.PrintDocument(source.DocumentPaginator, Res.App_Title);
        return true;
    }

    private static FlowDocument CreateDocument(Resume resume)
    {
        var doc = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 12 };
        PersonalInfo p = resume.Personal;

        doc.Blocks.Add(new Paragraph(new Run(p.FullName)) { FontSize = 22, FontWeight = FontWeights.Bold });
        doc.Blocks.Add(new Paragraph(new Run(p.Position)) { FontSize = 15 });
        doc.Blocks.Add(new Paragraph(new Run($"{TextHelper.NormalizePhone(p.Phone)}   {p.Email}   {p.City}")));

        AddHeader(doc, Res.Tab_About);
        doc.Blocks.Add(new Paragraph(new Run(resume.About)));

        AddHeader(doc, Res.Tab_Experience);
        foreach (WorkExperience e in resume.Experience)
            doc.Blocks.Add(new Paragraph(new Run(e.GetDescription() + "\n" + e.Description)));

        AddHeader(doc, Res.Tab_Education);
        foreach (Education ed in resume.Education)
            doc.Blocks.Add(new Paragraph(new Run(ed.GetDescription())));

        AddHeader(doc, Res.Tab_Skills);
        doc.Blocks.Add(new Paragraph(new Run(string.Join(", ", resume.Skills.Select(s => s.GetDescription())))));

        return doc;
    }

    private static void AddHeader(FlowDocument doc, string text)
    {
        doc.Blocks.Add(new Paragraph(new Run(text)) { FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 12, 0, 4) });
    }
}
