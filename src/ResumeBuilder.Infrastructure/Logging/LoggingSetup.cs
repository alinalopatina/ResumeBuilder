using Microsoft.Extensions.Configuration;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Services;
using Serilog;
using Serilog.Settings.Configuration;

namespace ResumeBuilder.Infrastructure.Logging;

/// <summary>
/// Настройка журналирования Serilog (лаба «Журналирование»).
/// </summary>
/// <remarks>
/// Вся конфигурация вынесена в <c>appsettings.json</c> (задание 2.1): уровни, приёмники (sinks),
/// ротация, переопределения уровней для отдельных пространств имён (задание 1.3).
/// Изменить уровень журналирования можно без перекомпиляции — достаточно поправить JSON.
/// </remarks>
public static class LoggingSetup
{
    /// <summary>Создаёт основной логгер из секции <c>Serilog</c>.</summary>
    public static Serilog.ILogger CreateMainLogger(IConfiguration configuration)
    {
        var slowMs = configuration.GetValue("Logging:SlowOperationThresholdMs", 1000);
        OperationTimer.SlowThresholdMs = slowMs;

        return new LoggerConfiguration()
            .ReadFrom.Configuration(configuration) // секция "Serilog"
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .CreateLogger();
    }

    /// <summary>
    /// Отдельный логгер аудита из секции <c>AuditLog</c>: свой файл и своя политика хранения
    /// (задание 3.2).
    /// </summary>
    public static Serilog.ILogger CreateAuditLogger(IConfiguration configuration) =>
        new LoggerConfiguration()
            .ReadFrom.Configuration(configuration, new ConfigurationReaderOptions { SectionName = "AuditLog" })
            .CreateLogger();
}

/// <summary>Журнал аудита на базе отдельного Serilog-логгера.</summary>
public sealed class SerilogAuditLogger(Serilog.ILogger auditLogger) : IAuditLogger
{
    /// <inheritdoc />
    public void Log(string action, string? target, bool success)
    {
        // Персональные данные (e-mail, телефон) в аудит не пишем — только имя пользователя ОС и имя файла
        auditLogger.Information("AUDIT {User} {Action} {Target} {Success}",
            Environment.UserName, action, target ?? "-", success ? "OK" : "FAIL");
    }
}

/// <summary>Пустой аудит (для тестов).</summary>
public sealed class NullAuditLogger : IAuditLogger
{
    /// <summary>Единственный экземпляр.</summary>
    public static readonly NullAuditLogger Instance = new();

    /// <inheritdoc />
    public void Log(string action, string? target, bool success) { }
}
