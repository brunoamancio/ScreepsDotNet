namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Models.UserMessages;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using StackExchange.Redis;

public sealed class MongoUserMessageService(
    IMongoDatabaseProvider databaseProvider,
    IRedisConnectionProvider redisConnectionProvider,
    ILogger<MongoUserMessageService> logger)
    : IUserMessageService
{
    private readonly IMongoCollection<UserMessageDocument> _messagesCollection = databaseProvider.GetCollection<UserMessageDocument>(databaseProvider.Settings.UserMessagesCollection);
    private readonly IMongoCollection<UserNotificationDocument> _notificationsCollection = databaseProvider.GetCollection<UserNotificationDocument>(databaseProvider.Settings.UserNotificationsCollection);
    private readonly IMongoCollection<UserDocument> _usersCollection = databaseProvider.GetCollection<UserDocument>(databaseProvider.Settings.UsersCollection);
    private readonly IConnectionMultiplexer _redisConnection = redisConnectionProvider.GetConnection();

    public async Task<UserMessageListResult> GetMessagesAsync(string userId, string respondentId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<UserMessageDocument>.Filter.And(
            Builders<UserMessageDocument>.Filter.Eq(message => message.UserId, userId),
            Builders<UserMessageDocument>.Filter.Eq(message => message.RespondentId, respondentId));

        var documents = await _messagesCollection.Find(filter)
                                                 .SortByDescending(message => message.Date)
                                                 .Limit(UserMessagingConstants.ThreadFetchLimit)
                                                 .ToListAsync(cancellationToken)
                                                 .ConfigureAwait(false);
        documents.Reverse();

        var messages = documents.Select(MapToModel).ToList();
        return new UserMessageListResult(messages);
    }

    public async Task<UserMessageIndexResult> GetMessageIndexAsync(string userId, CancellationToken cancellationToken = default)
    {
        var documents = await _messagesCollection.Find(message => message.UserId == userId)
                                                 .SortByDescending(message => message.Date)
                                                 .ToListAsync(cancellationToken)
                                                 .ConfigureAwait(false);

        var entries = new List<UserMessageIndexEntry>();
        var seenRespondents = new HashSet<string>(StringComparer.Ordinal);

        foreach (var document in documents) {
            if (!seenRespondents.Add(document.RespondentId))
                continue;

            entries.Add(new UserMessageIndexEntry(document.RespondentId, MapToModel(document)));
        }

        var users = await FetchUsersAsync(entries.Select(entry => entry.Id), cancellationToken).ConfigureAwait(false);
        return new UserMessageIndexResult(entries, users);
    }

    public async Task SendMessageAsync(string senderId, string respondentId, string text, CancellationToken cancellationToken = default)
    {
        var sender = await LoadUserAsync(senderId, cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException($"Sender {senderId} does not exist.");

        var respondent = await LoadUserAsync(respondentId, cancellationToken).ConfigureAwait(false)
                         ?? throw new ArgumentException("invalid respondent", nameof(respondentId));

        var now = DateTime.UtcNow;
        var outgoing = CreateMessageDocument(senderId, respondentId, now, UserMessagingConstants.MessageTypes.Outgoing, text, unread: true);
        var incoming = CreateMessageDocument(respondentId, senderId, now, UserMessagingConstants.MessageTypes.Incoming, text, unread: true);
        incoming.OutMessageId = outgoing.Id;

        await _messagesCollection.InsertManyAsync([outgoing, incoming], cancellationToken: cancellationToken).ConfigureAwait(false);

        var senderName = sender.Username ?? sender.Id ?? senderId;
        var notificationMessage = $"""<a href="https://screeps.com/a/#!/messages">New message</a> from user {senderName}""";
        await MaybeSendNotificationAsync(respondentId, notificationMessage, respondent.NotifyPrefs, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> MarkReadAsync(string userId, string messageId, CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(messageId, out var parsedId))
            return false;

        var filter = Builders<UserMessageDocument>.Filter.And(
            Builders<UserMessageDocument>.Filter.Eq(message => message.Id, parsedId),
            Builders<UserMessageDocument>.Filter.Eq(message => message.UserId, userId),
            Builders<UserMessageDocument>.Filter.Eq(message => message.Type, UserMessagingConstants.MessageTypes.Incoming));

        var message = await _messagesCollection.Find(filter)
                                               .FirstOrDefaultAsync(cancellationToken)
                                               .ConfigureAwait(false);
        if (message is null)
            return false;

        if (message.Unread) {
            var update = Builders<UserMessageDocument>.Update.Set(m => m.Unread, false);
            await _messagesCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        if (message.OutMessageId != ObjectId.Empty) {
            var counterpartFilter = Builders<UserMessageDocument>.Filter.Eq(m => m.Id, message.OutMessageId);
            var update = Builders<UserMessageDocument>.Update.Set(m => m.Unread, false);
            await _messagesCollection.UpdateOneAsync(counterpartFilter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<UserMessageDocument>.Filter.And(
            Builders<UserMessageDocument>.Filter.Eq(message => message.UserId, userId),
            Builders<UserMessageDocument>.Filter.Eq(message => message.Type, UserMessagingConstants.MessageTypes.Incoming),
            Builders<UserMessageDocument>.Filter.Eq(message => message.Unread, true));

        var count = await _messagesCollection.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return (int)count;
    }

    private static UserMessageDocument CreateMessageDocument(string userId, string respondentId, DateTime timestamp, string type, string text, bool unread)
        => new()
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            RespondentId = respondentId,
            Date = timestamp,
            Type = type,
            Text = text,
            Unread = unread
        };

    private static UserMessage MapToModel(UserMessageDocument document)
    {
        var outMessageId = document.OutMessageId == ObjectId.Empty ? null : document.OutMessageId.ToString();
        return new UserMessage(document.Id.ToString(),
                               document.UserId,
                               document.RespondentId,
                               document.Date,
                               document.Type,
                               document.Text,
                               document.Unread,
                               outMessageId);
    }

    private async Task<UserDocument?> LoadUserAsync(string userId, CancellationToken cancellationToken)
    {
        return await _usersCollection.Find(user => user.Id == userId)
                                     .FirstOrDefaultAsync(cancellationToken)
                                     .ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, UserMessageIndexUser>> FetchUsersAsync(IEnumerable<string> respondentIds, CancellationToken cancellationToken)
    {
        var ids = respondentIds.Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
            return new Dictionary<string, UserMessageIndexUser>(StringComparer.Ordinal);

        var filter = Builders<UserDocument>.Filter.In(user => user.Id, ids);
        var documents = await _usersCollection.Find(filter)
                                              .ToListAsync(cancellationToken)
                                              .ConfigureAwait(false);

        var result = new Dictionary<string, UserMessageIndexUser>(StringComparer.Ordinal);
        foreach (var document in documents) {
            if (document.Id is null)
                continue;

            result[document.Id] = new UserMessageIndexUser(document.Id, document.Username, document.Badge);
        }

        return result;
    }

    private async Task MaybeSendNotificationAsync(string userId,
                                                  string message,
                                                  IReadOnlyDictionary<string, object?>? notifyPrefs,
                                                  CancellationToken cancellationToken)
    {
        if (IsMessageNotificationsDisabled(notifyPrefs))
            return;

        if (!ShouldSendWhenOnline(notifyPrefs)) {
            var db = _redisConnection.GetDatabase();
            var key = $"{UserMessagingConstants.UserOnlineKeyPrefix}{userId}";
            var value = await db.StringGetAsync(key).ConfigureAwait(false);
            if (value.HasValue && long.TryParse(value.ToString(), out var lastSeen)) {
                var threshold = DateTimeOffset.UtcNow.AddMinutes(-UserMessagingConstants.NotificationOfflineWindowMinutes).ToUnixTimeMilliseconds();
                if (lastSeen > threshold)
                    return;
            }
        }

        var filter = Builders<UserNotificationDocument>.Filter.And(
            Builders<UserNotificationDocument>.Filter.Eq(notification => notification.UserId, userId),
            Builders<UserNotificationDocument>.Filter.Eq(notification => notification.Message, message),
            Builders<UserNotificationDocument>.Filter.Eq(notification => notification.Type, UserMessagingConstants.NotificationTypeMessage));

        var update = Builders<UserNotificationDocument>.Update
                                                       .Set(notification => notification.UserId, userId)
                                                       .Set(notification => notification.Message, message)
                                                       .Set(notification => notification.Date, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                                                       .Set(notification => notification.Type, UserMessagingConstants.NotificationTypeMessage)
                                                       .Inc(notification => notification.Count, 1);

        await _notificationsCollection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Message notification stored for user {UserId}.", userId);
    }

    private static bool IsMessageNotificationsDisabled(IReadOnlyDictionary<string, object?>? notifyPrefs)
    {
        if (notifyPrefs is null)
            return false;

        if (notifyPrefs.TryGetValue(NotifyPreferenceKeys.DisabledOnMessages, out var disabledValue)
            && disabledValue is bool disabled
            && disabled) {
            return true;
        }

        return false;
    }

    private static bool ShouldSendWhenOnline(IReadOnlyDictionary<string, object?>? notifyPrefs)
    {
        if (notifyPrefs is null)
            return false;

        if (!notifyPrefs.TryGetValue(NotifyPreferenceKeys.SendOnline, out var sendOnlineValue))
            return false;

        return sendOnlineValue is bool sendOnline && sendOnline;
    }
}
