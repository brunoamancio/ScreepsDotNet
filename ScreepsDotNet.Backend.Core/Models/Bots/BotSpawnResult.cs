namespace ScreepsDotNet.Backend.Core.Models.Bots;

/// <summary>
/// Summary returned after spawning a bot user into a room.
/// </summary>
public sealed record BotSpawnResult(
    string UserId,
    string Username,
    string RoomName,
    int SpawnX,
    int SpawnY);
