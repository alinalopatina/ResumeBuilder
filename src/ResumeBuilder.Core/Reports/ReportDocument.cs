namespace ResumeBuilder.Core.Reports;

/// <summary>
/// Формат-независимое представление отчёта: заголовок, разделы, таблицы и вывод.
/// Один и тот же объект отрисовывается в Excel, Word, PDF и текст — каждый рендерер
/// знает только, <i>как</i> рисовать, но не <i>что</i> считать.
/// </summary>
public sealed class ReportDocument
{
    /// <summary>Заголовок отчёта.</summary>
    public required string Title { get; init; }

    /// <summary>Культура, по правилам которой форматируются числа в ячейках.</summary>
    public string CultureName { get; init; } = "ru-RU";

    /// <summary>Подзаголовок (например, ФИО кандидата).</summary>
    public string? Subtitle { get; init; }

    /// <summary>Подпись «Сформировано: ...» (уже отформатированная по культуре).</summary>
    public string? GeneratedLabel { get; init; }

    /// <summary>Разделы отчёта.</summary>
    public List<ReportSection> Sections { get; } = [];

    /// <summary>Заголовок блока с выводом.</summary>
    public string ConclusionTitle { get; init; } = "Вывод";

    /// <summary>Текстовый вывод по результатам.</summary>
    public string? Conclusion { get; init; }
}

/// <summary>Раздел отчёта: заголовок + пары «показатель — значение» и/или таблица.</summary>
public sealed class ReportSection
{
    /// <summary>Заголовок раздела.</summary>
    public required string Heading { get; init; }

    /// <summary>Показатели «название — значение».</summary>
    public List<KeyValuePair<string, string>> Metrics { get; } = [];

    /// <summary>Таблица (необязательно).</summary>
    public ReportTable? Table { get; init; }
}

/// <summary>
/// Таблица отчёта. Ячейки — <see cref="object"/>: числа хранятся как <see cref="double"/>,
/// чтобы в Excel они остались числами (можно считать формулами), а не текстом.
/// </summary>
public sealed class ReportTable
{
    /// <summary>Заголовки столбцов.</summary>
    public required IReadOnlyList<string> Headers { get; init; }

    /// <summary>Строки данных.</summary>
    public List<object?[]> Rows { get; } = [];

    /// <summary>Итоговая строка (необязательно).</summary>
    public object?[]? Totals { get; set; }

    /// <summary>Формат для чисел с дробной частью (например, <c>0.0</c>).</summary>
    public string NumberFormat { get; init; } = "0.##";

    /// <summary>Текстовое представление ячейки (для Word, PDF и консоли; Excel получает «сырое» число).</summary>
    public string FormatCell(object? value, System.Globalization.CultureInfo culture) => value switch
    {
        null => string.Empty,
        double d when d == Math.Floor(d) => d.ToString("0", culture),
        double d => d.ToString(NumberFormat, culture),
        IFormattable f => f.ToString(null, culture),
        _ => value.ToString() ?? string.Empty
    };
}
