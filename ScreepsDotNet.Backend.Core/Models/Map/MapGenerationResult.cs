namespace ScreepsDotNet.Backend.Core.Models.Map;

/// <summary>
/// Summary returned after generating a room via the CLI.
/// </summary>
public sealed record MapGenerationResult(
    string RoomName,
    int TerrainTiles,
    int ObjectCount,
    bool ControllerCreated,
    int SourceCount,
    bool KeeperLairsCreated,
    string? MineralType);
