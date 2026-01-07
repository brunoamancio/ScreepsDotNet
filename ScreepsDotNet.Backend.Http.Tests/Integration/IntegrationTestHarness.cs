namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System;
using System.Collections.Generic;
using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using Testcontainers.MongoDb;
using Testcontainers.Redis;

public sealed class IntegrationTestHarness : IAsyncLifetime
{
    private const string UsersCollectionName = "users";
    private const string RoomsCollectionName = "rooms";
    private const string RoomsObjectsCollectionName = "rooms.objects";
    private const string RoomsTerrainCollectionName = "rooms.terrain";
    private const string WorldInfoCollectionName = "world.info";
    private const string ServerDataCollectionName = "server.data";
    private const string UserMoneyCollectionName = "users.money";
    private const string UserConsoleCollectionName = "users.console";
    private const string UserMemoryCollectionName = "users.memory";
    private const string MarketOrdersCollectionName = "market.orders";
    private const string MarketStatsCollectionName = "market.stats";
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

        await DropCollectionIfExistsAsync(UsersCollectionName);
        await DropCollectionIfExistsAsync(RoomsCollectionName);
        await DropCollectionIfExistsAsync(RoomsObjectsCollectionName);
        await DropCollectionIfExistsAsync(RoomsTerrainCollectionName);
        await DropCollectionIfExistsAsync(ServerDataCollectionName);
        await DropCollectionIfExistsAsync(UserMoneyCollectionName);
        await DropCollectionIfExistsAsync(UserConsoleCollectionName);
        await DropCollectionIfExistsAsync(UserMemoryCollectionName);
        await DropCollectionIfExistsAsync(MarketOrdersCollectionName);
        await DropCollectionIfExistsAsync(MarketStatsCollectionName);
        await DropCollectionIfExistsAsync(WorldInfoCollectionName);

