using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Services;
using Xunit;

namespace ResumeBuilder.Tests;

/// <summary>Сохранение/загрузка и экспорт в файлы.</summary>
public class FileTests
{
    private static string TempFile(string extension) =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid() + extension);

    [Theory]
    [InlineData(".json")]
    [InlineData(".rbin")]
    public void SaveAndLoad_ReturnsSameData(string extension)
    {
        string path = TempFile(extension);
        Resume original = Resume.CreateSample();

        IResumeSerializer serializer = ResumeFile.GetSerializer(path);
        serializer.Save(original, path);
        Resume loaded = serializer.Load(path);

        Assert.Equal(original.Personal.FullName, loaded.Personal.FullName);
        Assert.Equal(original.Personal.BirthDate, loaded.Personal.BirthDate);
        Assert.Equal(original.Experience.Count, loaded.Experience.Count);
        Assert.Equal(original.Experience[0].EndDate, loaded.Experience[0].EndDate);
        Assert.Equal(original.Skills[1].Level, loaded.Skills[1].Level);
        File.Delete(path);
    }

    [Fact]
    public void Binary_WrongFile_Throws()
    {
        string path = TempFile(".rbin");
        File.WriteAllText(path, "это не резюме");
        Assert.ThrowsAny<Exception>(() => new BinaryResumeSerializer().Load(path));
        File.Delete(path);
    }

    [Fact]
    public void Report_CalculatesTotals()
    {
        Resume resume = Resume.CreateSample();
        ResumeReport report = ReportCalculator.Calculate(resume, new DateTime(2026, 11, 10));

        // EPAM: 07.2025–09.2025 = 2 мес., фриланс: 02.2024–06.2025 = 16 мес.
        Assert.Equal(2, report.JobsCount);
        Assert.Equal(18, report.TotalMonths);
        Assert.Equal(9.0, report.AverageMonths);
        Assert.Equal(3.75, report.AverageSkillLevel);
    }

    [Fact]
    public void Word_ExportThenImport_SameJobs()
    {
        string path = TempFile(".docx");
        Resume resume = Resume.CreateSample();

        WordExporter.ExportResume(resume, path);
        var errors = new List<string>();
        List<WorkExperience> imported = WordImporter.ImportExperience(path, errors);

        Assert.Empty(errors);
        Assert.Equal(resume.Experience.Count, imported.Count);
        Assert.Equal(resume.Experience[0].Company, imported[0].Company);
        File.Delete(path);
    }

    [Fact]
    public void Excel_And_Pdf_FilesAreCreated()
    {
        Resume resume = Resume.CreateSample();
        string xlsx = TempFile(".xlsx");
        string pdf = TempFile(".pdf");

        ExcelExporter.ExportExperience(resume, xlsx);
        PdfExporter.Export(resume, pdf, includeQr: true);

        Assert.True(new FileInfo(xlsx).Length > 0);
        Assert.True(new FileInfo(pdf).Length > 0);
        File.Delete(xlsx);
        File.Delete(pdf);
    }
}
