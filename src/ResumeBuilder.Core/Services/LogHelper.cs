using System.Diagnostics;
using Serilog;
using Serilog.Context;

namespace ResumeBuilder.Core.Services;

/// <summary>
/// Журнал аудита: важные действия пользователя (открыл, сохранил, экспортировал).
/// Это отдельный логгер с именем AUDIT, он пишет в свой файл logs/audit-*.log.
/// </summary>
public static class AuditLog
{
    public const string Name = "AUDIT";

    public static void Write(string action, string target, bool success)
    {
        Log.ForContext(Serilog.Core.Constants.SourceContextPropertyName, Name)
           .Information("{User} | {Action} | {Target} | {Result}",
               Environment.UserName, action, target, success ? "успешно" : "ошибка");
    }
}

/// <summary>
/// Выполнение операции с записью в журнал:
/// 1) у всех записей внутри операции будет общий CorrelationId — по нему видно весь путь операции
///    (окно → экспорт → сохранение файла);
/// 2) записывается время выполнения ExecutionTimeMs. Обычные операции — уровень Information,
///    медленные (дольше порога) — Warning. В файл log-*.txt пишется только Warning и выше,
///    поэтому туда попадают только медленные операции.
/// </summary>
public static class LoggedOperation
{
    public static void Run(string operationName, Action action)
    {
        string correlationId = Guid.NewGuid().ToString("N").Substring(0, 8);
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            Log.Debug("Начало операции {Operation}", operationName);
            var timer = Stopwatch.StartNew();
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Операция {Operation} завершилась ошибкой", operationName);
                throw;
            }

            long ms = timer.ElapsedMilliseconds;
            if (ms > LoggingSetup.SlowOperationMs)
                Log.Warning("Медленная операция {Operation}: {ExecutionTimeMs} мс", operationName, ms);
            else
                Log.Information("Операция {Operation} выполнена за {ExecutionTimeMs} мс", operationName, ms);
        }
    }
}
