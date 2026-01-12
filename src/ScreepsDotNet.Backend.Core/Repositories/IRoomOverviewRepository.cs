namespace ScreepsDotNet.Backend.Core.Repositories;

using ScreepsDotNet.Backend.Core.Models;

public interface IRoomOverviewRepository
{
    Task<RoomOverview?> GetRoomOverviewAsync(RoomReference room, CancellationToken cancellationToken = default);
}
