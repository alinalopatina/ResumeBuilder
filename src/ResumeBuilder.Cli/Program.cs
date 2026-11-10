using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResumeBuilder.Cli;
using ResumeBuilder.Infrastructure;
using Serilog;

// ─────────────────────────────────────────────────────────────────────────────
// ResumeBuilder CLI — консольная версия конструктора резюме.
// Нужна для демонстрации лабораторных без GUI и для запуска в Docker.
// Пример: dotnet run --project src/ResumeBuilder.Cli -- export samples/sample.resume.json out/cv.pdf
// ─────────────────────────────────────────────────────────────────────────────

Console.OutputEncoding = Encoding.UTF8;

var configuration = ServiceRegistration.BuildConfiguration();
await using var provider = new ServiceCollection()
    .AddResumeBuilder(configuration)
    .AddSingleton<CliCommands>()
    .BuildServiceProvider();

var logger = provider.GetRequiredService<ILogger<Program>>();
var commands = provider.GetRequiredService<CliCommands>();

try
{
    var exitCode = commands.Run(args);
    return exitCode;
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Необработанная ошибка: {Message}", ex.Message);
    Console.Error.WriteLine($"Ошибка: {ex.Message}");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
