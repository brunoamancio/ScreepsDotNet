namespace ScreepsDotNet.Engine.Data.Models;

using ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Aggregated inter-room data (creep transfers, accessible rooms, market state) scoped to a tick.
/// </summary>
public sealed record GlobalState(
    int GameTime,
    IReadOnlyList<RoomObjectState> MovingCreeps,
    IReadOnlyDictionary<string, RoomInfoSnapshot> AccessibleRooms,
    IReadOnlyList<RoomObjectState> SpecialRoomObjects,
    GlobalMarketSnapshot Market)
{
    public static GlobalState FromSnapshot(GlobalSnapshot snapshot)
        => new(
            snapshot.GameTime,
            snapshot.MovingCreeps,
            snapshot.AccessibleRooms,
            snapshot.SpecialRoomObjects,
            snapshot.Market);
}
