namespace ScreepsDotNet.Backend.Core.Models;

using System.Collections.Generic;

/// <summary>
/// Input payload required to compute Screeps map statistics.
/// </summary>
/// <param name="Rooms">Rooms to inspect.</param>
/// <param name="StatName">Legacy stat name (e.g., owners1, power5).</param>
public sealed record MapStatsRequest(IReadOnlyCollection<RoomReference> Rooms, string StatName);

/// <summary>
/// Aggregated map statistics payload returned to HTTP layer.
/// </summary>
/// <param name="GameTime">Current server game time.</param>
/// <param name="Stats">Per-room status snapshot.</param>
/// <param name="StatsMax">Reserved legacy field (mirrors Node response).</param>
/// <param name="Users">User metadata referenced by stats entries.</param>
public sealed record MapStatsResult(int GameTime, IReadOnlyDictionary<string, MapStatsRoom> Stats, IReadOnlyDictionary<string, object?> StatsMax, IReadOnlyDictionary<string, MapStatsUser> Users);

/// <summary>
/// Status information for a single requested room.
/// </summary>
public sealed record MapStatsRoom(string RoomName, string? Status, bool? IsNoviceArea, bool? IsRespawnArea, long? OpenTime, RoomOwnershipInfo? Ownership,
                                  RoomSignInfo? Sign, bool IsSafeMode, RoomMineralInfo? PrimaryMineral);

/// <summary>
/// Ownership or reservation snapshot for a room.
/// </summary>
public sealed record RoomOwnershipInfo(string UserId, int Level);

/// <summary>
/// Player sign attached to a room controller.
/// </summary>
public sealed record RoomSignInfo(string UserId, string? Text, int? Time);

/// <summary>
/// Mineral data exposed via map stats (legacy uses "minerals0" key).
/// </summary>
public sealed record RoomMineralInfo(string Type, int? Density);

/// <summary>
/// Minimal user projection shared alongside map stats responses.
/// </summary>
public sealed record MapStatsUser(string Id, string Username, IReadOnlyDictionary<string, object?>? Badge);
