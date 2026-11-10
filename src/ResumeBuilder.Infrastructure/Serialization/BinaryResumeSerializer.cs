using System.Text;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Models;

namespace ResumeBuilder.Infrastructure.Serialization;

/// <summary>
/// Двоичная сериализация резюме в <b>собственный формат</b> <c>.rbin</c> через <see cref="BinaryWriter"/>/<see cref="BinaryReader"/>.
/// </summary>
/// <remarks>
/// <para><b>Почему не BinaryFormatter.</b> Microsoft признала его небезопасным: при десериализации
/// данных из ненадёжного источника он может создать произвольные объекты и выполнить код.
/// В .NET 8 он отключён по умолчанию, а в .NET 9 удалён полностью
/// (https://learn.microsoft.com/dotnet/standard/serialization/binaryformatter-security-guide).</para>
/// <para><b>Структура файла</b> (все строки — UTF-8 с префиксом длины):</para>
/// <code>
/// [4 байта] сигнатура "RSMB"
/// [2 байта] версия формата (ushort)
/// [...]     поля резюме по порядку; nullable-поле = bool-флаг + значение
/// [n]       списки: int количество + элементы
/// </code>
/// <para>Сигнатура и версия решают проблемы «не тот файл» и совместимости версий.
/// Поле <see cref="Resume.FilePath"/> намеренно не записывается (аналог <c>[NonSerialized]</c>).</para>
/// <para>Применение: быстрый автосейв (кэш) состояния редактора — файл в 3–5 раз меньше JSON.</para>
/// </remarks>
public sealed class BinaryResumeSerializer : IResumeSerializer
{
    private static readonly byte[] Magic = "RSMB"u8.ToArray();

    /// <summary>Текущая версия двоичного формата.</summary>
    public const ushort FormatVersion = 1;

    /// <inheritdoc />
    public string FormatName => "Binary (.rbin)";

    /// <inheritdoc />
    public string FileExtension => ".rbin";

    /// <inheritdoc />
    public void Serialize(Resume resume, Stream output)
    {
        ArgumentNullException.ThrowIfNull(resume);
        using var w = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);

        w.Write(Magic);
        w.Write(FormatVersion);

        w.Write(resume.Id.ToByteArray());
        w.Write(resume.SchemaVersion);
        w.Write((int)resume.Template);
        w.Write((int)resume.QrMode);
        w.Write(resume.CultureName);
        w.Write(resume.CreatedAt.ToBinary());
        w.Write(DateTime.Now.ToBinary());
        WriteNullable(w, resume.Summary);

        var p = resume.Personal;
        w.Write(p.FullName);
        w.Write(p.DesiredPosition);
        w.Write(p.Email);
        WriteNullable(w, p.Phone);
        WriteNullable(w, p.City);
        WriteNullable(w, p.BirthDate);
        WriteNullable(w, p.Website);
        w.Write(p.DesiredSalary.HasValue);
        if (p.DesiredSalary.HasValue) w.Write(p.DesiredSalary.Value);
        w.Write(p.SalaryCurrency);
        WriteNullable(w, p.PhotoPath);

        WriteList(w, resume.Experience, (bw, e) =>
        {
            bw.Write(e.Id.ToByteArray());
            bw.Write(e.Company);
            bw.Write(e.Position);
            WriteNullable(bw, e.City);
            bw.Write(e.StartDate.ToBinary());
            WriteNullable(bw, e.EndDate);
            WriteNullable(bw, e.Description);
        });

        WriteList(w, resume.Education, (bw, e) =>
        {
            bw.Write(e.Id.ToByteArray());
            bw.Write(e.Institution);
            WriteNullable(bw, e.Degree);
            WriteNullable(bw, e.Specialty);
            bw.Write(e.StartYear);
            bw.Write(e.EndYear.HasValue);
            if (e.EndYear.HasValue) bw.Write(e.EndYear.Value);
        });

        WriteList(w, resume.Skills, (bw, s) =>
        {
            bw.Write(s.Id.ToByteArray());
            bw.Write(s.Name);
            bw.Write((byte)s.Category);
            bw.Write((byte)s.Level);
        });

