namespace ScreepsDotNet.Backend.Core.Repositories;

using ScreepsDotNet.Backend.Core.Models;

public interface IUserWorldRepository
{
    Task<RoomReference?> GetRandomControllerRoomAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserWorldStatus> GetWorldStatusAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RoomReference>> GetControllerRoomsAsync(string userId, CancellationToken cancellationToken = default);
}
