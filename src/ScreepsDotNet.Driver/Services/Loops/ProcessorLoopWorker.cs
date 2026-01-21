using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Engine;
using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Constants;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class ProcessorLoopWorker(
    IRoomDataService roomDataService,
    IRoomSnapshotProvider snapshotProvider,
    IEnvironmentService environmentService,
    IDriverLoopHooks loopHooks,
    IDriverConfig config,
    IEngineHost engineHost,
    ILogger<ProcessorLoopWorker>? logger = null) : IProcessorLoopWorker
{
    public async Task HandleRoomAsync(string roomName, int? queueDepth, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        var gameTime = await environmentService.GetGameTimeAsync(token).ConfigureAwait(false);
        var scriptError = false;
        string? errorMessage = null;

        try {
            await engineHost.RunRoomAsync(roomName, gameTime, token).ConfigureAwait(false);
            await roomDataService.ClearRoomIntentsAsync(roomName, token).ConfigureAwait(false);
            snapshotProvider.Invalidate(roomName);

            var chunkSize = Math.Max(config.HistoryChunkSize, 1);
            if (gameTime % chunkSize == 0) {
                var chunkBase = Math.Max(gameTime - chunkSize + 1, 0);
                await loopHooks.UploadRoomHistoryChunkAsync(roomName, chunkBase, token).ConfigureAwait(false);
            }

            logger?.LogDebug("Engine processed room {Room} at tick {GameTime}.", roomName, gameTime);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            throw;
        }
        catch (Exception ex) {
            scriptError = true;
            errorMessage = ex.Message;
            logger?.LogError(ex, "Error processing room {Room} at tick {GameTime}.", roomName, gameTime);
        }

        var telemetry = new RuntimeTelemetryPayload(
            Loop: DriverProcessType.Processor,
            UserId: roomName,
            GameTime: gameTime,
            CpuLimit: 0,
            CpuBucket: 0,
            CpuUsed: 0,
            TimedOut: false,
            ScriptError: scriptError,
            HeapUsedBytes: 0,
            HeapSizeLimitBytes: 0,
            ErrorMessage: errorMessage,
            QueueDepth: queueDepth,
            ColdStartRequested: false,
            Stage: LoopStageNames.Processor.TelemetryProcessRoom);
        await loopHooks.PublishRuntimeTelemetryAsync(telemetry, token).ConfigureAwait(false);
    }
}
