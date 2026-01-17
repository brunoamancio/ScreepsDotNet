namespace ScreepsDotNet.Backend.Core.Repositories;

using ScreepsDotNet.Backend.Core.Models;

public interface IRoomTerrainRepository
{
    Task<IReadOnlyList<RoomTerrainData>> GetTerrainEntriesAsync(IEnumerable<RoomReference> rooms, CancellationToken cancellationToken = default);
}
