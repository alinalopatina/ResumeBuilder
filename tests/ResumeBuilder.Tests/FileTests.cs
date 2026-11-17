using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Services;
using Xunit;

namespace ResumeBuilder.Tests;

/// <summary>
/// Сохранение, загрузка и экспорт в файлы.
/// Тестовый план:
/// - один раз на класс: TestFolderFixture создаёт временную папку и удаляет её в конце;
/// - перед КАЖДЫМ тестом (конструктор): новое резюме-пример и пустой список созданных файлов;
/// - после КАЖДОГО теста (Dispose): удаляются файлы, созданные этим тестом.
/// </summary>
public class FileTests : IClassFixture<TestFolderFixture>, IDisposable
{
    private readonly TestFolderFixture _folder;
    private readonly Resume _resume;
    private readonly List<string> _createdFiles = new();

    public FileTests(TestFolderFixture folder)
    {
        _folder = folder;
        _resume = Resume.CreateSample();
    }

    public void Dispose()
    {
        foreach (string file in _createdFiles)
            if (File.Exists(file))
                File.Delete(file);
        GC.SuppressFinalize(this);
    }

    private string NewFile(string extension)
    {
        string path = Path.Combine(_folder.Folder, Guid.NewGuid() + extension);
        _createdFiles.Add(path);
        return path;
    }

    [Theory]
    [InlineData(".json")]
    [InlineData(".rbin")]
    public void SaveAndLoad_ReturnsSameData(string extension)
    {
        string path = NewFile(extension);

        IResumeSerializer serializer = ResumeFile.GetSerializer(path);
        serializer.Save(_resume, path);
        Resume loaded = serializer.Load(path);

        Assert.Equal(_resume.Personal.FullName, loaded.Personal.FullName);
        Assert.Equal(_resume.Personal.BirthDate, loaded.Personal.BirthDate);
        Assert.Equal(_resume.Personal.Website, loaded.Personal.Website);
        Assert.Equal(_resume.About, loaded.About);
        Assert.Equal(_resume.Experience.Count, loaded.Experience.Count);
        Assert.Equal(_resume.Experience[0].EndDate, loaded.Experience[0].EndDate);
        Assert.Equal(_resume.Education[0].EndYear, loaded.Education[0].EndYear);
        Assert.Equal(_resume.Skills[1].Level, loaded.Skills[1].Level);
        Assert.Equal(path, loaded.FilePath);
    }

    [Theory]
    [InlineData("resume.json", typeof(JsonResumeSerializer))]
    [InlineData("resume.rbin", typeof(BinaryResumeSerializer))]
    [InlineData("RESUME.RBIN", typeof(BinaryResumeSerializer))]
    [InlineData("resume.txt", typeof(JsonResumeSerializer))]
    public void GetSerializer_ChoosesByExtension(string fileName, Type expected)
    {
        Assert.IsType(expected, ResumeFile.GetSerializer(fileName));
    }

    [Fact]
    public void Json_DoesNotContainFilePath()
    {
        string path = NewFile(".json");
        _resume.FilePath = @"C:\secret\resume.json";

        new JsonResumeSerializer().Save(_resume, path);

        Assert.DoesNotContain("FilePath", File.ReadAllText(path));
    }

    [Fact]
    public void Binary_IsSmallerThanJson()
    {
        string json = NewFile(".json");
        string bin = NewFile(".rbin");

        new JsonResumeSerializer().Save(_resume, json);
        new BinaryResumeSerializer().Save(_resume, bin);

        Assert.True(new FileInfo(bin).Length < new FileInfo(json).Length);
    }

    [Fact]
    public void Word_ExportThenImport_SameJobs()
    {
        string path = NewFile(".docx");

        WordExporter.ExportResume(_resume, path);
        var errors = new List<string>();
        List<WorkExperience> imported = WordImporter.ImportExperience(path, errors);

        Assert.Empty(errors);
        Assert.Equal(_resume.Experience.Count, imported.Count);
        Assert.Equal(_resume.Experience[0].Company, imported[0].Company);
        Assert.Equal(_resume.Experience[0].Description, imported[0].Description);
        Assert.Equal(_resume.Experience[0].EndDate?.ToString("MM.yyyy"), imported[0].EndDate?.ToString("MM.yyyy"));
    }

    [Fact]
    public void Excel_HasFourSheets_AndPdfIsCreated()
    {
        string xlsx = NewFile(".xlsx");
        string pdf = NewFile(".pdf");

        ExcelExporter.ExportResume(_resume, xlsx);
        PdfExporter.Export(_resume, pdf, includeQr: true);

        Assert.True(new FileInfo(pdf).Length > 0);
        using var workbook = new ClosedXML.Excel.XLWorkbook(xlsx);
        Assert.Equal(4, workbook.Worksheets.Count);   // Резюме, Опыт, Образование, Навыки
        Assert.True(workbook.Worksheet(2).Cell(1, 2).Style.Font.Bold);   // жирная шапка
    }

    [Fact]
    public void Report_SameDataInExcelAndWord()
    {
        ResumeReport report = ReportCalculator.Calculate(_resume, new DateTime(2026, 11, 10));
        string xlsx = NewFile(".xlsx");
        string docx = NewFile(".docx");

        ExcelExporter.ExportReport(report, xlsx);
        WordExporter.ExportReport(report, docx);

        Assert.True(new FileInfo(xlsx).Length > 0);
        Assert.True(new FileInfo(docx).Length > 0);
    }

    [Fact]
    public void DraftCache_SaveAndLoad()
    {
        string path = NewFile(".rbin");   // свой файл, чтобы не испортить настоящий черновик

        DraftCache.Save(_resume, path);
        Resume? draft = DraftCache.TryLoad(path);

        Assert.NotNull(draft);
        Assert.Equal(_resume.Personal.FullName, draft!.Personal.FullName);
        Assert.Null(draft.FilePath);   // черновик не привязан к файлу пользователя
    }

    [Fact]
    public void DraftCache_NoFile_ReturnsNull()
    {
        Assert.Null(DraftCache.TryLoad(NewFile(".rbin")));
    }
}
