using ScreepsDotNet.Backend.Core.Models;

namespace ScreepsDotNet.Backend.Core.Repositories;

public interface IUserRepository
{
    Task<UserProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken = default);

    Task<int> GetActiveUsersCountAsync(CancellationToken cancellationToken = default);

    Task<UserPublicProfile?> FindPublicProfileAsync(string? username, string? userId, CancellationToken cancellationToken = default);

    Task UpdateNotifyPreferencesAsync(string userId, IDictionary<string, object?> notifyPreferences, CancellationToken cancellationToken = default);

    Task<bool> UpdateBadgeAsync(string userId, UserBadgeUpdate badge, CancellationToken cancellationToken = default);

    Task<EmailUpdateResult> UpdateEmailAsync(string userId, string email, CancellationToken cancellationToken = default);

    Task SetSteamVisibilityAsync(string userId, bool visible, CancellationToken cancellationToken = default);
}
