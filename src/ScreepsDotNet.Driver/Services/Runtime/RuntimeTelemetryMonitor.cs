using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Eventing;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class RuntimeTelemetryMonitor : IDisposable
{
    private readonly IDriverConfig _config;
    private readonly ILogger<RuntimeTelemetryMonitor> _logger;
    private bool _disposed;

    public RuntimeTelemetryMonitor(IDriverConfig config, ILogger<RuntimeTelemetryMonitor> logger)
    {
        _config = config;
        _logger = logger;
        _config.RuntimeTelemetry += HandleTelemetry;
    }

    private void HandleTelemetry(object? sender, RuntimeTelemetryEventArgs args)
    {
        var payload = args.Payload;
        var level = payload.TimedOut || payload.ScriptError ? LogLevel.Warning : LogLevel.Debug;
        _logger.Log(level,
            "Runtime telemetry for user {UserId} tick {GameTime}: CPU {CpuUsed}/{CpuLimit}ms bucket {CpuBucket}, heap {HeapUsed}/{HeapLimit} bytes, timedOut={TimedOut}, scriptError={ScriptError}.",
            payload.UserId,
            payload.GameTime,
            payload.CpuUsed,
            payload.CpuLimit,
            payload.CpuBucket,
            payload.HeapUsedBytes,
            payload.HeapSizeLimitBytes,
            payload.TimedOut,
            payload.ScriptError);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _config.RuntimeTelemetry -= HandleTelemetry;
        _disposed = true;
    }
}
