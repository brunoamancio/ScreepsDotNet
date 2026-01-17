namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class UserConsoleEntryDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("user")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("expression")]
    public string Expression { get; set; } = string.Empty;

    [BsonElement("hidden")]
    public bool Hidden { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
