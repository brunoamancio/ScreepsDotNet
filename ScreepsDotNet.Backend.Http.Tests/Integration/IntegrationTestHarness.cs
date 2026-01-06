namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System;
using System.Collections.Generic;
using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using Testcontainers.MongoDb;
using Testcontainers.Redis;

public sealed class IntegrationTestHarness : IAsyncLifetime
{
    private const string UsersCollectionName = "users";
    private const string RoomsCollectionName = "rooms";
    private const string RoomsObjectsCollectionName = "rooms.objects";
    private const string ServerDataCollectionName = "server.data";
    private const string UserMoneyCollectionName = "users.money";
    private const string UserConsoleCollectionName = "users.console";
    private const string UserMemoryCollectionName = "users.memory";
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
        Database = _mongoClient.GetDatabase(IntegrationTestValues.Database.Name);
        Factory = new IntegrationWebApplicationFactory(mongoConnectionString,
                                                       IntegrationTestValues.Database.Name,
                                                       _redisContainer.GetConnectionString(),
                                                       IntegrationTestValues.User.Id,
                                                       IntegrationTestValues.Auth.Ticket,
                                                       IntegrationTestValues.Auth.SteamId);
        InitializedAtUtc = DateTime.UtcNow;
        await ResetStateAsync();
    }

    public async Task ResetStateAsync()
    {
        if (_mongoClient is null)
            return;

        await DropCollectionIfExistsAsync(UsersCollectionName);
        await DropCollectionIfExistsAsync(RoomsCollectionName);
        await DropCollectionIfExistsAsync(RoomsObjectsCollectionName);
        await DropCollectionIfExistsAsync(ServerDataCollectionName);
        await DropCollectionIfExistsAsync(UserMoneyCollectionName);
        await DropCollectionIfExistsAsync(UserConsoleCollectionName);
        await DropCollectionIfExistsAsync(UserMemoryCollectionName);

        await SeedUsersAsync();
        await SeedRoomsAsync();
        await SeedServerDataAsync();
        await SeedRoomObjectsAsync();
        await SeedMoneyHistoryAsync();
        await SeedUserMemoryAsync();
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
            Id = IntegrationTestValues.User.Id,
            Username = IntegrationTestValues.User.Username,
            UsernameLower = IntegrationTestValues.User.Username.ToLowerInvariant(),
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

    private Task SeedRoomObjectsAsync()
    {
        var rooms = Database.GetCollection<RoomObjectDocument>(RoomsObjectsCollectionName);
        var documents = new[]
        {
            new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                UserId = IntegrationTestValues.User.Id,
                Type = ControllerTypeValue,
                Room = IntegrationTestValues.World.StartRoom,
                Level = ControllerLevel
            },
            new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                UserId = IntegrationTestValues.User.Id,
                Type = SpawnTypeValue,
                Room = IntegrationTestValues.World.StartRoom
            }
        };

        return rooms.InsertManyAsync(documents);
    }

    private Task SeedMoneyHistoryAsync()
    {
        var money = Database.GetCollection<UserMoneyEntryDocument>(UserMoneyCollectionName);
        var document = new UserMoneyEntryDocument
        {
            Id = ObjectId.GenerateNewId(),
            UserId = IntegrationTestValues.User.Id,
            Date = DateTime.UtcNow.AddMinutes(-5),
            ExtraElements = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["change"] = IntegrationTestValues.Money.Change,
                ["balance"] = IntegrationTestValues.Money.Balance,
                ["type"] = IntegrationTestValues.Money.Type,
                ["description"] = IntegrationTestValues.Money.Description
            }
        };

        return money.InsertOneAsync(document);
    }

    private Task SeedUserMemoryAsync()
    {
        var memory = Database.GetCollection<UserMemoryDocument>(UserMemoryCollectionName);
        var document = new UserMemoryDocument
        {
            UserId = IntegrationTestValues.User.Id,
            Memory = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["settings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["logLevel"] = "info"
                }
            },
            Segments = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [IntegrationTestValues.Memory.SegmentId.ToString(CultureInfo.InvariantCulture)] = IntegrationTestValues.Memory.SegmentValue
            }
        };

        return memory.InsertOneAsync(document);
    }

    private Task SeedRoomsAsync()
    {
        var rooms = Database.GetCollection<RoomDocument>(RoomsCollectionName);
        var document = new RoomDocument
        {
            Id = ObjectId.GenerateNewId(),
            Name = IntegrationTestValues.World.StartRoom,
            Owner = IntegrationTestValues.User.Username,
            Controller = new RoomControllerDocument
            {
                Level = ControllerLevel
            },
            EnergyAvailable = 500
        };

        return rooms.InsertOneAsync(document);
    }

    private Task SeedServerDataAsync()
    {
        var collection = Database.GetCollection<ServerDataDocument>(ServerDataCollectionName);
        var document = new ServerDataDocument
        {
            WelcomeText = IntegrationTestValues.ServerData.WelcomeText,
            CustomObjectTypes = IntegrationTestValues.ServerData.CreateCustomObjectTypes(),
            HistoryChunkSize = IntegrationTestValues.ServerData.HistoryChunkSize,
            SocketUpdateThrottle = IntegrationTestValues.ServerData.SocketUpdateThrottle,
            Renderer = new ServerRendererDocument
            {
                Resources = IntegrationTestValues.ServerData.CreateRendererResources(),
                Metadata = IntegrationTestValues.ServerData.CreateRendererMetadata()
            }
        };

        return collection.ReplaceOneAsync(doc => doc.Id == ServerDataDocument.DefaultId,
                                          document,
                                          new ReplaceOptions { IsUpsert = true });
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _mongoContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }
}
