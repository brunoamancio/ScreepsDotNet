namespace ScreepsDotNet.Engine.Data.Models;

using ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Aggregated inter-room data (creep transfers, accessible rooms, market state) scoped to a tick.
/// </summary>
public sealed record GlobalState(
    int GameTime,
    IReadOnlyList<RoomObjectSnapshot> MovingCreeps,
    IReadOnlyDictionary<string, RoomInfoSnapshot> AccessibleRooms,
    IReadOnlyDictionary<string, RoomExitTopology> ExitTopology,
    IReadOnlyList<RoomObjectSnapshot> SpecialRoomObjects,
    GlobalMarketSnapshot Market,
    IReadOnlyDictionary<string, RoomIntentSnapshot>? RoomIntents = null)
{
    public IReadOnlyDictionary<string, RoomIntentSnapshot> RoomIntents { get; init; } = RoomIntents ?? new Dictionary<string, RoomIntentSnapshot>(StringComparer.Ordinal);

    public static GlobalState FromSnapshot(GlobalSnapshot snapshot)
        => new(
            snapshot.GameTime,
            snapshot.MovingCreeps,
            snapshot.AccessibleRooms,
            snapshot.ExitTopology,
            snapshot.SpecialRoomObjects,
            snapshot.Market,
            snapshot.RoomIntents);
}
