using ScreepsDotNet.Driver.Abstractions.Loops;

namespace ScreepsDotNet.Driver.Abstractions.Runtime;

public interface IRuntimeTelemetrySink
{
    Task PublishTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default);
    Task PublishWatchdogAlertAsync(RuntimeWatchdogAlert alert, CancellationToken token = default);
}

public sealed record RuntimeWatchdogAlert(RuntimeTelemetryPayload Payload, int ConsecutiveFailures, DateTimeOffset Timestamp);
