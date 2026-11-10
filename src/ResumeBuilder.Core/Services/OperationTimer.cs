using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ResumeBuilder.Core.Services;

/// <summary>
/// Замер длительности операции с записью в журнал (лаба «Журналирование», задания 3.1 и 3.3).
/// </summary>
/// <remarks>
/// <para>При создании открывает область журнала (scope) с <c>CorrelationId</c> — все записи внутри
/// операции, даже из других классов, получают один и тот же идентификатор, и по нему можно
/// восстановить всю цепочку событий.</para>
/// <para>При завершении пишет длительность: быстрые операции — на уровне Debug,
/// медленные (дольше порога) — Warning. В основной файл журнала пишутся только Warning и выше,
/// поэтому в «продакшн-режиме» туда попадают только медленные операции.</para>
/// </remarks>
/// <example>
/// <code>
/// using (OperationTimer.Start(logger, "ExportPdf"))
/// {
///     exporter.Export(resume, stream, options);
/// }
/// </code>
/// </example>
public sealed class OperationTimer : IDisposable
{
    /// <summary>Порог «медленной» операции по умолчанию, мс. Переопределяется в appsettings.json.</summary>
    public static int SlowThresholdMs { get; set; } = 1000;

    private readonly ILogger _logger;
    private readonly string _operation;
    private readonly IDisposable? _scope;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private bool _failed;

    private OperationTimer(ILogger logger, string operation, string correlationId)
    {
        _logger = logger;
        _operation = operation;
        CorrelationId = correlationId;
        _scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId, ["Operation"] = operation });
        _logger.LogDebug("Операция {Operation} начата", operation);
    }

    /// <summary>Идентификатор корреляции операции.</summary>
    public string CorrelationId { get; }

    /// <summary>Прошло миллисекунд.</summary>
    public long ElapsedMs => _stopwatch.ElapsedMilliseconds;

    /// <summary>Начинает замер.</summary>
    public static OperationTimer Start(ILogger logger, string operation, string? correlationId = null) =>
        new(logger, operation, correlationId ?? Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Помечает операцию как неуспешную.</summary>
    public void Fail(Exception ex)
    {
        _failed = true;
        _logger.LogError(ex, "Операция {Operation} завершилась ошибкой", _operation);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopwatch.Stop();
        var ms = _stopwatch.ElapsedMilliseconds;
        if (!_failed)
        {
            if (ms > SlowThresholdMs)
                _logger.LogWarning("Медленная операция {Operation}: {ExecutionTimeMs} мс (порог {ThresholdMs} мс)", _operation, ms, SlowThresholdMs);
            else
                _logger.LogDebug("Операция {Operation} выполнена за {ExecutionTimeMs} мс", _operation, ms);
        }
        _scope?.Dispose();
    }
}
