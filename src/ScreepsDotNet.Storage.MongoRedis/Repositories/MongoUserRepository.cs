using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoUserRepository(IMongoDatabaseProvider databaseProvider) : IUserRepository
{
    private readonly IMongoCollection<UserDocument> _collection = databaseProvider.GetCollection<UserDocument>(databaseProvider.Settings.UsersCollection);

    public async Task<UserProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var document = await _collection.Find(user => user.Id == userId)
                                         .FirstOrDefaultAsync(cancellationToken)
                                         .ConfigureAwait(false);

        var result = document is null ? null : MapToUserProfile(document, userId);
        return result;
    }

    public async Task<int> GetActiveUsersCountAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<UserDocument>.Filter.Ne(user => user.Active, 0)
                     & Builders<UserDocument>.Filter.Gt(user => user.Cpu, 0)
                     & Builders<UserDocument>.Filter.Eq(user => user.Bot, null);

        var count = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return (int)count;
    }

    public async Task<UserPublicProfile?> FindPublicProfileAsync(string? username, string? userId, CancellationToken cancellationToken = default)
    {
        var filter = BuildPublicProfileFilter(username, userId);
        if (filter is null)
            return null;

        var document = await _collection.Find(filter)
                                         .FirstOrDefaultAsync(cancellationToken)
                                         .ConfigureAwait(false);
        if (document is null)
            return null;

        return new UserPublicProfile(document.Id ?? string.Empty,
                                     document.Username,
                                     document.Badge,
                                     document.Gcl,
                                     document.Power ?? 0,
                                     ExtractPublicSteamId(document));
    }

    public Task UpdateNotifyPreferencesAsync(string userId, IDictionary<string, object?> notifyPreferences, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(notifyPreferences, StringComparer.Ordinal);
        var update = Builders<UserDocument>.Update.Set(user => user.NotifyPrefs, payload);
        return _collection.UpdateOneAsync(user => user.Id == userId, update, cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateBadgeAsync(string userId, UserBadgeUpdate badge, CancellationToken cancellationToken = default)
    {
        var update = Builders<UserDocument>.Update.Set(user => user.Badge, CreateBadgePayload(badge));
        var result = await _collection.UpdateOneAsync(user => user.Id == userId, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.MatchedCount > 0;
    }

    public async Task<EmailUpdateResult> UpdateEmailAsync(string userId, string email, CancellationToken cancellationToken = default)
    {
        var duplicateFilter = Builders<UserDocument>.Filter.And(
            Builders<UserDocument>.Filter.Eq(user => user.Email, email),
            Builders<UserDocument>.Filter.Ne(user => user.Id, userId));

        var duplicateExists = await _collection.Find(duplicateFilter)
                                               .Limit(1)
                                               .AnyAsync(cancellationToken)
                                               .ConfigureAwait(false);
        if (duplicateExists)
            return EmailUpdateResult.AlreadyExists;

        var update = Builders<UserDocument>.Update
                                           .Set(user => user.Email, email)
                                           .Set(user => user.EmailDirty, false);

        var result = await _collection.UpdateOneAsync(user => user.Id == userId, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        var updateResult = result.MatchedCount == 0 ? EmailUpdateResult.UserNotFound : EmailUpdateResult.Success;
        return updateResult;
    }

    public async Task SetSteamVisibilityAsync(string userId, bool visible, CancellationToken cancellationToken = default)
    {
        var user = await _collection.Find(u => u.Id == userId)
                                    .Project(u => new { u.Steam })
                                    .FirstOrDefaultAsync(cancellationToken)
                                    .ConfigureAwait(false);

        var update = user?.Steam is null
            ? Builders<UserDocument>.Update.Set(u => u.Steam, new UserSteamDocument
            {
                SteamProfileLinkHidden = !visible
            })
            : Builders<UserDocument>.Update.Set(u => u.Steam!.SteamProfileLinkHidden, !visible);
        await _collection.UpdateOneAsync(u => u.Id == userId, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.ToLowerInvariant();
        return _collection.Find(user => user.Email == normalized)
                          .AnyAsync(cancellationToken);
    }

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.ToLowerInvariant();
        return _collection.Find(user => user.UsernameLower == normalized)
                          .AnyAsync(cancellationToken);
    }

    public async Task<SetUsernameResult> SetUsernameAsync(string userId, string username, string? email, CancellationToken cancellationToken = default)
    {
        var user = await _collection.Find(u => u.Id == userId)
                                    .Project(u => new { u.Id, u.Username })
                                    .FirstOrDefaultAsync(cancellationToken)
                                    .ConfigureAwait(false);

        if (user is null)
            return SetUsernameResult.UserNotFound;

        if (!string.IsNullOrWhiteSpace(user.Username))
            return SetUsernameResult.UsernameAlreadySet;

        var usernameLower = username.ToLowerInvariant();
        var duplicate = await _collection.Find(u => u.UsernameLower == usernameLower)
                                         .AnyAsync(cancellationToken)
                                         .ConfigureAwait(false);
        if (duplicate)
            return SetUsernameResult.UsernameExists;

        var update = Builders<UserDocument>.Update
                                           .Set(u => u.Username, username)
                                           .Set(u => u.UsernameLower, usernameLower);

        if (!string.IsNullOrWhiteSpace(email)) {
            var normalizedEmail = email.ToLowerInvariant();
            update = update.Set(u => u.Email, normalizedEmail)
                           .Set(u => u.EmailDirty, false);
        }

        var result = await _collection.UpdateOneAsync(user => user.Id == userId, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        var usernameResult = result.ModifiedCount == 0 ? SetUsernameResult.Failed : SetUsernameResult.Success;
        return usernameResult;
    }

    private static UserProfile MapToUserProfile(UserDocument document, string fallbackId)
        => new(
            document.Id ?? fallbackId,
            document.Username,
            document.Email,
            document.EmailDirty ?? false,
            HasPassword(document),
            document.Cpu ?? 0,
            document.Badge,
            document.LastRespawnDate,
            document.NotifyPrefs ?? new Dictionary<string, object?>(StringComparer.Ordinal),
            document.Gcl,
            document.LastChargeTime,
            document.Blocked ?? false,
            document.CustomBadge,
            document.Power ?? 0,
            (document.Money ?? 0d) / 1000d,
            BuildSteamProfile(document.Steam),
            document.PowerExperimentations ?? 0,
            document.PowerExperimentationTime ?? 0);

    private static bool HasPassword(UserDocument document)
        => !string.IsNullOrWhiteSpace(document.Password);

    private static UserSteamProfile? BuildSteamProfile(UserSteamDocument? steam)
    {
        var result = steam is null ? null : new UserSteamProfile(steam.Id, steam.DisplayName, steam.Ownership, steam.SteamProfileLinkHidden);
        return result;
    }

    private static FilterDefinition<UserDocument>? BuildPublicProfileFilter(string? username, string? userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
            return Builders<UserDocument>.Filter.Eq(user => user.Id, userId);

        if (!string.IsNullOrWhiteSpace(username)) {
            var normalized = username.ToLowerInvariant();
            var regex = new BsonRegularExpression($"^{Regex.Escape(username)}$", "i");
            return Builders<UserDocument>.Filter.Or(
                Builders<UserDocument>.Filter.Eq(user => user.UsernameLower, normalized),
                Builders<UserDocument>.Filter.Regex(user => user.Username!, regex));
        }

        return null;
    }

    private static string? ExtractPublicSteamId(UserDocument document)
    {
        var steam = document.Steam;
        if (steam is null)
            return null;

        var hidden = steam.SteamProfileLinkHidden ?? false;
        var steamId = hidden ? null : steam.Id;
        return steamId;
    }

    private static Dictionary<string, object?> CreateBadgePayload(UserBadgeUpdate badge)
        => new(StringComparer.Ordinal)
        {
            [BadgeDocumentFields.Type] = badge.Type,
            [BadgeDocumentFields.Color1] = badge.Color1,
            [BadgeDocumentFields.Color2] = badge.Color2,
            [BadgeDocumentFields.Color3] = badge.Color3,
            [BadgeDocumentFields.Param] = badge.Param,
            [BadgeDocumentFields.Flip] = badge.Flip
        };
}
