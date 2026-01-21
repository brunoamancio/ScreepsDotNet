using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Engine.Constants;

namespace ScreepsDotNet.Engine.Telemetry;

/// <summary>
/// Default implementation of IEngineTelemetrySink that bridges Engine telemetry to Driver hooks.
/// Emits Engine metrics as RuntimeTelemetryPayload with custom stage prefix "engine:room:{roomName}".
/// </summary>
internal sealed class EngineTelemetrySink(IDriverLoopHooks driverHooks) : IEngineTelemetrySink
{
    public Task PublishRoomTelemetryAsync(EngineTelemetryPayload payload, CancellationToken token = default)
    {
        // Bridge to Driver telemetry (extend RuntimeTelemetryPayload or emit separately)
        // Option A: Emit as custom stage in RuntimeTelemetryPayload
        // Option B: Add EngineTelemetryPayload to IDriverLoopHooks (requires D8.1 extension)

        // Emit as RuntimeTelemetryPayload with Stage="engine:room:{roomName}"
        var bridgedPayload = new RuntimeTelemetryPayload(
            Loop: DriverProcessType.Processor,
            UserId: string.Empty,  // Engine processes rooms, not users
            GameTime: payload.GameTime,
            CpuLimit: 0,
            CpuBucket: 0,
            CpuUsed: (int)payload.ProcessingTimeMs,  // Approximate
            TimedOut: false,
            ScriptError: false,
            HeapUsedBytes: 0,
            HeapSizeLimitBytes: 0,
            ErrorMessage: null,
            QueueDepth: null,
            ColdStartRequested: false,
            Stage: EngineTelemetryConstants.FormatEngineRoomStage(payload.RoomName)
        );

        return driverHooks.PublishRuntimeTelemetryAsync(bridgedPayload, token);
    }
}
