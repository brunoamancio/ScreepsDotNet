using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Queues;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Abstractions.Users;

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
        while (!token.IsCancellationRequested && !shouldExit)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await ExecuteOnceAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                shouldExit = true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Unexpected error inside main loop.");
            }
            finally
            {
                stopwatch.Stop();
                var remaining = config.MainLoopMinDurationMs - stopwatch.ElapsedMilliseconds;
                if (remaining > 0)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(remaining), token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        shouldExit = true;
                    }
                }
            }
        }
    }

    private async Task ExecuteOnceAsync(CancellationToken token)
    {
        config.EmitMainLoopStage("start");
        await environment.NotifyTickStartedAsync(token).ConfigureAwait(false);

        config.EmitMainLoopStage("getUsers");
        var users1 = await users.GetActiveUsersAsync(token).ConfigureAwait(false);

        var userIds = users1.Select(u => u.Id)
                           .Where(id => !string.IsNullOrWhiteSpace(id))
                           .Select(id => id!)
                           .ToArray();

        config.EmitMainLoopStage("addUsersToQueue", userIds);
        if (userIds.Length > 0)
            await _usersQueue!.EnqueueManyAsync(userIds, token).ConfigureAwait(false);

        config.EmitMainLoopStage("waitForUsers");
        await _usersQueue!.WaitUntilDrainedAsync(token).ConfigureAwait(false);

        config.EmitMainLoopStage("getRooms");
        var rooms1 = await rooms.DrainActiveRoomsAsync(token).ConfigureAwait(false);
        var roomNames = rooms1.Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();

        config.EmitMainLoopStage("addRoomsToQueue", roomNames);
        if (roomNames.Length > 0)
            await _roomsQueue!.EnqueueManyAsync(roomNames, token).ConfigureAwait(false);

        config.EmitMainLoopStage("waitForRooms");
        await _roomsQueue!.WaitUntilDrainedAsync(token).ConfigureAwait(false);

        config.EmitMainLoopStage("commitDbBulk:pre");
        await environment.CommitDatabaseBulkAsync(token).ConfigureAwait(false);

        config.EmitMainLoopStage("global");
        await globalProcessor.ExecuteAsync(token).ConfigureAwait(false);

        config.EmitMainLoopStage("commitDbBulk:post");
        await environment.CommitDatabaseBulkAsync(token).ConfigureAwait(false);

        config.EmitMainLoopStage("incrementGameTime");
        var gameTime = await environment.IncrementGameTimeAsync(token).ConfigureAwait(false);

        config.EmitMainLoopStage("updateAccessibleRooms");
        await rooms.UpdateAccessibleRoomsListAsync(token).ConfigureAwait(false);
        await rooms.UpdateRoomStatusDataAsync(token).ConfigureAwait(false);

        config.EmitMainLoopStage("notifyRoomsDone");
        await hooks.NotifyRoomsDoneAsync(gameTime, token).ConfigureAwait(false);

        config.EmitMainLoopStage("finish");
    }
}
