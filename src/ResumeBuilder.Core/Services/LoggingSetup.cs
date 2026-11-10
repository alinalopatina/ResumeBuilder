using Microsoft.Extensions.Configuration;
using Serilog;

namespace ResumeBuilder.Core.Services;

/// <summary>
/// Настройка журнала Serilog. Все параметры (уровни, файлы, ротация) лежат в appsettings.json,
/// поэтому их можно менять без перекомпиляции.
/// </summary>
public static class LoggingSetup
{
    public static void Init()
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .CreateLogger();

        Log.Information("Программа запущена");
    }
}
