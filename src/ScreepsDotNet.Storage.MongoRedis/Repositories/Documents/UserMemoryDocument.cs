namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class UserMemoryDocument
{
    [BsonId]
    [BsonElement("_id")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("memory")]
    public Dictionary<string, object?> Memory { get; set; } = new(StringComparer.Ordinal);

    [BsonElement("segments")]
    public Dictionary<string, string?> Segments { get; set; } = new(StringComparer.Ordinal);
}
