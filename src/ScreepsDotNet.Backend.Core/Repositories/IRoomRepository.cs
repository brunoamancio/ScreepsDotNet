namespace ScreepsDotNet.Backend.Core.Repositories;

using ScreepsDotNet.Backend.Core.Models;

public interface IRoomRepository
{
    Task<IReadOnlyCollection<RoomSummary>> GetOwnedRoomsAsync(CancellationToken cancellationToken = default);
}
