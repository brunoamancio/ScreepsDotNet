using MongoDB.Bson.Serialization.Attributes;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class RoomHistoryChunkDocument
{
    [BsonId]
    public string Id { get; set; } = default!;

    [BsonElement("room")]
    public string Room { get; set; } = default!;

    [BsonElement("baseTick")]
    public int BaseTick { get; set; }

    [BsonElement("timestamp")]
    public DateTime TimestampUtc { get; set; }

    [BsonElement("ticks")]
    public Dictionary<string, string?> Ticks { get; set; } = new(StringComparer.Ordinal);
}
