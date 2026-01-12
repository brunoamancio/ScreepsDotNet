namespace ScreepsDotNet.Backend.Core.Models.Map;

/// <summary>
/// Parameters accepted by the CLI map generator.
/// </summary>
public sealed record MapRoomGenerationOptions(
    string RoomName,
    string? ShardName,
    MapTerrainPreset TerrainPreset,
    int SourceCount,
    bool IncludeController,
    bool IncludeKeeperLairs,
    string? MineralType,
    bool OverwriteExisting,
    int? Seed);
