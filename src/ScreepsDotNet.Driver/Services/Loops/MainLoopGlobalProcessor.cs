using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Abstractions.Users;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class MainLoopGlobalProcessor(
    IUserDataService userDataService,
    IInterRoomSnapshotProvider snapshotProvider,
    ILogger<MainLoopGlobalProcessor>? logger = null)
    : IMainLoopGlobalProcessor
{
    public async Task ExecuteAsync(CancellationToken token = default)
    {
        await userDataService.ClearGlobalIntentsAsync(token).ConfigureAwait(false);

        var snapshot = await snapshotProvider.GetSnapshotAsync(token).ConfigureAwait(false);
        logger?.LogDebug("Inter-room snapshot captured {Rooms} accessible rooms and {Creeps} moving creeps at tick {GameTime}.",
            snapshot.AccessibleRooms.Count, snapshot.MovingCreeps.Count, snapshot.GameTime);
    }
}
