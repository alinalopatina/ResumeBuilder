using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ResumeBuilder.Core.Models;

namespace ResumeBuilder.Infrastructure.Pdf;

/// <summary>Цвета оформления в виде HEX-строк (принимаются любыми версиями QuestPDF).</summary>
public static class Palette
{
    public const string White = "#FFFFFF";
    public const string Black = "#212121";
    public const string Grey = "#757575";
    public const string GreyDark = "#424242";
}

/// <summary>
/// Базовый шаблон PDF-резюме — паттерн <b>«Шаблонный метод» (Template Method)</b>.
/// </summary>
/// <remarks>
/// Метод <see cref="Compose"/> задаёт неизменный «скелет» страницы (размер, поля, колонтитул),
/// а наследники переопределяют шаги <see cref="ComposeContent"/> и <see cref="AccentColor"/>.
/// Общие блоки (опыт, образование, навыки) реализованы здесь один раз и переиспользуются.
/// </remarks>
public abstract class PdfResumeTemplate(ResumeRenderContext ctx) : IDocument
{
    protected ResumeRenderContext Ctx { get; } = ctx;
    protected Resume Resume => Ctx.Resume;

    /// <summary>Основной цвет оформления (переопределяется в наследниках).</summary>
    protected virtual string AccentColor => "#1F4E79";

    /// <inheritdoc />
    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"{Ctx.T("Pdf_Resume")} — {Ctx.DisplayName}",
        Author = Ctx.DisplayName,
        Subject = Resume.Personal.DesiredPosition,
        Creator = "ResumeBuilder",
        Keywords = string.Join(", ", Resume.Skills.Select(s => s.Name))
    };

    /// <inheritdoc />
    public DocumentSettings GetSettings() => new();

    /// <summary>Шаблонный метод: последовательность шагов фиксирована.</summary>
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            ConfigurePage(page);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    /// <summary>Шаг 1: размер страницы, поля, шрифт.</summary>
    protected virtual void ConfigurePage(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(1.5f, Unit.Centimetre);
        page.PageColor(Palette.White);
        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial").LineHeight(1.25f));
    }

    /// <summary>Шаг 2: основное содержимое — уникально для каждого шаблона.</summary>
    protected abstract void ComposeContent(IContainer container);

    /// <summary>Шаг 3: нижний колонтитул с номером страницы.</summary>
    protected virtual void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(t =>
        {
            t.DefaultTextStyle(x => x.FontSize(8).FontColor(Palette.Grey));
            t.Span($"{Ctx.DisplayName} · ");
            t.CurrentPageNumber();
            t.Span(" / ");
            t.TotalPages();
        });
    }

    // ─────────────── общие блоки ───────────────

    protected void SectionTitle(ColumnDescriptor col, string key)
    {
        col.Item().PaddingTop(10).Text(Ctx.T(key).ToUpper(Ctx.Culture)).FontSize(12).Bold().FontColor(AccentColor);
        col.Item().PaddingBottom(4).LineHorizontal(1).LineColor(AccentColor);
    }

    protected void ComposeSummary(ColumnDescriptor col)
    {
        if (string.IsNullOrWhiteSpace(Resume.Summary)) return;
        SectionTitle(col, "Section_Summary");
        col.Item().Text(Resume.Summary);
    }

    protected void ComposeExperience(ColumnDescriptor col)
    {
        if (Resume.Experience.Count == 0) return;
        SectionTitle(col, "Section_Experience");
        foreach (var e in Resume.Experience.OrderByDescending(x => x.StartDate))
        {
            col.Item().PaddingBottom(6).Column(item =>
            {
                item.Item().Row(row =>
                {
                    row.RelativeItem().Text(e.Position).Bold().FontSize(11);
                    row.AutoItem().Text(Ctx.Period(e)).FontColor(Palette.Grey);
                });
                item.Item().Text(t =>
                {
                    t.Span(e.Company).SemiBold().FontColor(AccentColor);
                    if (!string.IsNullOrWhiteSpace(e.City)) t.Span($", {e.City}");
                    t.Span($"  ·  {Ctx.Duration(e)}").FontColor(Palette.Grey).FontSize(9);
                });
                if (!string.IsNullOrWhiteSpace(e.Description))
                    item.Item().PaddingTop(2).Text(e.Description);
            });
        }
    }

    protected void ComposeEducation(ColumnDescriptor col)
    {
        if (Resume.Education.Count == 0) return;
        SectionTitle(col, "Section_Education");
        foreach (var ed in Resume.Education.OrderByDescending(x => x.StartYear))
        {
            col.Item().PaddingBottom(4).Column(item =>
            {
                item.Item().Row(row =>
                {
                    row.RelativeItem().Text(ed.Institution).Bold();
                    row.AutoItem().Text(ed.EndYear is null ? $"{ed.StartYear} — {Ctx.T("Period_Present")}" : $"{ed.StartYear} — {ed.EndYear}")
                        .FontColor(Palette.Grey);
                });
                var details = string.Join(", ", new[] { ed.Degree, ed.Specialty }.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (details.Length > 0) item.Item().Text(details).Italic();
            });
        }
    }

    protected void ComposeSkills(ColumnDescriptor col, string textColor)
    {
        if (Resume.Skills.Count == 0) return;
        foreach (var group in Resume.Skills.GroupBy(s => s.Category).OrderBy(g => g.Key))
        {
            col.Item().PaddingTop(4).Text(Ctx.T($"SkillCategory_{group.Key}")).SemiBold().FontColor(textColor);
            foreach (var s in group.OrderByDescending(x => x.Level))
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text(s.Name).FontColor(textColor);
                    row.AutoItem().Text(ResumeRenderContext.LevelDots(s.Level)).FontSize(8).FontColor(textColor);
                });
            }
        }
    }

    protected void ComposeLanguages(ColumnDescriptor col, string textColor)
    {
        foreach (var l in Resume.Languages)
            col.Item().Text($"{l.Language} — {Ctx.T($"LanguageLevel_{l.Level}")}").FontColor(textColor);
    }

    protected void ComposeContacts(ColumnDescriptor col, string textColor)
    {
        void Line(string labelKey, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            col.Item().Text(t =>
            {
                t.Span($"{Ctx.T(labelKey)}: ").SemiBold().FontColor(textColor);
                t.Span(value).FontColor(textColor);
            });
        }

        Line("Field_Phone", Ctx.Phone);
        Line("Field_Email", Ctx.Email);
        Line("Field_Website", Resume.Personal.Website);
        Line("Field_City", Resume.Personal.City);
        Line("Field_Age", Ctx.Age);
        Line("Field_Salary", Ctx.Salary);
    }

    protected void ComposeQr(IContainer container, string captionColor)
    {
        if (Ctx.QrPng is null) return;
        container.Column(c =>
        {
            c.Item().AlignCenter().Width(95).Image(Ctx.QrPng);
            c.Item().AlignCenter().Text(Ctx.T(Resume.QrMode == QrContentMode.VCard ? "Pdf_QrVCard" : "Pdf_QrWebsite"))
                .FontSize(7).FontColor(captionColor);
        });
    }

    protected void ComposePhoto(IContainer container)
    {
        var path = Resume.Personal.PhotoPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        container.Width(90).Height(110).Image(File.ReadAllBytes(path)).FitArea();
    }
}

