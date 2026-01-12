namespace ScreepsDotNet.Backend.Core.Services;

using ScreepsDotNet.Backend.Core.Models.Map;

/// <summary>
/// Provides administrative control over world map data (room generation, status toggles, asset refresh).
/// </summary>
public interface IMapControlService
{
    Task<MapGenerationResult> GenerateRoomAsync(MapRoomGenerationOptions options, CancellationToken cancellationToken = default);

    Task OpenRoomAsync(string roomName, string? shardName, CancellationToken cancellationToken = default);

    Task CloseRoomAsync(string roomName, string? shardName, CancellationToken cancellationToken = default);

    Task RemoveRoomAsync(string roomName, string? shardName, bool purgeObjects, CancellationToken cancellationToken = default);

    Task UpdateRoomAssetsAsync(string roomName, string? shardName, bool fullRegeneration, CancellationToken cancellationToken = default);

    Task RefreshTerrainCacheAsync(CancellationToken cancellationToken = default);
}
