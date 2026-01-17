namespace ScreepsDotNet.Backend.Core.Models;

/// <summary>
/// Minimal data required by /api/game/room-overview.
/// </summary>
/// <param name="Room">Room identifier (name + shard).</param>
/// <param name="Owner">Owner metadata when the controller is claimed.</param>
public sealed record RoomOverview(RoomReference Room, RoomOverviewOwner? Owner);

/// <summary>
/// Player metadata exposed by /api/game/room-overview.
/// </summary>
/// <param name="Id">User id.</param>
/// <param name="Username">Player username.</param>
/// <param name="Badge">Badge dictionary (same shape as /api/game/map-stats users entry).</param>
public sealed record RoomOverviewOwner(string Id, string Username, IReadOnlyDictionary<string, object?>? Badge);
