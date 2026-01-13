namespace ScreepsDotNet.Driver.Contracts;

public sealed record RoomTerrainSnapshot(
    string Id,
    string RoomName,
    string? Shard,
    string? Type,
    string? Terrain);
