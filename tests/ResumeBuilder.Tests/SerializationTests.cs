using System.Text;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Builder;
using ResumeBuilder.Core.History;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Infrastructure.Serialization;

namespace ResumeBuilder.Tests;

/// <summary>Двоичная и JSON-сериализация, Memento.</summary>
public class SerializationTests
{
    public static TheoryData<IResumeSerializer> Serializers => new() { new JsonResumeSerializer(), new BinaryResumeSerializer() };

    [Theory]
    [MemberData(nameof(Serializers))]
    public void RoundTrip_PreservesAllData(IResumeSerializer serializer)
    {
        var original = SampleData.CreateSampleResume();
        original.FilePath = @"C:\temp\secret.resume.json";

        using var ms = new MemoryStream();
        serializer.Serialize(original, ms);
        ms.Position = 0;
        var restored = serializer.Deserialize(ms);

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Personal.FullName, restored.Personal.FullName);
        Assert.Equal(original.Personal.BirthDate, restored.Personal.BirthDate);
        Assert.Equal(original.Personal.DesiredSalary, restored.Personal.DesiredSalary);
        Assert.Equal(original.Summary, restored.Summary);
        Assert.Equal(original.Template, restored.Template);
        Assert.Equal(original.Experience.Count, restored.Experience.Count);
        Assert.Equal(original.Experience.Select(e => (e.Company, e.StartDate, e.EndDate)),
                     restored.Experience.Select(e => (e.Company, e.StartDate, e.EndDate)));
        Assert.Equal(original.Skills.Select(s => (s.Name, s.Level, s.Category)),
                     restored.Skills.Select(s => (s.Name, s.Level, s.Category)));
        Assert.Equal(original.Languages.Select(l => l.Level), restored.Languages.Select(l => l.Level));
        Assert.Equal(original.Education.Select(e => e.EndYear), restored.Education.Select(e => e.EndYear));
        Assert.Null(restored.FilePath); // поле исключено из сериализации
    }

    [Fact]
    public void Json_IsHumanReadable_WithCyrillicAndEnumNames()
    {
        using var ms = new MemoryStream();
        new JsonResumeSerializer().Serialize(SampleData.CreateSampleResume(), ms);
        var json = Encoding.UTF8.GetString(ms.ToArray());

        Assert.Contains("\"fullName\": \"Иванова Мария Сергеевна\"", json);
        Assert.Contains("\"template\": \"Modern\"", json);
        Assert.DoesNotContain("filePath", json);
        Assert.DoesNotContain("isCurrent", json);
    }

    [Fact]
    public void Binary_IsSmallerThanJson()
    {
        var resume = SampleData.CreateSampleResume();
        using var json = new MemoryStream();
        using var bin = new MemoryStream();
        new JsonResumeSerializer().Serialize(resume, json);
        new BinaryResumeSerializer().Serialize(resume, bin);
        Assert.True(bin.Length < json.Length, $"binary {bin.Length} >= json {json.Length}");
    }

    [Fact]
    public void Binary_WrongSignature_ThrowsInvalidData()
    {
        using var ms = new MemoryStream("NOPE-this-is-not-a-resume"u8.ToArray());
        Assert.Throws<InvalidDataException>(() => new BinaryResumeSerializer().Deserialize(ms));
    }

    [Fact]
    public void Binary_TruncatedFile_ThrowsInvalidData()
    {
        using var full = new MemoryStream();
        new BinaryResumeSerializer().Serialize(SampleData.CreateSampleResume(), full);
        using var truncated = new MemoryStream(full.ToArray()[..(int)(full.Length / 2)]);
        Assert.Throws<InvalidDataException>(() => new BinaryResumeSerializer().Deserialize(truncated));
    }

    [Fact]
    public void Json_NewerSchema_IsRejected()
    {
        using var ms = new MemoryStream("{\"schemaVersion\": 99, \"personal\": {}}"u8.ToArray());
        Assert.Throws<InvalidDataException>(() => new JsonResumeSerializer().Deserialize(ms));
    }

    [Fact]
    public void Json_OldFileWithoutNewFields_GetsDefaults()
    {
        using var ms = new MemoryStream("{\"personal\": {\"fullName\": \"Тест\"}}"u8.ToArray());
        var r = new JsonResumeSerializer().Deserialize(ms);
        Assert.Equal("Тест", r.Personal.FullName);
        Assert.Equal("BYN", r.Personal.SalaryCurrency);
        Assert.Empty(r.Experience);
    }

    [Fact]
    public void History_Undo_RestoresPreviousState()
    {
        var history = new ResumeHistory();
        var resume = SampleData.CreateSampleResume();
        history.Push(resume, "rename");
        resume.Personal.FullName = "Изменено";

        var restored = history.Undo();

        Assert.NotNull(restored);
        Assert.Equal("Иванова Мария Сергеевна", restored!.Personal.FullName);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void History_RespectsCapacity()
    {
        var history = new ResumeHistory(capacity: 3);
        var resume = new Resume();
        for (var i = 0; i < 10; i++) history.Push(resume, $"step {i}");
        Assert.Equal(3, history.Count);
        Assert.Equal("step 9", history.LastDescription);
    }
}
