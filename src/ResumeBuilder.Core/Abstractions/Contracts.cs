using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Reports;

namespace ResumeBuilder.Core.Abstractions;

/// <summary>
/// Сериализатор резюме (паттерн «Стратегия»: JSON или собственный двоичный формат).
/// </summary>
public interface IResumeSerializer
{
    /// <summary>Название формата для интерфейса.</summary>
    string FormatName { get; }

    /// <summary>Расширение файла с точкой, например <c>.resume.json</c>.</summary>
    string FileExtension { get; }

    /// <summary>Записывает резюме в поток.</summary>
    void Serialize(Resume resume, Stream output);

    /// <summary>Читает резюме из потока.</summary>
    /// <exception cref="InvalidDataException">Файл повреждён или имеет неизвестную версию формата.</exception>
    Resume Deserialize(Stream input);
}

/// <summary>Параметры экспорта резюме.</summary>
public sealed record ExportOptions
{
    /// <summary>Добавлять QR-код.</summary>
    public bool IncludeQrCode { get; init; } = true;

    /// <summary>«Публичная» версия: e-mail и телефон частично скрыты (маскирование регулярными выражениями).</summary>
    public bool MaskContacts { get; init; }

    /// <summary>Язык заголовков документа; <c>null</c> — взять из <see cref="Resume.CultureName"/>.</summary>
    public string? CultureName { get; init; }

    /// <summary>Параметры по умолчанию.</summary>
    public static ExportOptions Default { get; } = new();
}

/// <summary>Экспортёр резюме в конкретный формат (PDF, DOCX, XLSX) — паттерн «Стратегия».</summary>
public interface IResumeExporter
{
    /// <summary>Название формата.</summary>
    string FormatName { get; }

    /// <summary>Расширение файла с точкой.</summary>
    string FileExtension { get; }

    /// <summary>Экспортирует резюме в поток.</summary>
    void Export(Resume resume, Stream output, ExportOptions options);
}

/// <summary>Генератор изображения QR-кода.</summary>
public interface IQrCodeGenerator
{
    /// <summary>Возвращает PNG-изображение QR-кода с указанным содержимым.</summary>
    byte[] GeneratePng(string payload, int pixelsPerModule = 10);
}

/// <summary>
/// Отрисовщик сводного отчёта в конкретный формат. Получает уже посчитанный
/// <see cref="ReportDocument"/> — поэтому логика агрегации не дублируется (лаба «Excel/Word», модуль 3).
/// </summary>
public interface IReportRenderer
{
    /// <summary>Название формата.</summary>
    string FormatName { get; }

    /// <summary>Расширение файла с точкой.</summary>
    string FileExtension { get; }

    /// <summary>Отрисовывает отчёт в поток.</summary>
    void Render(ReportDocument report, Stream output);
}

/// <summary>Импорт мест работы из внешнего документа («обратная задача»).</summary>
public interface IExperienceImporter
{
    /// <summary>Читает места работы из потока.</summary>
    ImportResult<WorkExperience> Import(Stream input);
}

/// <summary>Результат импорта: успешно прочитанные элементы и ошибки по строкам.</summary>
public sealed class ImportResult<T>
{
    /// <summary>Прочитанные элементы.</summary>
    public List<T> Items { get; } = [];

    /// <summary>Ошибки разбора (номер строки + описание).</summary>
    public List<string> Errors { get; } = [];
}

/// <summary>Журнал аудита действий пользователя (отдельный файл, своя политика хранения).</summary>
public interface IAuditLogger
{
    /// <summary>Записывает действие пользователя.</summary>
    /// <param name="action">Действие (Open, Save, ExportPdf...).</param>
    /// <param name="target">Объект действия (имя файла, без персональных данных).</param>
    /// <param name="success">Успешность.</param>
    void Log(string action, string? target, bool success);
}
