using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Storage.MongoRedis.Extensions;
using ScreepsDotNet.Storage.MongoRedis.Providers;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoUserRepository : IUserRepository
{
    private const string IdField = "_id";
    private const string EmailField = "email";
    private const string EmailDirtyField = "emailDirty";
    private const string UsernameField = "username";
    private const string CpuField = "cpu";
    private const string ActiveField = "active";
    private const string BotField = "bot";
    private const string BadgeField = "badge";
    private const string PasswordField = "password";
    private const string LastRespawnField = "lastRespawnDate";
    private const string NotifyPrefsField = "notifyPrefs";
    private const string GclField = "gcl";
    private const string LastChargeField = "lastChargeTime";
    private const string BlockedField = "blocked";
    private const string CustomBadgeField = "customBadge";
    private const string PowerField = "power";
    private const string MoneyField = "money";
    private const string SteamField = "steam";
    private const string SteamIdField = "id";
    private const string SteamDisplayNameField = "displayName";
    private const string SteamOwnershipField = "ownership";
    private const string SteamProfileHiddenField = "steamProfileLinkHidden";
    private const string PowerExperimentationsField = "powerExperimentations";
    private const string PowerExperimentationTimeField = "powerExperimentationTime";
    private const string UsernameLowerField = "usernameLower";

    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoUserRepository(IMongoDatabaseProvider databaseProvider)
        => _collection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.UsersCollection);

    public async Task<UserProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq(IdField, userId);
        var document = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (document is null)
            return null;

        return new UserProfile(
            document.GetStringOrNull(IdField) ?? userId,
            document.GetStringOrNull(UsernameField),
            document.GetStringOrNull(EmailField),
            document.GetBooleanOrDefault(EmailDirtyField),
            HasPassword(document),
            document.GetDoubleOrDefault(CpuField),
            document.ToDotNet(BadgeField, BsonNull.Value),
            document.GetDateTimeOrNull(LastRespawnField),
            document.ToDotNet(NotifyPrefsField, BsonNull.Value),
            document.ToDotNet(GclField, BsonNull.Value),
            document.GetDateTimeOrNull(LastChargeField),
            document.GetBooleanOrDefault(BlockedField),
            document.ToDotNet(CustomBadgeField, BsonNull.Value),
            document.GetDoubleOrDefault(PowerField),
            document.GetDoubleOrDefault(MoneyField) / 1000d,
            BuildSteamProfile(document.GetValue(SteamField, BsonNull.Value)),
            document.GetDoubleOrDefault(PowerExperimentationsField),
            document.GetDoubleOrDefault(PowerExperimentationTimeField));
    }

    public async Task<int> GetActiveUsersCountAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Ne(ActiveField, 0),
                                                       Builders<BsonDocument>.Filter.Gt(CpuField, 0),
                                                       Builders<BsonDocument>.Filter.Or(Builders<BsonDocument>.Filter.Exists(BotField, false),
                                                                                        Builders<BsonDocument>.Filter.Eq(BotField, BsonNull.Value)));

        var count = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return (int)count;
    }

    private static bool HasPassword(BsonDocument document)
        => document.TryGetValue(PasswordField, out var value) && value.IsString && !string.IsNullOrWhiteSpace(value.AsString);

    private static object? ConvertToDotNet(BsonValue value)
        => value.IsBsonNull ? null : BsonTypeMapper.MapToDotNetValue(value);

    private static UserSteamProfile? BuildSteamProfile(BsonValue value)
    {
        if (value is not BsonDocument steamDoc)
            return null;

        return new UserSteamProfile(steamDoc.GetStringOrNull(SteamIdField), steamDoc.GetStringOrNull(SteamDisplayNameField),
                                    steamDoc.GetValue(SteamOwnershipField, BsonNull.Value).ToDotNet(),
                                    steamDoc.TryGetValue(SteamProfileHiddenField, out var hidden) && hidden.IsBoolean ? hidden.AsBoolean : null);
    }
    public async Task<UserPublicProfile?> FindPublicProfileAsync(string? username, string? userId, CancellationToken cancellationToken = default)
    {
        var filter = BuildPublicProfileFilter(username, userId);
        if (filter is null)
            return null;

        var document = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (document is null)
            return null;

        var steamId = ExtractPublicSteamId(document);

        return new UserPublicProfile(document.GetStringOrNull(IdField) ?? string.Empty, document.GetStringOrNull(UsernameField),
            document.ToDotNet(BadgeField, BsonNull.Value),
            document.ToDotNet(GclField, BsonNull.Value),
                                     document.GetDoubleOrDefault(PowerField),
                                     steamId);
    }

    private static FilterDefinition<BsonDocument>? BuildPublicProfileFilter(string? username, string? userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
            return Builders<BsonDocument>.Filter.Eq(IdField, userId);

        if (!string.IsNullOrWhiteSpace(username))
            return Builders<BsonDocument>.Filter.Eq(UsernameLowerField, username.ToLowerInvariant());

        return null;
    }

    private static string? ExtractPublicSteamId(BsonDocument document)
    {
        if (!document.TryGetValue(SteamField, out var steamValue) || !steamValue.IsBsonDocument)
            return null;

        var steamDoc = steamValue.AsBsonDocument;
        var hiddenValue = steamDoc.GetValue(SteamProfileHiddenField, BsonNull.Value);
        var hidden = (hiddenValue.IsBoolean && hiddenValue.AsBoolean) || (hiddenValue.IsNumeric && hiddenValue.ToInt32() != 0);

        if (hidden)
            return null;

        return steamDoc.TryGetValue(SteamIdField, out var idValue) && idValue.IsString ? idValue.AsString : null;
    }

    public Task UpdateNotifyPreferencesAsync(string userId, IDictionary<string, object?> notifyPreferences, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq(IdField, userId);
        var document = new BsonDocument();
        foreach (var kvp in notifyPreferences)
            document[kvp.Key] = kvp.Value is null ? BsonNull.Value : BsonValue.Create(kvp.Value);

        var update = Builders<BsonDocument>.Update.Set(NotifyPrefsField, document);
        return _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateBadgeAsync(string userId, UserBadgeUpdate badge, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq(IdField, userId);
        var badgeDocument = BuildBadgeDocument(badge);
        var update = Builders<BsonDocument>.Update.Set(BadgeField, badgeDocument);
        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.MatchedCount > 0;
    }

    public async Task<EmailUpdateResult> UpdateEmailAsync(string userId, string email, CancellationToken cancellationToken = default)
    {
        var duplicateFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq(EmailField, email),
            Builders<BsonDocument>.Filter.Ne(IdField, userId));

        var duplicateExists = await _collection.Find(duplicateFilter)
                                                .Limit(1)
                                                .AnyAsync(cancellationToken)
                                                .ConfigureAwait(false);

        if (duplicateExists)
            return EmailUpdateResult.AlreadyExists;

        var filter = Builders<BsonDocument>.Filter.Eq(IdField, userId);
        var update = Builders<BsonDocument>.Update
                                           .Set(EmailField, email)
                                           .Set(EmailDirtyField, false);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.MatchedCount == 0 ? EmailUpdateResult.UserNotFound : EmailUpdateResult.Success;
    }

    public Task SetSteamVisibilityAsync(string userId, bool visible, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq(IdField, userId);
        var hiddenValue = visible ? 0 : 1;
        var update = Builders<BsonDocument>.Update.Set($"{SteamField}.{SteamProfileHiddenField}", hiddenValue);
        return _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    private static BsonDocument BuildBadgeDocument(UserBadgeUpdate badge)
    {
        var document = new BsonDocument
        {
            [BadgeDocumentFields.Type] = BsonValue.Create(badge.Type),
            [BadgeDocumentFields.Color1] = badge.Color1,
            [BadgeDocumentFields.Color2] = badge.Color2,
            [BadgeDocumentFields.Color3] = badge.Color3,
            [BadgeDocumentFields.Param] = badge.Param,
            [BadgeDocumentFields.Flip] = badge.Flip
        };

        return document;
    }
}
