namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class WorldInfoDocument
{
    public const string DefaultId = "world";

    [BsonId]
    [BsonElement("_id")]
    public string Id { get; set; } = DefaultId;

    [BsonElement("gameTime")]
    public int GameTime { get; set; }

    [BsonElement("tickDuration")]
    public int TickDuration { get; set; }
}
