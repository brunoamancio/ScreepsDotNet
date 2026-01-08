namespace ScreepsDotNet.Storage.MongoRedis.Options;

public sealed class MongoRedisStorageOptions
{
    public const string SectionName = "Storage:MongoRedis";

    public string MongoConnectionString { get; init; } = "mongodb://localhost:27017";

    public string MongoDatabase { get; init; } = "screeps";

    public string UsersCollection { get; init; } = "users";

    public string UsersPowerCreepsCollection { get; init; } = "users.power_creeps";

    public string RoomsCollection { get; init; } = "rooms";

    public string RoomObjectsCollection { get; init; } = "rooms.objects";

    public string RoomTerrainCollection { get; init; } = "rooms.terrain";

    public string RoomsIntentsCollection { get; init; } = "rooms.intents";

    public string UsersIntentsCollection { get; init; } = "users.intents";

    public string UserCodeCollection { get; init; } = "users.code";

    public string UserMemoryCollection { get; init; } = "users.memory";

    public string UserConsoleCollection { get; init; } = "users.console";

    public string UserMoneyCollection { get; init; } = "users.money";

    public string ServerDataCollection { get; init; } = "server.data";

    public string MarketOrdersCollection { get; init; } = "market.orders";

    public string MarketStatsCollection { get; init; } = "market.stats";

    public string WorldInfoCollection { get; init; } = "world.info";

    public string VersionInfoCollection { get; init; } = "server.version";

    public string RoomsFlagsCollection { get; init; } = "rooms.flags";

    public string RedisConnectionString { get; init; } = "localhost:6379";
}
