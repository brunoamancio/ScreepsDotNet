using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Queues;
using ScreepsDotNet.Driver.Constants;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class ProcessorLoop(
    IDriverConfig config,
    IQueueService queues,
    IProcessorLoopWorker worker,
    IOptions<ProcessorLoopOptions> options,
    IDriverLoopHooks loopHooks,
    ILogger<ProcessorLoop>? logger = null)
    : IProcessorLoop
{
    private readonly ProcessorLoopOptions _options = options.Value;
    private readonly IDriverLoopHooks _hooks = loopHooks;

    private IWorkQueueChannel? _queue;

    public async Task RunAsync(CancellationToken token = default)
    {
        _queue ??= queues.GetQueue(QueueNames.Rooms, QueueMode.Read);

        while (!token.IsCancellationRequested) {
            string? room = null;
            try {
                config.EmitProcessorLoopStage(LoopStageNames.Processor.Start);
                int? queueDepth = await _queue.GetPendingCountAsync(token).ConfigureAwait(false);
                room = await _queue.FetchAsync(_options.FetchTimeout, token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(room)) {
                    await PublishLoopTelemetryAsync(LoopStageNames.Processor.TelemetryIdle, QueueNames.Rooms, queueDepth, null, token).ConfigureAwait(false);
                    await Task.Delay(_options.IdleDelay, token).ConfigureAwait(false);
                    continue;
                }

                config.EmitProcessorLoopStage(LoopStageNames.Processor.ProcessRoom, room);
                await PublishLoopTelemetryAsync(LoopStageNames.Processor.TelemetryDequeue, room!, queueDepth, null, token).ConfigureAwait(false);
                await worker.HandleRoomAsync(room, queueDepth, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                break;
            }
            catch (Exception ex) {
                logger?.LogError(ex, "Processor loop failed for room {Room}.", room);
            }
            finally {
                if (!string.IsNullOrWhiteSpace(room)) {
                    try {
                        await _queue.MarkDoneAsync(room!, token).ConfigureAwait(false);
                    }
                    catch (Exception ex) {
                        logger?.LogWarning(ex, "Failed to mark processor queue item {Room} as done.", room);
                    }
                }

                config.EmitProcessorLoopStage(LoopStageNames.Processor.Finish, room);
            }
        }
    }

    private Task PublishLoopTelemetryAsync(string stage, string targetId, int? queueDepth, string? message, CancellationToken token)
    {
        var payload = new RuntimeTelemetryPayload(
            Loop: DriverProcessType.Processor,
            UserId: targetId,
            GameTime: 0,
            CpuLimit: 0,
            CpuBucket: 0,
            CpuUsed: 0,
            TimedOut: false,
            ScriptError: false,
            HeapUsedBytes: 0,
            HeapSizeLimitBytes: 0,
            ErrorMessage: message,
            QueueDepth: queueDepth,
            ColdStartRequested: false,
            Stage: stage);
        return _hooks.PublishRuntimeTelemetryAsync(payload, token);
    }
}
