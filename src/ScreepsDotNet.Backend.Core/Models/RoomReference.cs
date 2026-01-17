namespace ScreepsDotNet.Backend.Core.Models;

/// <summary>
/// Identifies a Screeps room, optionally scoped to a shard.
/// </summary>
/// <param name="RoomName">Canonical room name (e.g., W10N5).</param>
/// <param name="ShardName">Optional shard identifier (e.g., shard3).</param>
public sealed record RoomReference(string RoomName, string? ShardName)
{
    public static RoomReference Create(string roomName, string? shardName = null)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            throw new ArgumentException("Room name is required.", nameof(roomName));

        var normalizedRoom = roomName.Trim();
        var normalizedShard = string.IsNullOrWhiteSpace(shardName) ? null : shardName.Trim();
        return new RoomReference(normalizedRoom, normalizedShard);
    }
}
