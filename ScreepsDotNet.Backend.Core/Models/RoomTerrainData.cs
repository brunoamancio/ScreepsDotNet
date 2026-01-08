namespace ScreepsDotNet.Backend.Core.Models;

/// <summary>
/// Raw entry from the rooms.terrain collection.
/// </summary>
public sealed record RoomTerrainData(string RoomName, string? Type, string? Terrain);

