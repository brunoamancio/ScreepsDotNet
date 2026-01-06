namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using Testcontainers.MongoDb;
using Testcontainers.Redis;

public sealed class IntegrationTestHarness : IAsyncLifetime
{
    private const string UsersCollectionName = "users";
    private const string RoomsObjectsCollectionName = "rooms.objects";
    private const string ControllerTypeValue = "controller";
    private const string SpawnTypeValue = "spawn";
    private const string MongoNamespaceNotFoundCode = "NamespaceNotFound";
    private const string MongoImage = "mongo:7.0";
    private const string RedisImage = "redis:7.2";
    private const string BadgePrimaryColor = "#ffffff";
    private const string BadgeSecondaryColor = "#000000";
    private const string BadgeTertiaryColor = "#888888";
    private const int DefaultCpu = 100;
    private const int ActiveFlagValue = 1;
    private const int BadgeTypeValue = 1;
    private const int BadgeParamValue = 0;
    private const int ControllerLevel = 3;

    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder(MongoImage).Build();
    private readonly RedisContainer _redisContainer = new RedisBuilder(RedisImage).Build();

    private MongoClient? _mongoClient;

    internal IntegrationWebApplicationFactory Factory { get; private set; } = null!;

    public IMongoDatabase Database { get; private set; } = null!;

    public DateTime InitializedAtUtc { get; private set; }

    public async Task InitializeAsync()
    {
        await _mongoContainer.StartAsync();
        await _redisContainer.StartAsync();

        var mongoConnectionString = _mongoContainer.GetConnectionString();
        _mongoClient = new MongoClient(mongoConnectionString);
        Database = _mongoClient.GetDatabase(IntegrationTestValues.DatabaseName);
        Factory = new IntegrationWebApplicationFactory(mongoConnectionString, IntegrationTestValues.DatabaseName, _redisContainer.GetConnectionString(),
                                                       IntegrationTestValues.UserId, IntegrationTestValues.AuthTicket,IntegrationTestValues.SteamId);
        InitializedAtUtc = DateTime.UtcNow;
        await ResetStateAsync();
    }

    public async Task ResetStateAsync()
    {
        if (_mongoClient is null)
            return;

        await DropCollectionIfExistsAsync(UsersCollectionName);
        await DropCollectionIfExistsAsync(RoomsObjectsCollectionName);

        await SeedUsersAsync();
        await SeedRoomsAsync();
    }

    private async Task DropCollectionIfExistsAsync(string name)
    {
        try
        {
            await Database.DropCollectionAsync(name);
        }
        catch (MongoCommandException ex) when (ex.CodeName == MongoNamespaceNotFoundCode)
        {
            // collection did not exist
        }
    }

    private Task SeedUsersAsync()
    {
        var users = Database.GetCollection<UserDocument>(UsersCollectionName);
        var document = new UserDocument
        {
            Id = IntegrationTestValues.UserId,
            Username = IntegrationTestValues.Username,
            UsernameLower = IntegrationTestValues.Username.ToLowerInvariant(),
            Cpu = DefaultCpu,
            Active = ActiveFlagValue,
            Badge = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [BadgeDocumentFields.Type] = BadgeTypeValue,
                [BadgeDocumentFields.Color1] = BadgePrimaryColor,
                [BadgeDocumentFields.Color2] = BadgeSecondaryColor,
                [BadgeDocumentFields.Color3] = BadgeTertiaryColor,
                [BadgeDocumentFields.Param] = BadgeParamValue,
                [BadgeDocumentFields.Flip] = false
            }
        };

        return users.InsertOneAsync(document);
    }

    private Task SeedRoomsAsync()
    {
        var rooms = Database.GetCollection<RoomObjectDocument>(RoomsObjectsCollectionName);
        var documents = new[]
        {
            new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                UserId = IntegrationTestValues.UserId,
                Type = ControllerTypeValue,
                Room = IntegrationTestValues.StartRoom,
                Level = ControllerLevel
            },
            new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                UserId = IntegrationTestValues.UserId,
                Type = SpawnTypeValue,
                Room = IntegrationTestValues.StartRoom
            }
        };

        return rooms.InsertManyAsync(documents);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _mongoContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }
}
