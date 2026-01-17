using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Queues;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Abstractions.Users;
using ScreepsDotNet.Driver.Constants;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class MainLoop(IDriverConfig config, IQueueService queues, IUserDataService users, IRoomDataService rooms,
                               IEnvironmentService environment, IDriverLoopHooks hooks, IMainLoopGlobalProcessor globalProcessor,
                               IOptions<MainLoopOptions> options, ILogger<MainLoop>? logger = null)
    : IMainLoop
{
    private readonly MainLoopOptions _options = options.Value;

    private IWorkQueueChannel? _roomsQueue;
    private IWorkQueueChannel? _usersQueue;

    public async Task RunAsync(CancellationToken token = default)
    {
        _roomsQueue ??= queues.GetQueue(QueueNames.Rooms, QueueMode.Write);
        _usersQueue ??= queues.GetQueue(QueueNames.Users, QueueMode.Write);

        var shouldExit = false;
        while (!token.IsCancellationRequested && !shouldExit) {
            var stopwatch = Stopwatch.StartNew();
            try {
                await ExecuteOnceAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                shouldExit = true;
            }
            catch (Exception ex) {
                logger?.LogError(ex, "Unexpected error inside main loop.");
            }
            finally {
                stopwatch.Stop();
                var remaining = config.MainLoopMinDurationMs - stopwatch.ElapsedMilliseconds;
                if (remaining > 0) {
                    try {
                        await Task.Delay(TimeSpan.FromMilliseconds(remaining), token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) {
                        shouldExit = true;
                    }
                }
            }
        }
    }

    private async Task ExecuteOnceAsync(CancellationToken token)
    {
        config.EmitMainLoopStage(LoopStageNames.Main.Start);
        await environment.NotifyTickStartedAsync(token).ConfigureAwait(false);
        var currentGameTime = await environment.GetGameTimeAsync(token).ConfigureAwait(false);

        config.EmitMainLoopStage(LoopStageNames.Main.GetUsers);
        var users1 = await users.GetActiveUsersAsync(token).ConfigureAwait(false);

        var userIds = users1.Select(u => u.Id)
                           .Where(id => !string.IsNullOrWhiteSpace(id))
                           .Select(id => id!)
                           .ToArray();

        config.EmitMainLoopStage(LoopStageNames.Main.AddUsersToQueue, userIds);
        if (userIds.Length > 0)
            await _usersQueue!.EnqueueManyAsync(userIds, token).ConfigureAwait(false);
        var usersDepth = await _usersQueue!.GetPendingCountAsync(token).ConfigureAwait(false);
        await PublishQueueDepthAsync(LoopStageNames.Main.TelemetryEnqueueUsers, QueueNames.Users, currentGameTime, usersDepth, token).ConfigureAwait(false);

        config.EmitMainLoopStage(LoopStageNames.Main.WaitForUsers);
        await _usersQueue!.WaitUntilDrainedAsync(token).ConfigureAwait(false);
        var usersDrainedDepth = await _usersQueue.GetPendingCountAsync(token).ConfigureAwait(false);
        await PublishQueueDepthAsync(LoopStageNames.Main.TelemetryDrainUsers, $"{QueueNames.Users}:drained", currentGameTime, usersDrainedDepth, token).ConfigureAwait(false);

        config.EmitMainLoopStage(LoopStageNames.Main.GetRooms);
        var rooms1 = await rooms.DrainActiveRoomsAsync(token).ConfigureAwait(false);
        var roomNames = rooms1.Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();

        config.EmitMainLoopStage(LoopStageNames.Main.AddRoomsToQueue, roomNames);
        if (roomNames.Length > 0)
            await _roomsQueue!.EnqueueManyAsync(roomNames, token).ConfigureAwait(false);
        var roomsDepth = await _roomsQueue!.GetPendingCountAsync(token).ConfigureAwait(false);
        await PublishQueueDepthAsync(LoopStageNames.Main.TelemetryEnqueueRooms, QueueNames.Rooms, currentGameTime, roomsDepth, token).ConfigureAwait(false);

        config.EmitMainLoopStage(LoopStageNames.Main.WaitForRooms);
        await _roomsQueue!.WaitUntilDrainedAsync(token).ConfigureAwait(false);
        var roomsDrainedDepth = await _roomsQueue.GetPendingCountAsync(token).ConfigureAwait(false);
        await PublishQueueDepthAsync(LoopStageNames.Main.TelemetryDrainRooms, $"{QueueNames.Rooms}:drained", currentGameTime, roomsDrainedDepth, token).ConfigureAwait(false);

        config.EmitMainLoopStage(LoopStageNames.Main.CommitDbBulkPre);
        await environment.CommitDatabaseBulkAsync(token).ConfigureAwait(false);

        config.EmitMainLoopStage(LoopStageNames.Main.Global);
        await globalProcessor.ExecuteAsync(token).ConfigureAwait(false);

        config.EmitMainLoopStage(LoopStageNames.Main.CommitDbBulkPost);
        await environment.CommitDatabaseBulkAsync(token).ConfigureAwait(false);

        config.EmitMainLoopStage(LoopStageNames.Main.IncrementGameTime);
        var gameTime = await environment.IncrementGameTimeAsync(token).ConfigureAwait(false);

        config.EmitMainLoopStage(LoopStageNames.Main.UpdateAccessibleRooms);
        await rooms.UpdateAccessibleRoomsListAsync(token).ConfigureAwait(false);
        await rooms.UpdateRoomStatusDataAsync(token).ConfigureAwait(false);

        config.EmitMainLoopStage(LoopStageNames.Main.NotifyRoomsDone);
        await hooks.NotifyRoomsDoneAsync(gameTime, token).ConfigureAwait(false);

        config.EmitMainLoopStage(LoopStageNames.Main.Finish);
    }

    private Task PublishQueueDepthAsync(string stage, string targetId, int gameTime, int queueDepth, CancellationToken token)
    {
        var payload = new RuntimeTelemetryPayload(
            Loop: DriverProcessType.Main,
            UserId: targetId,
            GameTime: gameTime,
            CpuLimit: 0,
            CpuBucket: 0,
            CpuUsed: 0,
            TimedOut: false,
            ScriptError: false,
            HeapUsedBytes: 0,
            HeapSizeLimitBytes: 0,
            ErrorMessage: null,
            QueueDepth: queueDepth,
            ColdStartRequested: false,
            Stage: stage);
        return hooks.PublishRuntimeTelemetryAsync(payload, token);
    }
}
