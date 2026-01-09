namespace ScreepsDotNet.Backend.Core.Repositories;

using System.Collections.Generic;
using ScreepsDotNet.Backend.Core.Models;

public interface IRoomTerrainRepository
{
    Task<IReadOnlyList<RoomTerrainData>> GetTerrainEntriesAsync(IEnumerable<RoomReference> rooms, CancellationToken cancellationToken = default);
}
