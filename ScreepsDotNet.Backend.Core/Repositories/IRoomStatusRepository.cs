namespace ScreepsDotNet.Backend.Core.Repositories;

using System.Collections.Generic;
using ScreepsDotNet.Backend.Core.Models;

public interface IRoomStatusRepository
{
    Task<RoomStatusInfo?> GetRoomStatusAsync(string roomName, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, RoomStatusInfo>> GetRoomStatusesAsync(IEnumerable<string> roomNames, CancellationToken cancellationToken = default);
}
