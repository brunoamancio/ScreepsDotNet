using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using StackExchange.Redis;

namespace ScreepsDotNet.Storage.MongoRedis.Services;

public sealed class RedisTokenService(IRedisConnectionProvider connectionProvider, IOptions<AuthOptions> options, ILogger<RedisTokenService> logger)
    : ITokenService
{
    private const string TokenKeyPrefix = "auth_";
    private const string TokenKeyPattern = TokenKeyPrefix + "*";
    private const int TokenBytesLength = 20;
    private const string MissingUserIdMessage = "User identifier must be provided.";
    private const string ResolveTokenErrorMessage = "Unable to resolve auth token.";
    private const string EnumerateTokensErrorMessage = "Failed to enumerate auth tokens on endpoint {Endpoint}.";

    private readonly IConnectionMultiplexer _connection = connectionProvider.GetConnection();
    private readonly TimeSpan _tokenTtl = TimeSpan.FromSeconds(Math.Max(1, options.Value.TokenTtlSeconds));

    public async Task<string> IssueTokenAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException(MissingUserIdMessage, nameof(userId));

        var token = GenerateToken(userId);
        var db = _connection.GetDatabase();
        await db.StringSetAsync(TokenKeyPrefix + token, userId, _tokenTtl).ConfigureAwait(false);
        return token;
    }

    public async Task<string?> ResolveUserIdAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var db = _connection.GetDatabase();
        try {
            var redisValue = await db.StringGetAsync(TokenKeyPrefix + token).ConfigureAwait(false);
            if (redisValue.IsNullOrEmpty)
                return null;

            await db.KeyExpireAsync(TokenKeyPrefix + token, _tokenTtl).ConfigureAwait(false);
            return redisValue;
        }
        catch (RedisException ex) {
            logger.LogError(ex, ResolveTokenErrorMessage);
            return null;
        }
    }

    public async Task<IReadOnlyList<AuthTokenInfo>> ListTokensAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var db = _connection.GetDatabase();
        var results = new List<AuthTokenInfo>();

        foreach (var endpoint in _connection.GetEndPoints()) {
            var server = _connection.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica || server.ServerType == ServerType.Sentinel)
                continue;

            try {
                await foreach (var key in server.KeysAsync(pattern: TokenKeyPattern, pageSize: 1000, flags: CommandFlags.DemandMaster)) {
                    cancellationToken.ThrowIfCancellationRequested();

                    var value = await db.StringGetAsync(key).ConfigureAwait(false);
                    if (value.IsNullOrEmpty)
                        continue;

                    var currentUserId = value.ToString();
                    if (!string.IsNullOrWhiteSpace(userId) && !string.Equals(currentUserId, userId, StringComparison.Ordinal))
                        continue;

                    var ttl = await db.KeyTimeToLiveAsync(key).ConfigureAwait(false);
                    var tokenKey = key.ToString();
                    var token = tokenKey.StartsWith(TokenKeyPrefix, StringComparison.Ordinal)
                                    ? tokenKey[TokenKeyPrefix.Length..]
                                    : tokenKey;

                    results.Add(new AuthTokenInfo(token, currentUserId, ttl));
                }
            }
            catch (Exception ex) when (ex is RedisException or InvalidOperationException) {
                logger.LogWarning(ex, EnumerateTokensErrorMessage, endpoint);
            }
        }

        return results;
    }

    public async Task<bool> RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var db = _connection.GetDatabase();
        return await db.KeyDeleteAsync(TokenKeyPrefix + token).ConfigureAwait(false);
    }

    private static string GenerateToken(string userId)
    {
        Span<byte> randomBytes = stackalloc byte[TokenBytesLength];
        RandomNumberGenerator.Fill(randomBytes);
        var randomSegment = Convert.ToHexString(randomBytes);
        return $"{randomSegment}{userId}";
    }
}
