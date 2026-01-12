using ScreepsDotNet.Driver.Abstractions.Loops;

namespace ScreepsDotNet.Driver.Abstractions.Eventing;

public sealed class RuntimeTelemetryEventArgs(RuntimeTelemetryPayload payload) : EventArgs
{
    public RuntimeTelemetryPayload Payload { get; } = payload;
}
