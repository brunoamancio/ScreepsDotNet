using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class NullRuntimeTelemetrySink : IRuntimeTelemetrySink
{
    public static NullRuntimeTelemetrySink Instance { get; } = new();

    private NullRuntimeTelemetrySink()
    { }

    public Task PublishTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default) =>
        Task.CompletedTask;

    public Task PublishWatchdogAlertAsync(RuntimeWatchdogAlert alert, CancellationToken token = default) =>
        Task.CompletedTask;
}
