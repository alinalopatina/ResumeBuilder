using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ResumeBuilder.Core.Localization;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Text;

namespace ResumeBuilder.Wpf.Services;

/// <summary>
/// Печать резюме на принтер (лаба «Вывод данных на печать»).
/// </summary>
/// <remarks>
/// Три этапа печати из методички:
/// <list type="number">
/// <item><b>Подготовка макета</b> — строится <see cref="FlowDocument"/> специально для бумаги
/// (белый фон, без кнопок и полей ввода — интерфейс не печатаем);</item>
/// <item><b>Системный диалог</b> — <see cref="PrintDialog"/>, пользователь сам выбирает принтер;</item>
/// <item><b>Передача на принтер</b> — <see cref="PrintDialog.PrintDocument"/> с пагинатором документа.</item>
/// </list>
/// </remarks>
public sealed class PrintService(ILocalizer localizer)
{
    /// <summary>Показывает диалог печати и печатает резюме. Возвращает <c>false</c>, если пользователь отменил.</summary>
    public bool Print(Resume resume)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return false;

        var document = BuildDocument(resume);
        document.PageHeight = dialog.PrintableAreaHeight;
        document.PageWidth = dialog.PrintableAreaWidth;
        document.PagePadding = new Thickness(60);
        document.ColumnWidth = double.PositiveInfinity; // одна колонка на всю ширину

        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        dialog.PrintDocument(paginator, $"{localizer["Pdf_Resume"]} — {resume.Personal.FullName}");
        return true;
    }

    /// <summary>Строит макет для печати.</summary>
    public FlowDocument BuildDocument(Resume resume)
    {
        var culture = CultureInfo.GetCultureInfo(resume.CultureName);
        string T(string key) => localizer.Get(key, culture);
        var accent = new SolidColorBrush(Color.FromRgb(0x1F, 0x4E, 0x79));

        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            Background = Brushes.White,
            Foreground = Brushes.Black
        };

        doc.Blocks.Add(new Paragraph(new Run(resume.Personal.FullName)) { FontSize = 24, FontWeight = FontWeights.Bold, Foreground = accent, Margin = new Thickness(0) });
        doc.Blocks.Add(new Paragraph(new Run(resume.Personal.DesiredPosition)) { FontSize = 15, Foreground = Brushes.DimGray });

        var contacts = new Paragraph { Margin = new Thickness(0, 4, 0, 8) };
        void Contact(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            contacts.Inlines.Add(new Bold(new Run($"{T(key)}: ")));
            contacts.Inlines.Add(new Run(value));
            contacts.Inlines.Add(new LineBreak());
        }
        Contact("Field_Phone", TextFormatter.NormalizePhone(resume.Personal.Phone));
        Contact("Field_Email", resume.Personal.Email);
        Contact("Field_Website", resume.Personal.Website);
        Contact("Field_City", resume.Personal.City);
        doc.Blocks.Add(contacts);

        void AddSection(string key) => doc.Blocks.Add(new Paragraph(new Run(T(key).ToUpper(culture)))
        {
            FontSize = 14, FontWeight = FontWeights.Bold, Foreground = accent,
            BorderBrush = accent, BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 12, 0, 4)
        });

        if (!string.IsNullOrWhiteSpace(resume.Summary))
        {
            AddSection("Section_Summary");
            doc.Blocks.Add(new Paragraph(new Run(resume.Summary)) { TextAlignment = TextAlignment.Justify });
        }

        if (resume.Experience.Count > 0)
        {
            AddSection("Section_Experience");
            foreach (var e in resume.Experience.OrderByDescending(x => x.StartDate))
            {
                var p = new Paragraph { Margin = new Thickness(0, 0, 0, 6) };
                p.Inlines.Add(new Bold(new Run(e.Position)));
                p.Inlines.Add(new Run($"  ·  {CultureFormatter.FormatPeriod(e.StartDate, e.EndDate, localizer, culture)}") { Foreground = Brushes.DimGray });
                p.Inlines.Add(new LineBreak());
                p.Inlines.Add(new Run(e.Company) { Foreground = accent });
                if (!string.IsNullOrWhiteSpace(e.Description))
                {
                    p.Inlines.Add(new LineBreak());
                    p.Inlines.Add(new Run(e.Description));
                }
                doc.Blocks.Add(p);
            }
        }

        if (resume.Education.Count > 0)
        {
            AddSection("Section_Education");
            var list = new List { MarkerStyle = TextMarkerStyle.Disc };
            foreach (var ed in resume.Education)
                list.ListItems.Add(new ListItem(new Paragraph(new Run(ed.GetSummary(culture)))));
            doc.Blocks.Add(list);
        }

        if (resume.Skills.Count > 0)
        {
            AddSection("Section_Skills");
            // Таблица навыков: название | категория | уровень
            var table = new Table { CellSpacing = 0, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0.5) };
            table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var group = new TableRowGroup();
            foreach (var s in resume.Skills.OrderBy(x => x.Category).ThenByDescending(x => x.Level))
            {
                var row = new TableRow();
                row.Cells.Add(new TableCell(new Paragraph(new Run(s.Name))) { Padding = new Thickness(4, 2, 4, 2) });
                row.Cells.Add(new TableCell(new Paragraph(new Run(T($"SkillCategory_{s.Category}")))) { Padding = new Thickness(4, 2, 4, 2) });
                row.Cells.Add(new TableCell(new Paragraph(new Run(new string('●', Math.Clamp(s.Level, 0, Skill.MaxLevel)) + new string('○', Skill.MaxLevel - Math.Clamp(s.Level, 0, Skill.MaxLevel))))) { Padding = new Thickness(4, 2, 4, 2) });
                group.Rows.Add(row);
            }
            table.RowGroups.Add(group);
            doc.Blocks.Add(table);
        }

        if (resume.Languages.Count > 0)
        {
            AddSection("Section_Languages");
            doc.Blocks.Add(new Paragraph(new Run(string.Join("; ",
                resume.Languages.Select(l => $"{l.Language} — {T($"LanguageLevel_{l.Level}")}")))));
        }

        return doc;
    }
}
