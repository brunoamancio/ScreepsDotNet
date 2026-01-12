namespace ScreepsDotNet.Backend.Core.Models;

/// <summary>
/// Projection of the rooms collection used by world endpoints.
/// </summary>
public sealed record RoomStatusInfo(string RoomName, string? Status, bool? IsNoviceArea, bool? IsRespawnArea, long? OpenTime);
