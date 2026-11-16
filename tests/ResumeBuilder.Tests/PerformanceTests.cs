using System.Diagnostics;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Services;
using Xunit;

namespace ResumeBuilder.Tests;

/// <summary>
/// Проверки на время выполнения: операция должна укладываться в заданный интервал.
/// Пределы взяты с большим запасом, чтобы тесты не падали на медленном компьютере.
/// </summary>
public class PerformanceTests : IClassFixture<TestFolderFixture>
{
    private readonly TestFolderFixture _folder;

    public PerformanceTests(TestFolderFixture folder)
    {
        _folder = folder;
    }

    [Fact]
    public void NormalizePhone_10000Times_UnderOneSecond()
    {
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++)
            TextHelper.NormalizePhone("8 (029) 123-45-67");
        timer.Stop();

        Assert.True(timer.ElapsedMilliseconds < 1000, $"Слишком медленно: {timer.ElapsedMilliseconds} мс");
    }

    [Fact]
    public void ReportCalculation_1000Jobs_UnderHalfSecond()
    {
        var resume = new Resume();
        for (int i = 0; i < 1000; i++)
            resume.Experience.Add(new WorkExperience
            {
                Company = "Компания " + i,
                Position = "Разработчик",
                StartDate = new DateTime(2000, 1, 1).AddMonths(i % 200),
                EndDate = new DateTime(2020, 1, 1)
            });

        var timer = Stopwatch.StartNew();
        ResumeReport report = ReportCalculator.Calculate(resume, DateTime.Today);
        timer.Stop();

        Assert.Equal(1000, report.JobsCount);
        Assert.True(timer.ElapsedMilliseconds < 500, $"Слишком медленно: {timer.ElapsedMilliseconds} мс");
    }

    [Fact]
    public void BinarySaveAndLoad_100Times_UnderTwoSeconds()
    {
        string path = Path.Combine(_folder.Folder, "perf.rbin");
        var serializer = new BinaryResumeSerializer();
        Resume resume = Resume.CreateSample();

        var timer = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            serializer.Save(resume, path);
            serializer.Load(path);
        }
        timer.Stop();

        Assert.True(timer.ElapsedMilliseconds < 2000, $"Слишком медленно: {timer.ElapsedMilliseconds} мс");
    }

    /// <summary>Таймаут средствами xUnit: если экспорт идёт дольше 30 секунд, тест считается проваленным.</summary>
    [Fact(Timeout = 30_000)]
    public async Task PdfExport_FinishesWithinTimeout()
    {
        string path = Path.Combine(_folder.Folder, "perf.pdf");
        await Task.Run(() => PdfExporter.Export(Resume.CreateSample(), path, includeQr: true));
        Assert.True(File.Exists(path));
    }
}
