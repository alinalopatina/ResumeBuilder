using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Filters;

namespace ResumeBuilder.Core.Services;

/// <summary>
/// Настройка журнала Serilog. Уровни, приёмники и ротация файлов лежат в appsettings.json,
/// поэтому их можно менять без перекомпиляции.
/// </summary>
public static class LoggingSetup
{
    /// <summary>Порог «медленной» операции в мс (задаётся в appsettings.json).</summary>
    public static int SlowOperationMs { get; private set; } = 1000;

    public static void Init()
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        SlowOperationMs = config.GetValue("Logging:SlowOperationMs", 1000);

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)                       // основные настройки — из файла
            .Enrich.WithProperty("UserId", Environment.UserName)  // кто работал с программой
            // Отдельный журнал аудита: попадают только записи логгера AUDIT,
            // хранится 12 месяцев (основной журнал — 7 дней)
            .WriteTo.Logger(audit => audit
                .Filter.ByIncludingOnly(Matching.FromSource(AuditLog.Name))
                .WriteTo.File("logs/audit-.log",
                    rollingInterval: RollingInterval.Month,
                    retainedFileCountLimit: 12,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} {Message:lj}{NewLine}"))
            .CreateLogger();

        Log.Information("Программа запущена");
    }
}
