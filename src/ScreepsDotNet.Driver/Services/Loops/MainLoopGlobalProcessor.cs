using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Engine;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Abstractions.Users;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class MainLoopGlobalProcessor(
    IUserDataService userDataService,
    IInterRoomSnapshotProvider snapshotProvider,
    IEngineHost engineHost,
    ILogger<MainLoopGlobalProcessor>? logger = null)
    : IMainLoopGlobalProcessor
{
    public async Task ExecuteAsync(CancellationToken token = default)
    {
        await userDataService.ClearGlobalIntentsAsync(token).ConfigureAwait(false);

        var snapshot = await snapshotProvider.GetSnapshotAsync(token).ConfigureAwait(false);

        try {
            await engineHost.RunGlobalAsync(snapshot.GameTime, token).ConfigureAwait(false);
            logger?.LogDebug(
                "Engine global processor ran at tick {GameTime} ({Rooms} accessible rooms, {Creeps} moving creeps).",
                snapshot.GameTime,
                snapshot.AccessibleRooms.Count,
                snapshot.MovingCreeps.Count);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            throw;
        }
        catch (Exception ex) {
            logger?.LogError(ex, "Error in engine global processor at tick {GameTime}.", snapshot.GameTime);
            throw;
        }
    }
}