/// <summary>Классический одноколоночный шаблон: шапка с QR справа, разделы друг под другом.</summary>
public sealed class ClassicPdfTemplate(ResumeRenderContext ctx) : PdfResumeTemplate(ctx)
{
    protected override string AccentColor => "#2E2E2E";

    protected override void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(2);
            col.Item().Row(row =>
            {
                row.AutoItem().PaddingRight(12).Element(ComposePhoto);
                row.RelativeItem().Column(head =>
                {
                    head.Item().Text(Ctx.DisplayName).FontSize(22).Bold();
                    head.Item().Text(Resume.Personal.DesiredPosition).FontSize(13).FontColor(Palette.GreyDark);
                    head.Item().PaddingTop(6).Column(c => ComposeContacts(c, Palette.Black));
                });
                row.ConstantItem(105).Element(c => ComposeQr(c, Palette.Grey));
            });

            ComposeSummary(col);
            ComposeExperience(col);
            ComposeEducation(col);

            if (Resume.Skills.Count > 0)
            {
                SectionTitle(col, "Section_Skills");
                col.Item().Column(c => ComposeSkills(c, Palette.Black));
            }

            if (Resume.Languages.Count > 0)
            {
                SectionTitle(col, "Section_Languages");
                col.Item().Column(c => ComposeLanguages(c, Palette.Black));
            }
        });
    }
}

/// <summary>Современный шаблон: цветная боковая колонка (контакты, навыки, языки, QR) и основная колонка.</summary>
public sealed class ModernPdfTemplate(ResumeRenderContext ctx) : PdfResumeTemplate(ctx)
{
    protected override string AccentColor => "#1F4E79";

    protected override void ComposeContent(IContainer container)
    {
        container.Row(row =>
        {
            // Боковая колонка
            row.ConstantItem(170).Background(AccentColor).Padding(12).Column(side =>
            {
                side.Spacing(3);
                side.Item().AlignCenter().Element(ComposePhoto);
                side.Item().PaddingTop(4).Text(Ctx.T("Section_Contacts").ToUpper(Ctx.Culture)).Bold().FontColor(Palette.White);
                side.Item().Column(c => ComposeContacts(c, Palette.White));

                if (Resume.Skills.Count > 0)
                {
                    side.Item().PaddingTop(10).Text(Ctx.T("Section_Skills").ToUpper(Ctx.Culture)).Bold().FontColor(Palette.White);
                    side.Item().Column(c => ComposeSkills(c, Palette.White));
                }

                if (Resume.Languages.Count > 0)
                {
                    side.Item().PaddingTop(10).Text(Ctx.T("Section_Languages").ToUpper(Ctx.Culture)).Bold().FontColor(Palette.White);
                    side.Item().Column(c => ComposeLanguages(c, Palette.White));
                }

                if (Ctx.QrPng is not null)
                    side.Item().PaddingTop(14).Background(Palette.White).Padding(6).Element(c => ComposeQr(c, Palette.GreyDark));
            });

            // Основная колонка
            row.RelativeItem().PaddingLeft(16).Column(main =>
            {
                main.Spacing(2);
                main.Item().Text(Ctx.DisplayName).FontSize(24).Bold().FontColor(AccentColor);
                main.Item().Text(Resume.Personal.DesiredPosition).FontSize(13).FontColor(Palette.GreyDark);
                ComposeSummary(main);
                ComposeExperience(main);
                ComposeEducation(main);
            });
        });
    }
}
