using ScreepsDotNet.Driver.Abstractions.Loops;

namespace ScreepsDotNet.Driver.Abstractions.Runtime;

public interface IRuntimeTelemetryListener
{
    Task OnTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default);
    Task OnWatchdogAlertAsync(RuntimeWatchdogAlert alert, CancellationToken token = default);
}
