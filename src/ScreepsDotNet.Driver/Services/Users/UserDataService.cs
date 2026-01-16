using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Driver.Abstractions.Bulk;
using ScreepsDotNet.Driver.Abstractions.Users;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using StackExchange.Redis;

namespace ScreepsDotNet.Driver.Services.Users;

internal sealed class UserDataService(IMongoDatabaseProvider databaseProvider, IRedisConnectionProvider redisProvider, IBulkWriterFactory bulkWriterFactory) : IUserDataService
{
    private const int MaxMemoryBytes = 2 * 1024 * 1024;
    private const int MaxNotificationsPerBatch = 20;

    private readonly IMongoCollection<UserDocument> _users = databaseProvider.GetCollection<UserDocument>(databaseProvider.Settings.UsersCollection);
    private readonly IMongoCollection<UserNotificationDocument> _userNotifications = databaseProvider.GetCollection<UserNotificationDocument>(databaseProvider.Settings.UserNotificationsCollection);
    private readonly IMongoCollection<UserIntentDocument> _userIntents = databaseProvider.GetCollection<UserIntentDocument>(databaseProvider.Settings.UsersIntentsCollection);
    private readonly IMongoCollection<RoomIntentDocument> _roomIntents = databaseProvider.GetCollection<RoomIntentDocument>(databaseProvider.Settings.RoomsIntentsCollection);
    private readonly IDatabase _redis = redisProvider.GetConnection().GetDatabase();

    public async Task<IReadOnlyList<UserDocument>> GetActiveUsersAsync(CancellationToken token = default)
    {
        var filter = Builders<UserDocument>.Filter.And(
            Builders<UserDocument>.Filter.Ne(user => user.Active, 0),
            Builders<UserDocument>.Filter.Gt(user => user.Cpu, 0)
        );

        return await _users.Find(filter)
                           .Sort(Builders<UserDocument>.Sort.Descending(document => document.LastChargeTime))
                           .ToListAsync(token)
                           .ConfigureAwait(false);
    }

    public async Task<UserDocument?> GetUserAsync(string userId, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return await _users.Find(user => user.Id == userId)
                           .FirstOrDefaultAsync(token)
                           .ConfigureAwait(false);
    }

    public async Task SaveUserMemoryAsync(string userId, string memoryJson, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(memoryJson);

        if (Encoding.UTF8.GetByteCount(memoryJson) > MaxMemoryBytes)
            throw new InvalidOperationException("Script execution has been terminated: memory allocation limit reached.");

        await _redis.StringSetAsync($"{RedisKeys.Memory}{userId}", memoryJson).ConfigureAwait(false);
    }

    public async Task SaveUserMemorySegmentsAsync(string userId, IReadOnlyDictionary<int, string> segments, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(segments);

        if (segments.Count == 0)
            return;

        var entries = segments.Select(pair => new HashEntry(pair.Key.ToString(), pair.Value ?? string.Empty)).ToArray();
        await _redis.HashSetAsync($"{RedisKeys.MemorySegments}{userId}", entries).ConfigureAwait(false);
    }

    public Task SaveUserInterShardSegmentAsync(string userId, string segmentData, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(segmentData);
        return _redis.StringSetAsync($"{RedisKeys.PublicMemorySegments}{userId}", segmentData);
    }

    public async Task SaveUserIntentsAsync(string userId, UserIntentWritePayload payload, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(payload);

        var operations = new List<Task>();

        if (payload.Rooms is { Count: > 0 })
            operations.Add(SaveRoomIntentsAsync(userId, payload.Rooms, token));

        if (payload.Notifications is { Count: > 0 })
            operations.Add(SaveNotificationsAsync(userId, payload.Notifications, token));

        if (payload.Global is { Count: > 0 })
            operations.Add(SaveGlobalIntentsAsync(userId, payload.Global, token));

        if (operations.Count == 0)
            return;

        await Task.WhenAll(operations).ConfigureAwait(false);
    }

    public Task ClearGlobalIntentsAsync(CancellationToken token = default)
        => _userIntents.DeleteManyAsync(FilterDefinition<UserIntentDocument>.Empty, token);

