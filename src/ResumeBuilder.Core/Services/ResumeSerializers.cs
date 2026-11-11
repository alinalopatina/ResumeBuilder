using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using ResumeBuilder.Core.Models;

namespace ResumeBuilder.Core.Services;

/// <summary>Общий интерфейс для сохранения резюме в файл.</summary>
public interface IResumeSerializer
{
    void Save(Resume resume, string path);
    Resume Load(string path);
}

/// <summary>Сохранение в JSON (System.Text.Json). Файл можно открыть в блокноте.</summary>
public class JsonResumeSerializer : IResumeSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // чтобы русские буквы не превращались в И...
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public void Save(Resume resume, string path)
    {
        ArgumentNullException.ThrowIfNull(resume);
        string json = JsonSerializer.Serialize(resume, Options);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    public Resume Load(string path)
    {
        string json = File.ReadAllText(path, Encoding.UTF8);
        Resume? resume = JsonSerializer.Deserialize<Resume>(json, Options);
        if (resume == null)
            throw new InvalidDataException("Файл пустой или повреждён");
        resume.FilePath = path;
        return resume;
    }
}

/// <summary>
/// Двоичное сохранение через BinaryWriter / BinaryReader.
/// BinaryFormatter не используется: Microsoft признала его небезопасным, в .NET 9 он удалён.
/// Формат файла: подпись "RSMB", номер версии, затем поля по порядку.
/// </summary>
public class BinaryResumeSerializer : IResumeSerializer
{
    private const string Signature = "RSMB";
    private const int FormatVersion = 1;

    public void Save(Resume resume, string path)
    {
        ArgumentNullException.ThrowIfNull(resume);
        using var writer = new BinaryWriter(File.Create(path), Encoding.UTF8);
        writer.Write(Signature);
        writer.Write(FormatVersion);

        PersonalInfo p = resume.Personal;
        writer.Write(p.FullName);
        writer.Write(p.Position);
        writer.Write(p.Email);
        writer.Write(p.Phone);
        writer.Write(p.City);
        writer.Write(p.Website ?? "");
        writer.Write(p.BirthDate?.ToBinary() ?? 0L);
        writer.Write(resume.About);

        writer.Write(resume.Experience.Count);
        foreach (WorkExperience e in resume.Experience)
        {
            writer.Write(e.Company);
            writer.Write(e.Position);
            writer.Write(e.StartDate.ToBinary());
            writer.Write(e.EndDate?.ToBinary() ?? 0L);   // 0 — значит «по настоящее время»
            writer.Write(e.Description);
        }

        writer.Write(resume.Education.Count);
        foreach (Education ed in resume.Education)
        {
            writer.Write(ed.Institution);
            writer.Write(ed.Specialty);
            writer.Write(ed.StartYear);
            writer.Write(ed.EndYear ?? 0);
        }

        writer.Write(resume.Skills.Count);
        foreach (Skill s in resume.Skills)
        {
            writer.Write(s.Name);
            writer.Write(s.Level);
        }
    }

    public Resume Load(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path), Encoding.UTF8);
        try
        {
            if (reader.ReadString() != Signature)
                throw new InvalidDataException("Это не файл резюме");
            int version = reader.ReadInt32();
            if (version > FormatVersion)
                throw new InvalidDataException("Файл создан более новой версией программы");

            var resume = new Resume { FilePath = path };
            PersonalInfo p = resume.Personal;
            p.FullName = reader.ReadString();
            p.Position = reader.ReadString();
            p.Email = reader.ReadString();
            p.Phone = reader.ReadString();
            p.City = reader.ReadString();
            string website = reader.ReadString();
            p.Website = website == "" ? null : website;
            long birth = reader.ReadInt64();
            p.BirthDate = birth == 0 ? null : DateTime.FromBinary(birth);
            resume.About = reader.ReadString();

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var e = new WorkExperience();
                e.Company = reader.ReadString();
                e.Position = reader.ReadString();
                e.StartDate = DateTime.FromBinary(reader.ReadInt64());
                long end = reader.ReadInt64();
                e.EndDate = end == 0 ? null : DateTime.FromBinary(end);
                e.Description = reader.ReadString();
                resume.Experience.Add(e);
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var ed = new Education();
                ed.Institution = reader.ReadString();
                ed.Specialty = reader.ReadString();
                ed.StartYear = reader.ReadInt32();
                int endYear = reader.ReadInt32();
                ed.EndYear = endYear == 0 ? null : endYear;
                resume.Education.Add(ed);
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
                resume.Skills.Add(new Skill { Name = reader.ReadString(), Level = reader.ReadInt32() });

            return resume;
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("Файл повреждён");
        }
    }
}

/// <summary>Выбор способа сохранения по расширению файла.</summary>
public static class ResumeFile
{
    public const string JsonExtension = ".json";
    public const string BinaryExtension = ".rbin";
    public const string DialogFilter = "Резюме JSON (*.json)|*.json|Резюме двоичный (*.rbin)|*.rbin";

    public static IResumeSerializer GetSerializer(string path) =>
        path.EndsWith(BinaryExtension, StringComparison.OrdinalIgnoreCase)
            ? new BinaryResumeSerializer()
            : new JsonResumeSerializer();
}

/// <summary>
/// Сравнение форматов: один и тот же объект сохраняется в JSON и в двоичный файл,
/// сравниваются размер и скорость (лабораторная по сериализации, вариант 1).
/// </summary>
public static class SerializationComparison
{
    public static string Compare(Resume resume, int repeats = 200)
    {
        string folder = Path.Combine(Path.GetTempPath(), "ResumeBuilderTest");
        Directory.CreateDirectory(folder);
        var lines = new List<string>();

        foreach (string extension in new[] { ResumeFile.JsonExtension, ResumeFile.BinaryExtension })
        {
            string path = Path.Combine(folder, "test" + extension);
            IResumeSerializer serializer = ResumeFile.GetSerializer(path);

            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < repeats; i++)
                serializer.Save(resume, path);
            double saveMs = timer.Elapsed.TotalMilliseconds / repeats;

            timer.Restart();
            for (int i = 0; i < repeats; i++)
                serializer.Load(path);
            double loadMs = timer.Elapsed.TotalMilliseconds / repeats;

            long size = new FileInfo(path).Length;
            lines.Add($"{extension,-6}  {size,7} байт   запись {saveMs:0.00} мс   чтение {loadMs:0.00} мс");
        }

        lines.Add("");
        lines.Add("JSON больше, но его можно прочитать в блокноте и открыть в другой программе.");
        lines.Add("Двоичный файл меньше и быстрее, но читается только нашей программой.");
        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Кэш черновика: при закрытии программы резюме сохраняется в двоичный файл,
/// а при следующем запуске восстанавливается из него (лабораторная по сериализации, сценарий 2).
/// </summary>
public static class DraftCache
{
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ResumeBuilder", "draft.rbin");

    public static void Save(Resume resume)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string? realPath = resume.FilePath;
        new BinaryResumeSerializer().Save(resume, FilePath);
        resume.FilePath = realPath;
    }

    public static Resume? TryLoad()
    {
        if (!File.Exists(FilePath))
            return null;
        try
        {
            Resume resume = new BinaryResumeSerializer().Load(FilePath);
            resume.FilePath = null;   // черновик ещё не сохранён пользователем в свой файл
            return resume;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}
