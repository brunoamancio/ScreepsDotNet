using System.Text.Json.Nodes;

namespace ScreepsDotNet.Driver.Abstractions.History;

public sealed record RoomHistoryChunk(string Room, int BaseTick, DateTimeOffset Timestamp, IReadOnlyDictionary<int, JsonNode?> Ticks);
