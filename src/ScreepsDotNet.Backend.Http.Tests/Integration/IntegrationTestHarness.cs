namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Seeding;
using StackExchange.Redis;
using Testcontainers.MongoDb;
using Testcontainers.Redis;

public sealed class IntegrationTestHarness : IAsyncLifetime
{
    private const string MongoImage = "mongo:7.0";
    private const string RedisImage = "redis:7.2";
    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder(MongoImage).Build();
    private readonly RedisContainer _redisContainer = new RedisBuilder(RedisImage).Build();
    private readonly ISeedDataService _seedDataService = new SeedDataService();

    private MongoClient? _mongoClient;

    internal IntegrationWebApplicationFactory Factory { get; private set; } = null!;

    public IMongoDatabase Database { get; private set; } = null!;

    public string RedisConnectionString => _redisContainer.GetConnectionString();

    public DateTime InitializedAtUtc { get; private set; }

    public async ValueTask InitializeAsync()
    {
        await _mongoContainer.StartAsync();
        await _redisContainer.StartAsync();

        var mongoConnectionString = _mongoContainer.GetConnectionString();
        _mongoClient = new MongoClient(mongoConnectionString);
        Database = _mongoClient.GetDatabase(SeedDataDefaults.Database.Name);
        Factory = new IntegrationWebApplicationFactory(mongoConnectionString,
                                                       SeedDataDefaults.Database.Name,
                                                       _redisContainer.GetConnectionString(),
                                                       SeedDataDefaults.User.Id,
                                                       SeedDataDefaults.Auth.Ticket,
                                                       SeedDataDefaults.Auth.SteamId);
        InitializedAtUtc = DateTime.UtcNow;
        await ResetStateAsync();
    }

    public async Task ResetStateAsync()
    {
        if (_mongoClient is null)
            return;

        await _seedDataService.ReseedAsync(Database).ConfigureAwait(false);
        await ResetRedisStateAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _mongoContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }

    private async Task ResetRedisStateAsync()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString).ConfigureAwait(false);
        var db = connection.GetDatabase();
        await db.StringSetAsync(SystemControlConstants.MainLoopPausedKey, "0").ConfigureAwait(false);
        await db.StringSetAsync(SystemControlConstants.MainLoopMinimumDurationKey, SystemControlConstants.DefaultTickDurationMilliseconds).ConfigureAwait(false);
    }
}
