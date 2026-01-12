using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Eventing;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Abstractions.Notifications;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class RuntimeTelemetryMonitor : IRuntimeWatchdog, IDisposable
{
    private readonly IDriverConfig _config;
    private readonly INotificationService _notifications;
    private readonly IRuntimeTelemetrySink _telemetry;
    private readonly ILogger<RuntimeTelemetryMonitor> _logger;
    private readonly ConcurrentDictionary<string, WatchdogState> _watchdogStates = new(StringComparer.Ordinal);
    private static readonly TimeSpan NotificationCooldown = TimeSpan.FromMinutes(10);
    private const int FailureThreshold = 3;
    private bool _disposed;

    public RuntimeTelemetryMonitor(IDriverConfig config, INotificationService notifications, IRuntimeTelemetrySink telemetry, ILogger<RuntimeTelemetryMonitor> logger)
    {
        _config = config;
        _notifications = notifications;
        _telemetry = telemetry;
        _logger = logger;
        _config.RuntimeTelemetry += HandleTelemetry;
    }

    private async void HandleTelemetry(object? sender, RuntimeTelemetryEventArgs args)
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

        try
        {
            await ProcessWatchdogAsync(payload).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Runtime watchdog processing failed for user {UserId}.", payload.UserId);
        }
    }

    private async Task ProcessWatchdogAsync(RuntimeTelemetryPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.UserId))
            return;

        if (!(payload.TimedOut || payload.ScriptError))
        {
            _watchdogStates.TryRemove(payload.UserId, out _);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var state = _watchdogStates.GetOrAdd(payload.UserId, _ => new WatchdogState());

        var consecutive = state.Increment(now);
        if (consecutive < FailureThreshold)
            return;

        if (state.RequestColdStart())
        {
            _logger.LogWarning("Runtime watchdog requesting cold sandbox restart for user {UserId} after {Consecutive} consecutive failures.", payload.UserId, consecutive);
            var alert = new RuntimeWatchdogAlert(payload, consecutive, now);
            await _telemetry.PublishWatchdogAlertAsync(alert).ConfigureAwait(false);
        }

        if (now - state.LastNotification < NotificationCooldown)
            return;

        state.LastNotification = now;
        var message = $"Watchdog: runtime for {payload.UserId} failed {consecutive} consecutive ticks (timedOut={payload.TimedOut}, scriptError={payload.ScriptError}) at tick {payload.GameTime}.";
        await _notifications.SendNotificationAsync(payload.UserId, message, new NotificationOptions(15, "watchdog")).ConfigureAwait(false);
    }

    public bool TryConsumeColdStartRequest(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        return _watchdogStates.TryGetValue(userId, out var state) && state.TryConsumeColdStartRequest();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _config.RuntimeTelemetry -= HandleTelemetry;
        _disposed = true;
    }

    private sealed class WatchdogState
    {
        private int _consecutiveFailures;
        private int _pendingColdStarts;
        public DateTimeOffset LastFailure { get; private set; }
        public DateTimeOffset LastNotification { get; set; }

        public int Increment(DateTimeOffset timestamp)
        {
            LastFailure = timestamp;
            return Interlocked.Increment(ref _consecutiveFailures);
        }

        public bool RequestColdStart()
        {
            var pending = Interlocked.Increment(ref _pendingColdStarts);
            return pending == 1;
        }

        public bool TryConsumeColdStartRequest()
        {
            while (true)
            {
                var pending = Volatile.Read(ref _pendingColdStarts);
                if (pending == 0)
                    return false;
                if (Interlocked.CompareExchange(ref _pendingColdStarts, pending - 1, pending) == pending)
                    return true;
            }
        }
    }
}
