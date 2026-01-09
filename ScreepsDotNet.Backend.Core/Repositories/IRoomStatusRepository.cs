namespace ScreepsDotNet.Backend.Core.Repositories;

using System.Collections.Generic;
using ScreepsDotNet.Backend.Core.Models;

public interface IRoomStatusRepository
{
    Task<RoomStatusInfo?> GetRoomStatusAsync(string roomName, string? shardName = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, RoomStatusInfo>> GetRoomStatusesAsync(IEnumerable<RoomReference> rooms, CancellationToken cancellationToken = default);
}
