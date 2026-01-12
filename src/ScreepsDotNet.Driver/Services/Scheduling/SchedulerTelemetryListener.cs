using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Scheduling;

internal sealed class SchedulerTelemetryListener(
    IRuntimeThrottleRegistry throttleRegistry,
    IOptions<SchedulerTelemetryOptions> options,
    ILogger<SchedulerTelemetryListener>? logger = null) : IRuntimeTelemetryListener
{
    private readonly IRuntimeThrottleRegistry _throttleRegistry = throttleRegistry;
    private readonly SchedulerTelemetryOptions _options = options.Value;
    private readonly ILogger<SchedulerTelemetryListener>? _logger = logger;
    private readonly ConcurrentDictionary<string, FailureState> _states = new(StringComparer.Ordinal);

    public Task OnTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!(payload.TimedOut || payload.ScriptError))
        {
            _states.TryRemove(payload.UserId, out _);
            _throttleRegistry.Clear(payload.UserId);
            return Task.CompletedTask;
        }

        var state = _states.GetOrAdd(payload.UserId, _ => new FailureState());
        var failures = state.Increment();
        if (failures < Math.Max(1, _options.FailureThreshold))
            return Task.CompletedTask;

        _throttleRegistry.RegisterThrottle(payload.UserId, _options.ThrottleDuration);
        _logger?.LogWarning(
            "Scheduler telemetry throttling user {UserId} for {Duration} after {Failures} consecutive failures (timedOut={TimedOut}, scriptError={ScriptError}).",
            payload.UserId,
            _options.ThrottleDuration,
            failures,
            payload.TimedOut,
            payload.ScriptError);
        return Task.CompletedTask;
    }

    public Task OnWatchdogAlertAsync(RuntimeWatchdogAlert alert, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(alert);
        _throttleRegistry.RegisterThrottle(alert.Payload.UserId, _options.ThrottleDuration);
        _logger?.LogWarning("Watchdog throttling user {UserId} for {Duration}.", alert.Payload.UserId, _options.ThrottleDuration);
        return Task.CompletedTask;
    }

    private sealed class FailureState
    {
        private int _count;
        public int Increment() => Interlocked.Increment(ref _count);
    }
}
