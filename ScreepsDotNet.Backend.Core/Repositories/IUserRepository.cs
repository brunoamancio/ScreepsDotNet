using ScreepsDotNet.Backend.Core.Models;

namespace ScreepsDotNet.Backend.Core.Repositories;

public interface IUserRepository
{
    Task<UserProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken = default);

    Task<int> GetActiveUsersCountAsync(CancellationToken cancellationToken = default);

    Task<UserPublicProfile?> FindPublicProfileAsync(string? username, string? userId, CancellationToken cancellationToken = default);
}
