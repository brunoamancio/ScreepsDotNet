namespace ScreepsDotNet.Engine.Data.Models;

using ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Engine-friendly view of a room for a specific tick. Wraps the driver snapshot while leaving room
/// for additional computed state (terrain grids, cost matrices, etc.) as the engine matures.
/// </summary>
public sealed record RoomState(
    string RoomName,
    int GameTime,
    RoomInfoSnapshot? Info,
    IReadOnlyDictionary<string, RoomObjectState> Objects,
    IReadOnlyDictionary<string, UserState> Users,
    RoomIntentSnapshot? Intents,
    IReadOnlyDictionary<string, RoomTerrainSnapshot> Terrain,
    IReadOnlyList<RoomFlagSnapshot> Flags,
    string RawRoomDocumentJson)
{
    public static RoomState FromSnapshot(RoomSnapshot snapshot)
        => new(
            snapshot.RoomName,
            snapshot.GameTime,
            snapshot.Info,
            snapshot.Objects,
            snapshot.Users,
            snapshot.Intents,
            snapshot.Terrain,
            snapshot.Flags,
            snapshot.RawRoomDocumentJson);
}
