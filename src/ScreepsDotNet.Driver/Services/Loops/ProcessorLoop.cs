using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Queues;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class ProcessorLoop(IDriverConfig config, IQueueService queues, IProcessorLoopWorker worker, IOptions<ProcessorLoopOptions> options, ILogger<ProcessorLoop>? logger = null)
    : IProcessorLoop
{
    private readonly ProcessorLoopOptions _options = options.Value;

    private IWorkQueueChannel? _queue;

    public async Task RunAsync(CancellationToken token = default)
    {
        _queue ??= queues.GetQueue(QueueNames.Rooms, QueueMode.Read);

        while (!token.IsCancellationRequested)
        {
            string? room = null;
            try
            {
                config.EmitProcessorLoopStage("start");
                room = await _queue.FetchAsync(_options.FetchTimeout, token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(room))
                {
                    await Task.Delay(_options.IdleDelay, token).ConfigureAwait(false);
                    continue;
                }

                config.EmitProcessorLoopStage("processRoom", room);
                await worker.HandleRoomAsync(room, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Processor loop failed for room {Room}.", room);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(room))
                {
                    try
                    {
                        await _queue.MarkDoneAsync(room!, token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to mark processor queue item {Room} as done.", room);
                    }
                }

                config.EmitProcessorLoopStage("finish", room);
            }
        }
    }
}
