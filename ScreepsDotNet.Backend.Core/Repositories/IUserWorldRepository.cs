using ScreepsDotNet.Backend.Core.Models;

namespace ScreepsDotNet.Backend.Core.Repositories;

public interface IUserWorldRepository
{
    Task<string?> GetRandomControllerRoomAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserWorldStatus> GetWorldStatusAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetControllerRoomsAsync(string userId, CancellationToken cancellationToken = default);
}
