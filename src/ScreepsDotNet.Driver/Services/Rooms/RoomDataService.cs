using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Driver.Abstractions.Bulk;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Common;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using StackExchange.Redis;

namespace ScreepsDotNet.Driver.Services.Rooms;

internal sealed class RoomDataService(
    IMongoDatabaseProvider databaseProvider,
    IRedisConnectionProvider redisProvider,
    IBulkWriterFactory bulkWriterFactory) : IRoomDataService
{
    private static readonly string[] MovingCreepTypes = [RoomObjectTypes.Creep, RoomObjectTypes.PowerCreep];
    private static readonly string[] SpecialObjectTypes = [RoomObjectTypes.Terminal, RoomObjectTypes.PowerSpawn, RoomObjectTypes.PowerCreep];
    private readonly IMongoCollection<RoomObjectDocument> _roomObjects = databaseProvider.GetCollection<RoomObjectDocument>(databaseProvider.Settings.RoomObjectsCollection);
    private readonly IMongoCollection<UserDocument> _users = databaseProvider.GetCollection<UserDocument>(databaseProvider.Settings.UsersCollection);
    private readonly IMongoCollection<RoomFlagDocument> _roomFlags = databaseProvider.GetCollection<RoomFlagDocument>(databaseProvider.Settings.RoomsFlagsCollection);
    private readonly IMongoCollection<RoomTerrainDocument> _roomTerrain = databaseProvider.GetCollection<RoomTerrainDocument>(databaseProvider.Settings.RoomTerrainCollection);
    private readonly IMongoCollection<RoomDocument> _rooms = databaseProvider.GetCollection<RoomDocument>(databaseProvider.Settings.RoomsCollection);
    private readonly IMongoCollection<BsonDocument> _roomsRaw = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.RoomsCollection);
    private readonly IMongoCollection<RoomIntentDocument> _roomIntents = databaseProvider.GetCollection<RoomIntentDocument>(databaseProvider.Settings.RoomsIntentsCollection);
    private readonly IMongoCollection<MarketOrderDocument> _marketOrders = databaseProvider.GetCollection<MarketOrderDocument>(databaseProvider.Settings.MarketOrdersCollection);
    private readonly IMongoCollection<PowerCreepDocument> _powerCreeps = databaseProvider.GetCollection<PowerCreepDocument>(databaseProvider.Settings.UsersPowerCreepsCollection);
    private readonly IMongoCollection<UserIntentDocument> _userIntents = databaseProvider.GetCollection<UserIntentDocument>(databaseProvider.Settings.UsersIntentsCollection);
    private readonly IDatabase _redis = redisProvider.GetConnection().GetDatabase();

    public async Task<IReadOnlyList<string>> DrainActiveRoomsAsync(CancellationToken token = default)
    {
        var members = await _redis.SetMembersAsync(RedisKeys.ActiveRooms).ConfigureAwait(false);
        if (members.Length == 0) return [];

        await _redis.KeyDeleteAsync(RedisKeys.ActiveRooms).ConfigureAwait(false);
        return members.Where(value => value.HasValue)
                      .Select(value => value.ToString())
                      .ToArray();
    }

    public async Task ActivateRoomsAsync(IEnumerable<string> roomNames, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(roomNames);
        var values = roomNames.Where(name => !string.IsNullOrWhiteSpace(name))
                              .Select(name => (RedisValue)name)
                              .ToArray();
        if (values.Length == 0) return;

        await _redis.SetAddAsync(RedisKeys.ActiveRooms, values).ConfigureAwait(false);
    }

    public async Task<RoomObjectsPayload> GetRoomObjectsAsync(string roomName, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        var objects = await _roomObjects.Find(document => document.Room == roomName)
                                        .ToListAsync(token)
                                        .ConfigureAwait(false);

        var userIds = objects.Select(document => document.UserId)
                             .Where(id => !string.IsNullOrWhiteSpace(id))
                             .Distinct(StringComparer.Ordinal)
                             .ToArray();

        var users = userIds.Length == 0
            ? []
            : await _users.Find(user => ((IEnumerable<string?>)userIds).Contains(user.Id))
                          .ToListAsync(token)
                          .ConfigureAwait(false);

        return new RoomObjectsPayload(MapById(objects, document => document.Id.ToString()),
                                      MapById(users, document => document.Id ?? string.Empty));
    }

    public async Task<IReadOnlyList<RoomFlagDocument>> GetRoomFlagsAsync(string roomName, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        return await _roomFlags.Find(document => document.Room == roomName)
                               .ToListAsync(token)
                               .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, RoomTerrainDocument>> GetRoomTerrainAsync(string roomName, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        var documents = await _roomTerrain.Find(document => document.Room == roomName)
                                          .ToListAsync(token)
                                          .ConfigureAwait(false);
        return MapById(documents, document => document.Id.ToString());
    }

    public async Task<RoomDocument?> GetRoomInfoAsync(string roomName, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        return await _rooms.Find(room => room.Id == roomName)
                           .FirstOrDefaultAsync(token)
                           .ConfigureAwait(false);
    }

    public async Task SaveRoomInfoAsync(RoomDocument room, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (string.IsNullOrWhiteSpace(room.Id))
            throw new ArgumentException("Room must have an identifier.", nameof(room));

        var writer = bulkWriterFactory.CreateRoomsWriter();
        writer.Update(room.Id, room);
        await writer.ExecuteAsync(token).ConfigureAwait(false);
    }

    public async Task SetRoomStatusAsync(string roomName, string status, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        var writer = bulkWriterFactory.CreateRoomsWriter();
        writer.Update(roomName, new { Status = status });
        await writer.ExecuteAsync(token).ConfigureAwait(false);
    }

    public async Task<RoomIntentDocument?> GetRoomIntentsAsync(string roomName, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        return await _roomIntents.Find(document => document.Room == roomName)
                                 .FirstOrDefaultAsync(token)
                                 .ConfigureAwait(false);
    }

    public Task ClearRoomIntentsAsync(string roomName, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        return _roomIntents.DeleteOneAsync(document => document.Room == roomName, token);
    }

    public Task SaveRoomEventLogAsync(string roomName, string eventLogJson, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventLogJson);
        return _redis.HashSetAsync(RedisKeys.RoomEventLog, roomName, eventLogJson);
    }

    public Task SaveMapViewAsync(string roomName, string mapViewJson, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapViewJson);
        return _redis.StringSetAsync($"{RedisKeys.MapView}{roomName}", mapViewJson);
    }

    public async Task UpdateAccessibleRoomsListAsync(CancellationToken token = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var rooms = await _rooms.Find(room => room.Status == "normal")
                                .ToListAsync(token)
                                .ConfigureAwait(false);
        var accessible = rooms.Where(room => !room.OpenTime.HasValue || room.OpenTime <= now)
                              .Select(room => room.Id)
                              .Where(id => !string.IsNullOrWhiteSpace(id))
                              .ToArray();

        var payload = JsonSerializer.Serialize(accessible);
        await _redis.StringSetAsync(RedisKeys.AccessibleRooms, payload).ConfigureAwait(false);
    }

    public async Task UpdateRoomStatusDataAsync(CancellationToken token = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Gt("novice", now),
            Builders<BsonDocument>.Filter.Gt("respawnArea", now),
            Builders<BsonDocument>.Filter.Gt("openTime", now),
            Builders<BsonDocument>.Filter.Eq("status", "out of borders")
        );

        var rooms = await _roomsRaw.Find(filter)
                                   .ToListAsync(token)
                                   .ConfigureAwait(false);

        var statusData = new RoomStatusData();
        foreach (var room in rooms)
        {
            var id = room.GetValue("_id", BsonNull.Value).ToString();
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (TryGetLong(room, "novice", out var novice) && novice > now)
                statusData.Novice[id] = novice;
            else if (TryGetLong(room, "respawnArea", out var respawn) && respawn > now)
                statusData.Respawn[id] = respawn;
            else if (TryGetLong(room, "openTime", out var openTime) && openTime > now)
                statusData.Closed[id] = openTime;
            else
                statusData.Closed[id] = long.MaxValue;
        }

        var payload = JsonSerializer.Serialize(statusData);
        await _redis.StringSetAsync(RedisKeys.RoomStatusData, payload).ConfigureAwait(false);
    }

    public async Task<InterRoomSnapshot> GetInterRoomSnapshotAsync(int gameTime, CancellationToken token = default)
    {
        var movingCreepsTask = _roomObjects.Find(Builders<RoomObjectDocument>.Filter.And(
                                                Builders<RoomObjectDocument>.Filter.In(document => document.Type, MovingCreepTypes),
                                                new BsonDocument("interRoom", new BsonDocument("$ne", BsonNull.Value))))
                                           .ToListAsync(token);

        var accessibleRoomsTask = _rooms.Find(room => room.Status == "normal")
                                        .ToListAsync(token);

        var specialObjectsTask = _roomObjects.Find(Builders<RoomObjectDocument>.Filter.In(document => document.Type, SpecialObjectTypes))
                                             .ToListAsync(token);

        var ordersTask = _marketOrders.Find(FilterDefinition<MarketOrderDocument>.Empty).ToListAsync(token);
        var powerCreepsTask = _powerCreeps.Find(FilterDefinition<PowerCreepDocument>.Empty).ToListAsync(token);
        var userIntentsTask = _userIntents.Find(FilterDefinition<UserIntentDocument>.Empty).ToListAsync(token);

        await Task.WhenAll(movingCreepsTask, accessibleRoomsTask, specialObjectsTask, ordersTask, powerCreepsTask, userIntentsTask).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var accessibleRooms = accessibleRoomsTask.Result
            .Where(room => !room.OpenTime.HasValue || room.OpenTime <= now)
            .Where(room => !string.IsNullOrWhiteSpace(room.Id))
            .ToDictionary(room => room.Id!, room => room, StringComparer.Ordinal);

        var marketOrders = ordersTask.Result;
        var powerCreeps = powerCreepsTask.Result;
        var userIntents = userIntentsTask.Result;

        var userIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var order in marketOrders) {
            if (!string.IsNullOrWhiteSpace(order.UserId))
                userIds.Add(order.UserId);
        }

        foreach (var creep in powerCreeps) {
            if (!string.IsNullOrWhiteSpace(creep.UserId))
                userIds.Add(creep.UserId);
        }

        foreach (var intent in userIntents) {
            if (!string.IsNullOrWhiteSpace(intent.UserId))
                userIds.Add(intent.UserId);
        }

        var users = userIds.Count == 0 ? [] : await _users.Find(Builders<UserDocument>.Filter.In(document => document.Id, userIds))
                                                          .ToListAsync(token)
                                                          .ConfigureAwait(false);

        var marketSnapshot = new InterRoomMarketSnapshot(marketOrders, users, powerCreeps, userIntents, string.Empty);
        return new InterRoomSnapshot(gameTime, movingCreepsTask.Result, accessibleRooms, specialObjectsTask.Result, marketSnapshot);
    }

    private static IReadOnlyDictionary<string, TDocument> MapById<TDocument>(IEnumerable<TDocument> documents, Func<TDocument, string> selector)
    {
        var dictionary = new Dictionary<string, TDocument>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            var key = selector(document);
            if (!string.IsNullOrWhiteSpace(key))
                dictionary[key] = document;
        }

        return dictionary;
    }

    private static bool TryGetLong(BsonDocument document, string field, out long value)
    {
        value = default;
        if (!document.TryGetValue(field, out var bsonValue))
            return false;

        if (bsonValue.IsInt64)
        {
            value = bsonValue.AsInt64;
            return true;
        }

        if (bsonValue.IsInt32)
        {
            value = bsonValue.AsInt32;
            return true;
        }

        if (bsonValue.IsDouble)
        {
            value = (long)bsonValue.AsDouble;
            return true;
        }

        return false;
    }

    private sealed class RoomStatusData
    {
        public Dictionary<string, long> Novice { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, long> Respawn { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, long> Closed { get; } = new(StringComparer.Ordinal);
    }
}
