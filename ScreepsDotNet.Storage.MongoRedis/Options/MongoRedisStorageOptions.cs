namespace ScreepsDotNet.Storage.MongoRedis.Options;

public sealed class MongoRedisStorageOptions
{
    public const string SectionName = "Storage:MongoRedis";

    public string MongoConnectionString { get; init; } = "mongodb://localhost:27017";

    public string MongoDatabase { get; init; } = "screeps";

    public string ServerInfoCollection { get; init; } = "serverData";

    public string ServerInfoDocumentId { get; init; } = "serverInfo";

    public string UsersCollection { get; init; } = "users";

    public string RoomsCollection { get; init; } = "rooms";

    public string RedisConnectionString { get; init; } = "localhost:6379";
}