    public async Task AddRoomToUserAsync(string userId, string roomName, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        var writer = bulkWriterFactory.CreateUsersWriter();
        writer.AddToSet(userId, UserDocumentFields.Rooms, roomName);
        await writer.ExecuteAsync(token).ConfigureAwait(false);
    }

    public async Task RemoveRoomFromUserAsync(string userId, string roomName, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        var writer = bulkWriterFactory.CreateUsersWriter();
        writer.Pull(userId, UserDocumentFields.Rooms, roomName);
        await writer.ExecuteAsync(token).ConfigureAwait(false);
    }

    private async Task SaveRoomIntentsAsync(string userId, IReadOnlyDictionary<string, object?> rooms, CancellationToken token)
    {
        var roomNamesToActivate = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (roomName, intentPayload) in rooms)
        {
            if (string.IsNullOrWhiteSpace(roomName))
                continue;

            var bsonPayload = NormalizeIntentPayload(intentPayload);
            var updatePath = $"{RoomIntentDocumentFields.UsersRoot}.{userId}.{RoomIntentDocumentFields.ObjectsManual}";
            var update = Builders<RoomIntentDocument>.Update
                                                     .Set(updatePath, bsonPayload);

            await _roomIntents.UpdateOneAsync(document => document.Room == roomName,
                                              update,
                                              new UpdateOptions { IsUpsert = true },
                                              token)
                              .ConfigureAwait(false);
            roomNamesToActivate.Add(roomName);
        }

        if (roomNamesToActivate.Count > 0)
            await _redis.SetAddAsync(RedisKeys.ActiveRooms, roomNamesToActivate.Select(name => (RedisValue)name).ToArray()).ConfigureAwait(false);
    }

    private async Task SaveNotificationsAsync(string userId, IReadOnlyList<NotifyIntentPayload> notifications, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var notification in notifications.Take(MaxNotificationsPerBatch))
        {
            var normalizedInterval = Math.Clamp(notification.GroupIntervalMinutes, 0, 1440);
            var intervalMilliseconds = (long)TimeSpan.FromMinutes(normalizedInterval).TotalMilliseconds;
            var bucketTime = intervalMilliseconds > 0
                ? (long)(Math.Ceiling(now / (double)intervalMilliseconds) * intervalMilliseconds)
                : now;

            var message = (notification.Message ?? string.Empty).Trim();
            if (message.Length > 500)
                message = message[..500];

            var filter = Builders<UserNotificationDocument>.Filter.And(
                Builders<UserNotificationDocument>.Filter.Eq(document => document.UserId, userId),
                Builders<UserNotificationDocument>.Filter.Eq(document => document.Message, message),
                Builders<UserNotificationDocument>.Filter.Eq(document => document.Type, UserMessagingConstants.NotificationTypeMessage),
                Builders<UserNotificationDocument>.Filter.Eq(document => document.Date, bucketTime)
            );

            var update = Builders<UserNotificationDocument>.Update
                .Set(document => document.UserId, userId)
                .Set(document => document.Message, message)
                .Set(document => document.Type, UserMessagingConstants.NotificationTypeMessage)
                .Set(document => document.Date, bucketTime)
                .Inc(document => document.Count, 1);

            await _userNotifications.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, token).ConfigureAwait(false);
        }
    }

    private Task SaveGlobalIntentsAsync(string userId, IReadOnlyDictionary<string, object?> intents, CancellationToken token)
    {
        var document = new UserIntentDocument
        {
            UserId = userId,
            Intents = NormalizeIntentPayload(intents)
        };

        return _userIntents.InsertOneAsync(document, cancellationToken: token);
    }

    private static BsonDocument NormalizeIntentPayload(object? payload)
    {
        return payload switch
        {
            null => [],
            BsonDocument document => document.DeepClone().AsBsonDocument,
            IReadOnlyDictionary<string, object?> dictionary => new BsonDocument(dictionary.Select(pair => new BsonElement(pair.Key, BsonValue.Create(pair.Value)))),
            IDictionary<string, object?> dictionary => new BsonDocument(dictionary.Cast<KeyValuePair<string, object?>>().Select(pair => new BsonElement(pair.Key, BsonValue.Create(pair.Value)))),
            _ => BsonDocument.Create(payload)
        };
    }
}
