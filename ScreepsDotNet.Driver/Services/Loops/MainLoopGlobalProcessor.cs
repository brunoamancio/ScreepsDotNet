using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Abstractions.Users;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class MainLoopGlobalProcessor(IUserDataService userDataService, IRoomDataService roomDataService, ILogger<MainLoopGlobalProcessor>? logger = null)
    : IMainLoopGlobalProcessor
{
    private readonly IUserDataService _users = userDataService;
    private readonly IRoomDataService _rooms = roomDataService;
    private readonly ILogger<MainLoopGlobalProcessor>? _logger = logger;

    public async Task ExecuteAsync(CancellationToken token = default)
    {
        await _users.ClearGlobalIntentsAsync(token).ConfigureAwait(false);

        var snapshot = await _rooms.GetInterRoomSnapshotAsync(token).ConfigureAwait(false);
        _logger?.LogDebug("Inter-room snapshot captured {Rooms} accessible rooms and {Creeps} moving creeps at tick {GameTime}.",
            snapshot.AccessibleRooms.Count, snapshot.MovingCreeps.Count, snapshot.GameTime);
    }
}
