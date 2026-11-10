using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Services;

namespace ResumeBuilder.Infrastructure.Serialization;

/// <summary>
/// Открытие и сохранение файлов резюме. Выбирает сериализатор по расширению файла
/// (паттерн «Фабрика» + «Стратегия») и пишет в журнал/аудит.
/// </summary>
public sealed class ResumeFileService(
    IEnumerable<IResumeSerializer> serializers,
    ILogger<ResumeFileService> logger,
    IAuditLogger audit)
{
    private readonly IReadOnlyList<IResumeSerializer> _serializers = serializers.ToList();

    /// <summary>Фильтр для диалогов открытия/сохранения.</summary>
    public string DialogFilter =>
        string.Join("|", _serializers.Select(s => $"{s.FormatName} (*{s.FileExtension})|*{s.FileExtension}"));

    /// <summary>Подбирает сериализатор по имени файла.</summary>
    public IResumeSerializer GetSerializer(string path) =>
        _serializers.FirstOrDefault(s => path.EndsWith(s.FileExtension, StringComparison.OrdinalIgnoreCase))
        ?? _serializers.FirstOrDefault(s => s.FileExtension.EndsWith(Path.GetExtension(path), StringComparison.OrdinalIgnoreCase))
        ?? throw new NotSupportedException($"Неизвестный формат файла: {Path.GetFileName(path)}");

    /// <summary>Сохраняет резюме в файл. Запись идёт во временный файл и затем атомарно заменяет старый.</summary>
    public void Save(Resume resume, string path)
    {
        using var op = OperationTimer.Start(logger, "SaveResume");
        var serializer = GetSerializer(path);
        var tmp = path + ".tmp";
        try
        {
            using (var fs = File.Create(tmp)) serializer.Serialize(resume, fs);
            File.Move(tmp, path, overwrite: true);
            resume.FilePath = path;
            logger.LogInformation("Резюме сохранено в {FileName} ({Format}, {Bytes} байт)",
                Path.GetFileName(path), serializer.FormatName, new FileInfo(path).Length);
            audit.Log("Save", Path.GetFileName(path), true);
        }
        catch (Exception ex)
        {
            op.Fail(ex);
            audit.Log("Save", Path.GetFileName(path), false);
            if (File.Exists(tmp)) File.Delete(tmp);
            throw;
        }
    }

    /// <summary>Загружает резюме из файла.</summary>
    public Resume Load(string path)
    {
        using var op = OperationTimer.Start(logger, "LoadResume");
        try
        {
            var serializer = GetSerializer(path);
            using var fs = File.OpenRead(path);
            var resume = serializer.Deserialize(fs);
            resume.FilePath = path;
            logger.LogInformation("Резюме загружено из {FileName} ({Format})", Path.GetFileName(path), serializer.FormatName);
            audit.Log("Open", Path.GetFileName(path), true);
            return resume;
        }
        catch (Exception ex)
        {
            op.Fail(ex);
            audit.Log("Open", Path.GetFileName(path), false);
            throw;
        }
    }

    /// <summary>
    /// Сравнение форматов (лаба «Сериализация», раздел 2, вариант 1): размер и скорость
    /// сериализации одного и того же объекта всеми доступными сериализаторами.
    /// </summary>
    public IReadOnlyList<SerializerBenchmarkResult> Benchmark(Resume resume, int iterations = 1000)
    {
        var results = new List<SerializerBenchmarkResult>();
        foreach (var s in _serializers)
        {
            byte[] bytes;
            using (var ms = new MemoryStream()) { s.Serialize(resume, ms); bytes = ms.ToArray(); }

            var sw = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
            {
                using var ms = new MemoryStream();
                s.Serialize(resume, ms);
            }
            var serializeMs = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            for (var i = 0; i < iterations; i++)
            {
                using var ms = new MemoryStream(bytes);
                s.Deserialize(ms);
            }
            var deserializeMs = sw.Elapsed.TotalMilliseconds;

            results.Add(new SerializerBenchmarkResult(s.FormatName, bytes.Length, serializeMs / iterations, deserializeMs / iterations));
        }
        return results;
    }
}

/// <summary>Результат сравнения сериализаторов.</summary>
public sealed record SerializerBenchmarkResult(string Format, int SizeBytes, double SerializeMsPerOp, double DeserializeMsPerOp);
