namespace ScreepsDotNet.Driver.Contracts;

public sealed record RoomFlagSnapshot(
    string Id,
    string? UserId,
    string RoomName,
    string? Shard,
    string? Data);