        await SeedUsersAsync();
        await SeedRoomsAsync();
        await SeedRoomTerrainAsync();
        await SeedServerDataAsync();
        await SeedRoomObjectsAsync();
        await SeedMoneyHistoryAsync();
        await SeedUserMemoryAsync();
        await SeedMarketOrdersAsync();
        await SeedMarketStatsAsync();
        await SeedWorldInfoAsync();
    }

    private async Task DropCollectionIfExistsAsync(string name)
    {
        try {
            await Database.DropCollectionAsync(name);
        }
        catch (MongoCommandException ex) when (ex.CodeName == MongoNamespaceNotFoundCode) {
            // collection did not exist
        }
    }

    private Task SeedUsersAsync()
    {
        var users = Database.GetCollection<UserDocument>(UsersCollectionName);
        var document = new UserDocument
        {
            Id = SeedDataDefaults.User.Id,
            Username = SeedDataDefaults.User.Username,
            UsernameLower = SeedDataDefaults.User.Username.ToLowerInvariant(),
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
                UserId = SeedDataDefaults.User.Id,
                Type = RoomObjectType.Controller.ToDocumentValue(),
                Room = SeedDataDefaults.World.StartRoom,
                Level = ControllerLevel,
                SafeMode = SeedDataDefaults.World.SafeModeExpiry,
                Sign = new RoomSignDocument
                {
                    UserId = SeedDataDefaults.User.Id,
                    Text = SeedDataDefaults.World.ControllerSign,
                    Time = SeedDataDefaults.World.GameTime
                }
            },
            new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                UserId = SeedDataDefaults.User.Id,
                Type = RoomObjectType.Spawn.ToDocumentValue(),
                Room = SeedDataDefaults.World.StartRoom
            },
            new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                Type = "mineral",
                Room = SeedDataDefaults.World.StartRoom,
                MineralType = SeedDataDefaults.World.MineralType,
                Density = SeedDataDefaults.World.MineralDensity
            },
            new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                UserId = SeedDataDefaults.World.InvaderUser,
                Type = "invaderCore",
                Room = SeedDataDefaults.World.SecondaryRoom,
                Level = 2
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
            UserId = SeedDataDefaults.User.Id,
            Date = DateTime.UtcNow.AddMinutes(-5),
            ExtraElements = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["change"] = SeedDataDefaults.Money.Change,
                ["balance"] = SeedDataDefaults.Money.Balance,
                ["type"] = SeedDataDefaults.Money.Type,
                ["description"] = SeedDataDefaults.Money.Description
            }
        };

        return money.InsertOneAsync(document);
    }

    private Task SeedUserMemoryAsync()
    {
        var memory = Database.GetCollection<UserMemoryDocument>(UserMemoryCollectionName);
        var document = new UserMemoryDocument
        {
            UserId = SeedDataDefaults.User.Id,
            Memory = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["settings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["logLevel"] = "info"
                }
            },
            Segments = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [SeedDataDefaults.Memory.SegmentId.ToString(CultureInfo.InvariantCulture)] = SeedDataDefaults.Memory.SegmentValue
            }
        };

        return memory.InsertOneAsync(document);
    }

    private Task SeedRoomsAsync()
    {
        var rooms = Database.GetCollection<RoomDocument>(RoomsCollectionName);
        var startRoom = new RoomDocument
        {
            Id = SeedDataDefaults.World.StartRoom,
            Status = "normal",
            Novice = false,
            RespawnArea = false,
            Owner = SeedDataDefaults.User.Username,
            Controller = new RoomControllerDocument
            {
                Level = ControllerLevel
            },
            EnergyAvailable = 500
        };

        var secondaryRoom = new RoomDocument
        {
            Id = SeedDataDefaults.World.SecondaryRoom,
            Status = "out of borders",
            Novice = false,
            RespawnArea = false,
            Owner = null,
            Controller = new RoomControllerDocument
            {
                Level = 0
            },
            EnergyAvailable = 0
        };

        return rooms.BulkWriteAsync([
            new ReplaceOneModel<RoomDocument>(Builders<RoomDocument>.Filter.Eq(room => room.Id, startRoom.Id), startRoom)
            {
                IsUpsert = true
            },
            new ReplaceOneModel<RoomDocument>(Builders<RoomDocument>.Filter.Eq(room => room.Id, secondaryRoom.Id), secondaryRoom)
            {
                IsUpsert = true
            }
        ]);
    }

    private Task SeedRoomTerrainAsync()
    {
        var terrains = Database.GetCollection<RoomTerrainDocument>(RoomsTerrainCollectionName);
        var documents = new[]
        {
            new RoomTerrainDocument
            {
                Id = ObjectId.GenerateNewId(),
                Room = SeedDataDefaults.World.StartRoom,
                Type = "terrain",
                Terrain = new string('0', 2500)
            },
            new RoomTerrainDocument
            {
                Id = ObjectId.GenerateNewId(),
                Room = SeedDataDefaults.World.SecondaryRoom,
                Type = "terrain",
                Terrain = new string('1', 2500)
            }
        };

        return terrains.InsertManyAsync(documents);
    }

    private Task SeedServerDataAsync()
    {
        var collection = Database.GetCollection<ServerDataDocument>(ServerDataCollectionName);
        var document = new ServerDataDocument
        {
            WelcomeText = SeedDataDefaults.ServerData.WelcomeText,
            CustomObjectTypes = SeedDataDefaults.ServerData.CreateCustomObjectTypes(),
            HistoryChunkSize = SeedDataDefaults.ServerData.HistoryChunkSize,
            SocketUpdateThrottle = SeedDataDefaults.ServerData.SocketUpdateThrottle,
            Renderer = new ServerRendererDocument
            {
                Resources = SeedDataDefaults.ServerData.CreateRendererResources(),
                Metadata = SeedDataDefaults.ServerData.CreateRendererMetadata()
            }
        };

        return collection.ReplaceOneAsync(doc => doc.Id == ServerDataDocument.DefaultId,
                                          document,
                                          new ReplaceOptions { IsUpsert = true });
    }

    private Task SeedMarketOrdersAsync()
    {
        var collection = Database.GetCollection<MarketOrderDocument>(MarketOrdersCollectionName);
        var orders = new List<MarketOrderDocument>
        {
            new()
            {
                Id = ObjectId.GenerateNewId(),
                Active = true,
                UserId = SeedDataDefaults.User.Id,
                Type = "sell",
                RoomName = SeedDataDefaults.World.StartRoom,
                ResourceType = "energy",
                Price = 5000,
                Amount = 1000,
                RemainingAmount = 750,
                TotalAmount = 1000,
                CreatedTick = 12345,
                CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            },
            new()
            {
                Id = ObjectId.GenerateNewId(),
                Active = true,
                UserId = null,
                Type = "buy",
                RoomName = "W2N2",
                ResourceType = "energy",
                Price = 4500,
                Amount = 800,
                RemainingAmount = 800,
                TotalAmount = 800,
                CreatedTick = 12346,
                CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }
        };

        return collection.InsertManyAsync(orders);
    }

    private Task SeedMarketStatsAsync()
    {
        var collection = Database.GetCollection<MarketStatsDocument>(MarketStatsCollectionName);
        var entries = new List<MarketStatsDocument>
        {
            new()
            {
                Id = ObjectId.GenerateNewId(),
                ResourceType = "energy",
                Date = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Transactions = 10,
                Volume = 5000,
                AveragePrice = 4.8,
                StandardDeviation = 0.3
            },
            new()
            {
                Id = ObjectId.GenerateNewId(),
                ResourceType = "energy",
                Date = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Transactions = 5,
                Volume = 2500,
                AveragePrice = 4.5,
                StandardDeviation = 0.25
            }
        };

        return collection.InsertManyAsync(entries);
    }

    private Task SeedWorldInfoAsync()
    {
        var collection = Database.GetCollection<WorldInfoDocument>(WorldInfoCollectionName);
        var document = new WorldInfoDocument
        {
            Id = WorldInfoDocument.DefaultId,
            GameTime = SeedDataDefaults.World.GameTime,
            TickDuration = SeedDataDefaults.World.TickDuration
        };

        return collection.ReplaceOneAsync(info => info.Id == document.Id,
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
