using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Models;

namespace ResumeBuilder.Infrastructure.Serialization;

/// <summary>
/// JSON-сериализация резюме (System.Text.Json). Основной формат файлов <c>*.resume.json</c>:
/// читаем человеком, переносим между платформами, безопасен (не исполняет код при чтении).
/// </summary>
public sealed class JsonResumeSerializer : IResumeSerializer
{
    /// <summary>Настройки: отступы, кириллица без \u-экранирования, перечисления строками.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <inheritdoc />
    public string FormatName => "JSON";

    /// <inheritdoc />
    public string FileExtension => ".resume.json";

    /// <inheritdoc />
    public void Serialize(Resume resume, Stream output)
    {
        ArgumentNullException.ThrowIfNull(resume);
        resume.UpdatedAt = DateTime.Now;
        resume.SchemaVersion = Resume.CurrentSchemaVersion;
        JsonSerializer.Serialize(output, resume, Options);
    }

    /// <inheritdoc />
    public Resume Deserialize(Stream input)
    {
        Resume? resume;
        try
        {
            resume = JsonSerializer.Deserialize<Resume>(input, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Файл не является корректным JSON-резюме: {ex.Message}", ex);
        }

        if (resume is null) throw new InvalidDataException("Файл пуст.");
        if (resume.SchemaVersion > Resume.CurrentSchemaVersion)
            throw new InvalidDataException(
                $"Файл создан более новой версией программы (схема {resume.SchemaVersion}, поддерживается до {Resume.CurrentSchemaVersion}).");

        // Новые поля, которых не было в старом файле, получают значения по умолчанию из конструктора —
        // так JSON естественно решает проблему версионности.
        resume.Personal ??= new PersonalInfo();
        return resume;
    }
}
