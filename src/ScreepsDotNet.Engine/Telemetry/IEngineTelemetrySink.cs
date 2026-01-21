namespace ScreepsDotNet.Engine.Telemetry;

/// <summary>
/// Receives Engine telemetry events and forwards to observability pipeline.
/// Implementations bridge Engine metrics to Driver's IRuntimeTelemetrySink or other exporters.
/// </summary>
public interface IEngineTelemetrySink
{
    Task PublishRoomTelemetryAsync(EngineTelemetryPayload payload, CancellationToken token = default);
}
