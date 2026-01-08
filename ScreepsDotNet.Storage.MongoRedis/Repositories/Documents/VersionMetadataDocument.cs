namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class VersionMetadataDocument
{
    public const string DefaultId = "version-info";

    [BsonId]
    public string Id { get; set; } = DefaultId;

    [BsonElement("protocol")]
    public int Protocol { get; set; }

    [BsonElement("useNativeAuth")]
    public bool UseNativeAuth { get; set; }

    [BsonElement("packageVersion")]
    public string? PackageVersion { get; set; }
}
