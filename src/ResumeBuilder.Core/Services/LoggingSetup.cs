using Microsoft.Extensions.Configuration;
using ResumeBuilder.Core.Configuration;
using Serilog;
using Serilog.Filters;

namespace ResumeBuilder.Core.Services;

/// <summary>
/// Настройка журнала Serilog. Уровни, приёмники и ротация файлов лежат в appsettings.json
/// (и в профилях appsettings.Development.json / appsettings.Production.json),
/// поэтому их можно менять без перекомпиляции.
/// </summary>
public static class LoggingSetup
{
    /// <summary>Порог «медленной» операции в мс (App:SlowOperationMs в настройках).</summary>
    public static int SlowOperationMs => ConfigManager.Current.SlowOperationMs;

    /// <summary>
    /// Папка журналов: %LOCALAPPDATA%\ResumeBuilder\logs. Не рядом с exe — после установки
    /// в Program Files туда нельзя писать без прав администратора.
    /// </summary>
    public static string LogFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ResumeBuilder", "logs");

    /// <summary>Создаёт журнал по уже загруженной конфигурации (см. <see cref="ConfigManager"/>).</summary>
    /// <param name="config">
    /// Объединённая конфигурация. Если в ней поменять MinimumLevel в файле во время работы,
    /// Serilog подхватит новый уровень без перезапуска.
    /// </param>
    public static void Init(IConfiguration config)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)                       // основные настройки — из файла
            .Enrich.WithProperty("UserId", Environment.UserName)  // кто работал с программой
            // Отдельный журнал аудита: попадают только записи логгера AUDIT,
            // хранится 12 месяцев (основной журнал — 7 дней)
            .WriteTo.Logger(audit => audit
                .Filter.ByIncludingOnly(Matching.FromSource(AuditLog.Name))
                .WriteTo.File(Path.Combine(LogFolder, "audit-.log"),
                    rollingInterval: RollingInterval.Month,
                    retainedFileCountLimit: 12,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} {Message:lj}{NewLine}"))
            .CreateLogger();

        Log.Information("Программа запущена, профиль {Environment}", ConfigManager.Current.Environment);
    }
}
