namespace ScreepsDotNet.Driver.Contracts;

using System.Collections.Generic;

/// <summary>
/// Full snapshot of a room tick, including world objects, users, intents, terrain, flags, and power creeps.
/// </summary>
public sealed record RoomSnapshot(
    string RoomName,
    int GameTime,
    RoomInfoSnapshot? Info,
    IReadOnlyDictionary<string, RoomObjectSnapshot> Objects,
    IReadOnlyDictionary<string, UserState> Users,
    RoomIntentSnapshot? Intents,
    IReadOnlyDictionary<string, RoomTerrainSnapshot> Terrain,
    IReadOnlyList<RoomFlagSnapshot> Flags);
