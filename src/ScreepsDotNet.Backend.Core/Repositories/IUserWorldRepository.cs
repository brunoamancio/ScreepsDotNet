using ScreepsDotNet.Backend.Core.Models;

namespace ScreepsDotNet.Backend.Core.Repositories;

public interface IUserWorldRepository
{
    Task<RoomReference?> GetRandomControllerRoomAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserWorldStatus> GetWorldStatusAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RoomReference>> GetControllerRoomsAsync(string userId, CancellationToken cancellationToken = default);
}
