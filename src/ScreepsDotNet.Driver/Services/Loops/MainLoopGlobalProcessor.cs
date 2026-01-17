using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Engine;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Abstractions.Users;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class MainLoopGlobalProcessor(
    IUserDataService userDataService,
    IInterRoomSnapshotProvider snapshotProvider,
    IInterRoomTransferProcessor transferProcessor,
    IEngineHost? engineHost = null,
    ILogger<MainLoopGlobalProcessor>? logger = null)
    : IMainLoopGlobalProcessor
{
    public async Task ExecuteAsync(CancellationToken token = default)
    {
        await userDataService.ClearGlobalIntentsAsync(token).ConfigureAwait(false);

        var snapshot = await snapshotProvider.GetSnapshotAsync(token).ConfigureAwait(false);

        if (engineHost is not null)
        {
            await engineHost.RunGlobalAsync(snapshot.GameTime, token).ConfigureAwait(false);
            logger?.LogDebug(
                "Engine global processor ran at tick {GameTime} ({Rooms} accessible rooms, {Creeps} moving creeps).",
                snapshot.GameTime,
                snapshot.AccessibleRooms.Count,
                snapshot.MovingCreeps.Count);
            return;
        }

        var moved = await transferProcessor.ProcessTransfersAsync(snapshot.AccessibleRooms, token).ConfigureAwait(false);

        if (moved > 0)
            snapshotProvider.Invalidate();

        logger?.LogDebug(
            "Inter-room snapshot captured {Rooms} accessible rooms, {Creeps} moving creeps; processed {Moved} transfers at tick {GameTime}.",
            snapshot.AccessibleRooms.Count,
            snapshot.MovingCreeps.Count,
            moved,
            snapshot.GameTime);
    }
}
