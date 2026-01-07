using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using StackExchange.Redis;

namespace ScreepsDotNet.Storage.MongoRedis.Services;

public sealed class RedisTokenService(IRedisConnectionProvider connectionProvider, IOptions<AuthOptions> options, ILogger<RedisTokenService> logger)
    : ITokenService
{
    private const string TokenKeyPrefix = "auth_";
    private const int TokenBytesLength = 20;
    private const string MissingUserIdMessage = "User identifier must be provided.";
    private const string ResolveTokenErrorMessage = "Unable to resolve auth token.";

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

    private static string GenerateToken(string userId)
    {
        Span<byte> randomBytes = stackalloc byte[TokenBytesLength];
        RandomNumberGenerator.Fill(randomBytes);
        var randomSegment = Convert.ToHexString(randomBytes);
        return $"{randomSegment}{userId}";
    }
}
