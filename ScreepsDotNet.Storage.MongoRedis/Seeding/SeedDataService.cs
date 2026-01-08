namespace ScreepsDotNet.Storage.MongoRedis.Seeding;

using System;
using System.Collections.Generic;
using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class SeedDataService : ISeedDataService
{
    private const string UsersCollectionName = "users";
    private const string RoomsCollectionName = "rooms";
    private const string RoomsObjectsCollectionName = "rooms.objects";
    private const string RoomsTerrainCollectionName = "rooms.terrain";
    private const string WorldInfoCollectionName = "world.info";
    private const string ServerDataCollectionName = "server.data";
    private const string VersionInfoCollectionName = "server.version";
    private const string UserMoneyCollectionName = "users.money";
    private const string UserConsoleCollectionName = "users.console";
    private const string UserMemoryCollectionName = "users.memory";
    private const string MarketOrdersCollectionName = "market.orders";
    private const string MarketStatsCollectionName = "market.stats";
    private const string MongoNamespaceNotFoundCode = "NamespaceNotFound";

    private const int DefaultCpu = 100;
    private const int ActiveFlagValue = 1;
    private const int BadgeTypeValue = 1;
    private const int BadgeParamValue = 0;
    private const int ControllerLevel = 3;
    private const string BadgePrimaryColor = "#ffffff";
    private const string BadgeSecondaryColor = "#000000";
    private const string BadgeTertiaryColor = "#888888";

    public Task ReseedAsync(string mongoConnectionString, string databaseName, CancellationToken cancellationToken = default)
    {
        var client = new MongoClient(mongoConnectionString);
        return ReseedAsync(client.GetDatabase(databaseName), cancellationToken);
    }

    public async Task ReseedAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var collectionNames = new[]
        {
            UsersCollectionName,
            RoomsCollectionName,
            RoomsObjectsCollectionName,
            RoomsTerrainCollectionName,
            ServerDataCollectionName,
            VersionInfoCollectionName,
            UserMoneyCollectionName,
            UserConsoleCollectionName,
            UserMemoryCollectionName,
            MarketOrdersCollectionName,
            MarketStatsCollectionName,
            WorldInfoCollectionName
        };

        foreach (var name in collectionNames)
            await DropCollectionIfExistsAsync(database, name, cancellationToken).ConfigureAwait(false);

        await SeedUsersAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedRoomsAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedRoomTerrainAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedServerDataAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedVersionInfoAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedRoomObjectsAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedMoneyHistoryAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedUserMemoryAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedMarketOrdersAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedMarketStatsAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedWorldInfoAsync(database, cancellationToken).ConfigureAwait(false);
    }

    private static async Task DropCollectionIfExistsAsync(IMongoDatabase database, string name, CancellationToken cancellationToken)
    {
        try {
            await database.DropCollectionAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (MongoCommandException ex) when (ex.CodeName == MongoNamespaceNotFoundCode) {
            // collection did not exist
        }
    }

    private static Task SeedUsersAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var users = database.GetCollection<UserDocument>(UsersCollectionName);
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

        return users.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    private static Task SeedRoomObjectsAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var rooms = database.GetCollection<RoomObjectDocument>(RoomsObjectsCollectionName);
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

        return rooms.InsertManyAsync(documents, cancellationToken: cancellationToken);
    }

    private static Task SeedMoneyHistoryAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var money = database.GetCollection<UserMoneyEntryDocument>(UserMoneyCollectionName);
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

        return money.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    private static Task SeedUserMemoryAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var memory = database.GetCollection<UserMemoryDocument>(UserMemoryCollectionName);
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

        return memory.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    private static Task SeedRoomsAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var rooms = database.GetCollection<RoomDocument>(RoomsCollectionName);
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
        ], options: null, cancellationToken);
    }

    private static Task SeedRoomTerrainAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var terrains = database.GetCollection<RoomTerrainDocument>(RoomsTerrainCollectionName);
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

        return terrains.InsertManyAsync(documents, cancellationToken: cancellationToken);
    }

    private static Task SeedServerDataAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<ServerDataDocument>(ServerDataCollectionName);
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
                                          new ReplaceOptions { IsUpsert = true },
                                          cancellationToken);
    }

    private static Task SeedVersionInfoAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<VersionMetadataDocument>(VersionInfoCollectionName);
        var document = new VersionMetadataDocument
        {
            Protocol = SeedDataDefaults.Version.Protocol,
            UseNativeAuth = SeedDataDefaults.Version.UseNativeAuth,
            PackageVersion = SeedDataDefaults.Version.PackageVersion
        };

        return collection.ReplaceOneAsync(doc => doc.Id == VersionMetadataDocument.DefaultId,
                                          document,
                                          new ReplaceOptions { IsUpsert = true },
                                          cancellationToken);
    }

    private static Task SeedMarketOrdersAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
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

        var collection = database.GetCollection<MarketOrderDocument>(MarketOrdersCollectionName);
        return collection.InsertManyAsync(orders, cancellationToken: cancellationToken);
    }

    private static Task SeedMarketStatsAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
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

        var collection = database.GetCollection<MarketStatsDocument>(MarketStatsCollectionName);
        return collection.InsertManyAsync(entries, cancellationToken: cancellationToken);
    }

    private static Task SeedWorldInfoAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<WorldInfoDocument>(WorldInfoCollectionName);
        var document = new WorldInfoDocument
        {
            Id = WorldInfoDocument.DefaultId,
            GameTime = SeedDataDefaults.World.GameTime,
            TickDuration = SeedDataDefaults.World.TickDuration
        };

        return collection.ReplaceOneAsync(info => info.Id == document.Id,
                                          document,
                                          new ReplaceOptions { IsUpsert = true },
                                          cancellationToken);
    }
}
