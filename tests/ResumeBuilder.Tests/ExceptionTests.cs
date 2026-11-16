using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Services;
using ResumeBuilder.Core.Validation;
using Xunit;

namespace ResumeBuilder.Tests;

/// <summary>
/// Исключительные ситуации: неверные параметры, повреждённые и чужие файлы.
/// Программа должна сообщать об ошибке понятным исключением, а не падать «как попало».
/// </summary>
public class ExceptionTests : IClassFixture<TestFolderFixture>
{
    private readonly TestFolderFixture _folder;

    public ExceptionTests(TestFolderFixture folder)
    {
        _folder = folder;
    }

    private string NewFile(string extension) => Path.Combine(_folder.Folder, Guid.NewGuid() + extension);

    [Fact]
    public void Validate_NullResume_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ResumeValidator.ValidateManual(null!));
        Assert.Throws<ArgumentNullException>(() => ResumeValidator.ValidateByAttributes(null!));
    }

    [Fact]
    public void Save_NullResume_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonResumeSerializer().Save(null!, NewFile(".json")));
        Assert.Throws<ArgumentNullException>(() => new BinaryResumeSerializer().Save(null!, NewFile(".rbin")));
    }

    [Fact]
    public void PdfExport_BadArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PdfExporter.Export(null!, NewFile(".pdf"), includeQr: false));
        Assert.Throws<ArgumentException>(() => PdfExporter.Export(Resume.CreateSample(), " ", includeQr: false));
    }

    [Fact]
    public void TextImporter_NullResume_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TextImporter.FillContacts(null!, "text"));
    }

    [Fact]
    public void ReportAndPhone_NullArgument_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ReportCalculator.Calculate(null!, DateTime.Today));
        Assert.Throws<ArgumentNullException>(() => TextHelper.NormalizePhone(null!));
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => new JsonResumeSerializer().Load(NewFile(".json")));
    }

    [Fact]
    public void Json_NullContent_ThrowsInvalidData()
    {
        string path = NewFile(".json");
        File.WriteAllText(path, "null");
        Assert.Throws<InvalidDataException>(() => new JsonResumeSerializer().Load(path));
    }

    [Fact]
    public void Json_BrokenText_ThrowsJsonException()
    {
        string path = NewFile(".json");
        File.WriteAllText(path, "{ это не json");
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => new JsonResumeSerializer().Load(path));
    }

    [Fact]
    public void Binary_WrongSignature_ThrowsInvalidData()
    {
        string path = NewFile(".rbin");
        using (var writer = new BinaryWriter(File.Create(path), Encoding.UTF8))
        {
            writer.Write("XXXX");
            writer.Write(1);
        }
        var ex = Assert.Throws<InvalidDataException>(() => new BinaryResumeSerializer().Load(path));
        Assert.Contains("не файл резюме", ex.Message);
    }

    [Fact]
    public void Binary_NewerVersion_ThrowsInvalidData()
    {
        string path = NewFile(".rbin");
        using (var writer = new BinaryWriter(File.Create(path), Encoding.UTF8))
        {
            writer.Write("RSMB");
            writer.Write(99);   // версия формата из «будущего»
        }
        Assert.Throws<InvalidDataException>(() => new BinaryResumeSerializer().Load(path));
    }

    [Fact]
    public void Binary_TruncatedFile_ThrowsInvalidData()
    {
        string path = NewFile(".rbin");
        new BinaryResumeSerializer().Save(Resume.CreateSample(), path);
        byte[] bytes = File.ReadAllBytes(path);
        File.WriteAllBytes(path, bytes[..(bytes.Length / 2)]);   // «обрезанный» файл

        Assert.Throws<InvalidDataException>(() => new BinaryResumeSerializer().Load(path));
    }

    [Fact]
    public void WordImport_NoTable_ReturnsErrorInsteadOfException()
    {
        string path = NewFile(".docx");
        using (WordprocessingDocument doc = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            MainDocumentPart main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text("Просто текст без таблицы")))));
            main.Document.Save();
        }

        var errors = new List<string>();
        List<WorkExperience> result = WordImporter.ImportExperience(path, errors);

        Assert.Empty(result);
        Assert.Single(errors);
    }
}
