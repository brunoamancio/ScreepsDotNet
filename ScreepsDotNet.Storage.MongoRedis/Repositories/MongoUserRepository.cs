using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Models;
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
            GetString(document, IdField) ?? userId,
            GetString(document, UsernameField),
            GetString(document, EmailField),
            GetBoolean(document, EmailDirtyField),
            HasPassword(document),
            GetDouble(document, CpuField),
            ConvertToDotNet(document.GetValue(BadgeField, BsonNull.Value)),
            GetDateTime(document, LastRespawnField),
            ConvertToDotNet(document.GetValue(NotifyPrefsField, BsonNull.Value)),
            ConvertToDotNet(document.GetValue(GclField, BsonNull.Value)),
            GetDateTime(document, LastChargeField),
            GetBoolean(document, BlockedField),
            ConvertToDotNet(document.GetValue(CustomBadgeField, BsonNull.Value)),
            GetDouble(document, PowerField),
            GetDouble(document, MoneyField) / 1000d,
            BuildSteamProfile(document.GetValue(SteamField, BsonNull.Value)),
            GetDouble(document, PowerExperimentationsField),
            GetDouble(document, PowerExperimentationTimeField));
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

    private static string? GetString(BsonDocument document, string fieldName)
        => document.TryGetValue(fieldName, out var value) && value.IsString ? value.AsString : null;

    private static bool GetBoolean(BsonDocument document, string fieldName)
        => document.TryGetValue(fieldName, out var value) && value.IsBoolean && value.AsBoolean;

    private static double GetDouble(BsonDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value) || value.IsBsonNull)
            return 0;

        return value.IsNumeric ? value.ToDouble() : 0;
    }

    private static DateTime? GetDateTime(BsonDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value) || value.IsBsonNull)
            return null;

        if (value.BsonType == BsonType.DateTime)
            return value.ToUniversalTime();

        return null;
    }

    private static bool HasPassword(BsonDocument document)
        => document.TryGetValue(PasswordField, out var value) && value.IsString && !string.IsNullOrWhiteSpace(value.AsString);

    private static object? ConvertToDotNet(BsonValue value)
        => value.IsBsonNull ? null : BsonTypeMapper.MapToDotNetValue(value);

    private static UserSteamProfile? BuildSteamProfile(BsonValue value)
    {
        if (value is not BsonDocument steamDoc)
            return null;

        return new UserSteamProfile(GetString(steamDoc, SteamIdField), GetString(steamDoc, SteamDisplayNameField),
                                    ConvertToDotNet(steamDoc.GetValue(SteamOwnershipField, BsonNull.Value)),
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

        return new UserPublicProfile(GetString(document, IdField) ?? string.Empty, GetString(document, UsernameField),
                                     ConvertToDotNet(document.GetValue(BadgeField, BsonNull.Value)),
                                     ConvertToDotNet(document.GetValue(GclField, BsonNull.Value)),
                                     GetDouble(document, PowerField),
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
}