        WriteList(w, resume.Languages, (bw, l) =>
        {
            bw.Write(l.Id.ToByteArray());
            bw.Write(l.Language);
            bw.Write((byte)l.Level);
        });
    }

    /// <inheritdoc />
    public Resume Deserialize(Stream input)
    {
        using var r = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        try
        {
            var magic = r.ReadBytes(Magic.Length);
            if (!magic.AsSpan().SequenceEqual(Magic))
                throw new InvalidDataException("Это не файл резюме ResumeBuilder (неверная сигнатура).");

            var version = r.ReadUInt16();
            if (version > FormatVersion)
                throw new InvalidDataException($"Версия двоичного формата {version} не поддерживается (максимум {FormatVersion}).");

            var resume = new Resume
            {
                Id = ReadGuid(r),
                SchemaVersion = r.ReadInt32(),
                Template = (ResumeTemplate)r.ReadInt32(),
                QrMode = (QrContentMode)r.ReadInt32(),
                CultureName = r.ReadString(),
                CreatedAt = DateTime.FromBinary(r.ReadInt64()),
                UpdatedAt = DateTime.FromBinary(r.ReadInt64()),
                Summary = ReadNullableString(r)
            };

            var p = resume.Personal;
            p.FullName = r.ReadString();
            p.DesiredPosition = r.ReadString();
            p.Email = r.ReadString();
            p.Phone = ReadNullableString(r);
            p.City = ReadNullableString(r);
            p.BirthDate = ReadNullableDate(r);
            p.Website = ReadNullableString(r);
            p.DesiredSalary = r.ReadBoolean() ? r.ReadDecimal() : null;
            p.SalaryCurrency = r.ReadString();
            p.PhotoPath = ReadNullableString(r);

            resume.Experience = ReadList(r, br => new WorkExperience
            {
                Id = ReadGuid(br),
                Company = br.ReadString(),
                Position = br.ReadString(),
                City = ReadNullableString(br),
                StartDate = DateTime.FromBinary(br.ReadInt64()),
                EndDate = ReadNullableDate(br),
                Description = ReadNullableString(br)
            });

            resume.Education = ReadList(r, br => new Education
            {
                Id = ReadGuid(br),
                Institution = br.ReadString(),
                Degree = ReadNullableString(br),
                Specialty = ReadNullableString(br),
                StartYear = br.ReadInt32(),
                EndYear = br.ReadBoolean() ? br.ReadInt32() : null
            });

            resume.Skills = ReadList(r, br => new Skill
            {
                Id = ReadGuid(br),
                Name = br.ReadString(),
                Category = (SkillCategory)br.ReadByte(),
                Level = br.ReadByte()
            });

            resume.Languages = ReadList(r, br => new LanguageSkill
            {
                Id = ReadGuid(br),
                Language = br.ReadString(),
                Level = (LanguageLevel)br.ReadByte()
            });

            return resume;
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("Файл повреждён: неожиданный конец данных.", ex);
        }
    }

    // ─── вспомогательные методы ───

    private const int MaxListLength = 10_000; // защита от «бомбы» в повреждённом файле

    private static void WriteNullable(BinaryWriter w, string? value)
    {
        w.Write(value is not null);
        if (value is not null) w.Write(value);
    }

    private static void WriteNullable(BinaryWriter w, DateTime? value)
    {
        w.Write(value.HasValue);
        if (value.HasValue) w.Write(value.Value.ToBinary());
    }

    private static Guid ReadGuid(BinaryReader r)
    {
        var bytes = r.ReadBytes(16);
        if (bytes.Length != 16) throw new EndOfStreamException();
        return new Guid(bytes);
    }

    private static string? ReadNullableString(BinaryReader r) => r.ReadBoolean() ? r.ReadString() : null;

    private static DateTime? ReadNullableDate(BinaryReader r) => r.ReadBoolean() ? DateTime.FromBinary(r.ReadInt64()) : null;

    private static void WriteList<T>(BinaryWriter w, IReadOnlyCollection<T> items, Action<BinaryWriter, T> writeItem)
    {
        w.Write(items.Count);
        foreach (var item in items) writeItem(w, item);
    }

    private static List<T> ReadList<T>(BinaryReader r, Func<BinaryReader, T> readItem)
    {
        var count = r.ReadInt32();
        if (count is < 0 or > MaxListLength)
            throw new InvalidDataException($"Файл повреждён: недопустимая длина списка ({count}).");
        var list = new List<T>(count);
        for (var i = 0; i < count; i++) list.Add(readItem(r));
        return list;
    }
}
