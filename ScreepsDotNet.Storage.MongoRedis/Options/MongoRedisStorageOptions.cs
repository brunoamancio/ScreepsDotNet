namespace ScreepsDotNet.Storage.MongoRedis.Options;

public sealed class MongoRedisStorageOptions
{
    public const string SectionName = "Storage:MongoRedis";

    public string MongoConnectionString { get; init; } = "mongodb://localhost:27017";

    public string MongoDatabase { get; init; } = "screeps";

    public string UsersCollection { get; init; } = "users";

    public string RoomsCollection { get; init; } = "rooms";

    public string RoomObjectsCollection { get; init; } = "rooms.objects";

    public string UserCodeCollection { get; init; } = "users.code";

    public string UserMemoryCollection { get; init; } = "users.memory";

    public string UserConsoleCollection { get; init; } = "users.console";

    public string UserMoneyCollection { get; init; } = "users.money";

    public string ServerDataCollection { get; init; } = "server.data";

    public string RedisConnectionString { get; init; } = "localhost:6379";
}
