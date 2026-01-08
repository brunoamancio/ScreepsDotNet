namespace ScreepsDotNet.Backend.Core.Services;

using ScreepsDotNet.Backend.Core.Models.Strongholds;

/// <summary>
/// Encapsulates mutations against NPC strongholds (spawn, expand, etc.).
/// </summary>
public interface IStrongholdControlService
{
    Task<StrongholdSpawnResult> SpawnAsync(string roomName, StrongholdSpawnOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces the stronghold in the given room to expand (spawn a lesser invader core).
    /// </summary>
    Task<bool> ExpandAsync(string roomName, CancellationToken cancellationToken = default);
}
