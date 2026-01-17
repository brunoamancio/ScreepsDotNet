using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Queues;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Constants;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class RunnerLoop(
    IDriverConfig config,
    IQueueService queues,
    IRunnerLoopWorker worker,
    IRuntimeThrottleRegistry throttleRegistry,
    IOptions<RunnerLoopOptions> options,
    IDriverLoopHooks loopHooks,
    ILogger<RunnerLoop>? logger = null)
    : IRunnerLoop
{
    private readonly RunnerLoopOptions _options = options.Value;
    private readonly IDriverLoopHooks _hooks = loopHooks;

    private IWorkQueueChannel? _queue;

    public async Task RunAsync(CancellationToken token = default)
    {
        _queue ??= queues.GetQueue(QueueNames.Users, QueueMode.Read);

        while (!token.IsCancellationRequested) {
            string? userId = null;
            int? queueDepth = null;
            try {
                config.EmitRunnerLoopStage(LoopStageNames.Runner.Start);
                queueDepth = await _queue.GetPendingCountAsync(token).ConfigureAwait(false);
                userId = await _queue.FetchAsync(_options.FetchTimeout, token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(userId)) {
                    await PublishLoopTelemetryAsync(LoopStageNames.Runner.TelemetryIdle, QueueNames.Users, queueDepth, null, token).ConfigureAwait(false);
                    await Task.Delay(_options.IdleDelay, token).ConfigureAwait(false);
                    continue;
                }

                config.EmitRunnerLoopStage(LoopStageNames.Runner.RunUser, userId);
                await PublishLoopTelemetryAsync(LoopStageNames.Runner.TelemetryDequeue, userId!, queueDepth, null, token).ConfigureAwait(false);

                if (throttleRegistry.TryGetDelay(userId, out var delay) && delay > TimeSpan.Zero) {
                    logger?.LogWarning("Runner loop delaying user {UserId} for {Delay} due to telemetry throttle.", userId, delay);
                    await PublishLoopTelemetryAsync(LoopStageNames.Runner.TelemetryThrottleDelay, userId!, queueDepth, $"delay={delay.TotalMilliseconds:F0}ms", token).ConfigureAwait(false);
                    await Task.Delay(delay, token).ConfigureAwait(false);
                }

                await worker.HandleUserAsync(userId, queueDepth, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                break;
            }
            catch (Exception ex) {
                logger?.LogError(ex, "Runner loop failed for user {UserId}.", userId);
            }
            finally {
                if (!string.IsNullOrWhiteSpace(userId)) {
                    try {
                        await _queue.MarkDoneAsync(userId!, token).ConfigureAwait(false);
                    }
                    catch (Exception ex) {
                        logger?.LogWarning(ex, "Failed to mark runner queue item {UserId} as done.", userId);
                    }
                }

                config.EmitRunnerLoopStage(LoopStageNames.Runner.Finish, userId);
            }
        }
    }

    private Task PublishLoopTelemetryAsync(string stage, string targetId, int? queueDepth, string? message, CancellationToken token)
    {
        var payload = new RuntimeTelemetryPayload(
            Loop: DriverProcessType.Runner,
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
