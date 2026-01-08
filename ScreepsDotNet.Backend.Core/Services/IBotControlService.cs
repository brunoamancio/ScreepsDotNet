namespace ScreepsDotNet.Backend.Core.Services;

using ScreepsDotNet.Backend.Core.Models.Bots;

/// <summary>
/// Provides administrative control over NPC bot users.
/// </summary>
public interface IBotControlService
{
    Task<BotSpawnResult> SpawnAsync(string botName, string roomName, BotSpawnOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads (re-syncs) the AI modules for every bot using the specified definition.
    /// </summary>
    Task<int> ReloadAsync(string botName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a bot account and all associated runtime artifacts.
    /// </summary>
    Task<bool> RemoveAsync(string username, CancellationToken cancellationToken = default);
}
