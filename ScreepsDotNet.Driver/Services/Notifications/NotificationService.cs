using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using StackExchange.Redis;

namespace ScreepsDotNet.Driver.Services.Notifications;

internal sealed class NotificationService(IMongoDatabaseProvider databaseProvider, IRedisConnectionProvider redisProvider, ILogger<NotificationService>? logger = null) : INotificationService
{
    private const int DefaultErrorIntervalMinutes = 30;
    private const int MaxMessageLength = 500;
    private const string ErrorNotificationType = "error";

    private readonly IMongoCollection<UserNotificationDocument> _notifications = databaseProvider.GetCollection<UserNotificationDocument>(databaseProvider.Settings.UserNotificationsCollection);
    private readonly ISubscriber _subscriber = redisProvider.GetConnection().GetSubscriber();
    private readonly IDatabase _redis = redisProvider.GetConnection().GetDatabase();
    private readonly ILogger<NotificationService>? _logger = logger;

    public Task PublishConsoleMessagesAsync(string userId, ConsoleMessagesPayload payload, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(payload);

        if (IsNpcUser(userId))
        {
            if (payload.Log.Count > 0)
                _logger?.LogInformation("[Console:{User}] {Messages}", GetNpcName(userId), string.Join(", ", payload.Log));
            return Task.CompletedTask;
        }

        var json = JsonSerializer.Serialize(new { userId, messages = payload });
        return _subscriber.PublishAsync(RedisChannel.Literal($"user:{userId}/console"), json);
    }

    public async Task PublishConsoleErrorAsync(string userId, string errorMessage, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        var normalized = errorMessage.Trim();

        if (IsNpcUser(userId))
        {
            _logger?.LogWarning("[Console:{User}] {Error}", GetNpcName(userId), normalized);
            return;
        }

        await UpsertNotificationAsync(userId, normalized, new NotificationOptions(DefaultErrorIntervalMinutes, ErrorNotificationType), token).ConfigureAwait(false);
        await _subscriber.PublishAsync(RedisChannel.Literal($"user:{userId}/console"), JsonSerializer.Serialize(new { userId, error = normalized })).ConfigureAwait(false);
    }

    public Task SendNotificationAsync(string userId, string message, NotificationOptions options, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return UpsertNotificationAsync(userId, message, options, token);
    }

    public Task NotifyRoomsDoneAsync(int gameTime, CancellationToken token = default) =>
        _subscriber.PublishAsync(RedisChannel.Literal(RedisChannels.RoomsDone), gameTime);

    private async Task UpsertNotificationAsync(string userId, string message, NotificationOptions options, CancellationToken token)
    {
        var clampedMessage = message.Length > MaxMessageLength ? message[..MaxMessageLength] : message;
        var intervalMilliseconds = Math.Clamp(options.GroupIntervalMinutes, 0, 1440) * 60_000L;

        if (intervalMilliseconds > 0)
        {
            var bucket = (long)(Math.Ceiling(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / (double)intervalMilliseconds) * intervalMilliseconds);
            await SaveNotificationAsync(userId, clampedMessage, options.Type, bucket, token).ConfigureAwait(false);
        }
        else
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await SaveNotificationAsync(userId, clampedMessage, options.Type, timestamp, token).ConfigureAwait(false);
        }
    }

    private async Task SaveNotificationAsync(string userId, string message, string type, long timestamp, CancellationToken token)
    {
        var filter = Builders<UserNotificationDocument>.Filter.And(
            Builders<UserNotificationDocument>.Filter.Eq(document => document.UserId, userId),
            Builders<UserNotificationDocument>.Filter.Eq(document => document.Type, type),
            Builders<UserNotificationDocument>.Filter.Eq(document => document.Message, message),
            Builders<UserNotificationDocument>.Filter.Eq(document => document.Date, timestamp)
        );

        var update = Builders<UserNotificationDocument>.Update
            .Set(document => document.UserId, userId)
            .Set(document => document.Type, type)
            .Set(document => document.Message, message)
            .Set(document => document.Date, timestamp)
            .Inc(document => document.Count, 1);

        await _notifications.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, token).ConfigureAwait(false);
    }

    private static bool IsNpcUser(string userId) => userId is "2" or "3";

    private static string GetNpcName(string userId) => userId switch
    {
        "2" => "Invader",
        "3" => "Source Keeper",
        _ => "NPC"
    };
}
