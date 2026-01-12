using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Queues;
using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class RunnerLoop(
    IDriverConfig config,
    IQueueService queues,
    IRunnerLoopWorker worker,
    IRuntimeThrottleRegistry throttleRegistry,
    IOptions<RunnerLoopOptions> options,
    ILogger<RunnerLoop>? logger = null)
    : IRunnerLoop
{
    private readonly RunnerLoopOptions _options = options.Value;

    private IWorkQueueChannel? _queue;

    public async Task RunAsync(CancellationToken token = default)
    {
        _queue ??= queues.GetQueue(QueueNames.Users, QueueMode.Read);

        while (!token.IsCancellationRequested)
        {
            string? userId = null;
            try
            {
                config.EmitRunnerLoopStage("start");
                userId = await _queue.FetchAsync(_options.FetchTimeout, token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(userId))
                {
                    await Task.Delay(_options.IdleDelay, token).ConfigureAwait(false);
                    continue;
                }

                config.EmitRunnerLoopStage("runUser", userId);

                if (throttleRegistry.TryGetDelay(userId, out var delay) && delay > TimeSpan.Zero)
                {
                    logger?.LogWarning("Runner loop delaying user {UserId} for {Delay} due to telemetry throttle.", userId, delay);
                    await Task.Delay(delay, token).ConfigureAwait(false);
                }

                await worker.HandleUserAsync(userId, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Runner loop failed for user {UserId}.", userId);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    try
                    {
                        await _queue.MarkDoneAsync(userId!, token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to mark runner queue item {UserId} as done.", userId);
                    }
                }

                config.EmitRunnerLoopStage("finish", userId);
            }
        }
    }
}
