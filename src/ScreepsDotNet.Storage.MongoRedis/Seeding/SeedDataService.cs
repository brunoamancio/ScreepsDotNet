namespace ScreepsDotNet.Storage.MongoRedis.Seeding;

using System;
using System.Collections.Generic;
using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Common.Constants;
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
    private const string UsersPowerCreepsCollectionName = "users.power_creeps";
    private const string UsersMessagesCollectionName = "users.messages";
    private const string UsersNotificationsCollectionName = "users.notifications";
    private const string MarketOrdersCollectionName = "market.orders";
    private const string MarketStatsCollectionName = "market.stats";
    private const string UsersIntentsCollectionName = "users.intents";
    private const string RoomsIntentsCollectionName = "rooms.intents";
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
            WorldInfoCollectionName,
            UsersPowerCreepsCollectionName,
            UsersIntentsCollectionName,
            RoomsIntentsCollectionName,
            UsersMessagesCollectionName,
            UsersNotificationsCollectionName
        };

        foreach (var name in collectionNames)
            await DropCollectionIfExistsAsync(database, name, cancellationToken).ConfigureAwait(false);

        await SeedUsersAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedRoomsAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedRoomTerrainAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedServerDataAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedVersionInfoAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedRoomObjectsAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedPowerCreepsAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedMoneyHistoryAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedUserMemoryAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedMarketOrdersAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedMarketStatsAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedWorldInfoAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedRoomIntentsAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedUserIntentsAsync(database, cancellationToken).ConfigureAwait(false);
        await SeedUserMessagesAsync(database, cancellationToken).ConfigureAwait(false);
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
        var badge = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [BadgeDocumentFields.Type] = BadgeTypeValue,
            [BadgeDocumentFields.Color1] = BadgePrimaryColor,
            [BadgeDocumentFields.Color2] = BadgeSecondaryColor,
            [BadgeDocumentFields.Color3] = BadgeTertiaryColor,
            [BadgeDocumentFields.Param] = BadgeParamValue,
            [BadgeDocumentFields.Flip] = false
        };

        var primaryUser = new UserDocument
        {
            Id = SeedDataDefaults.User.Id,
            Username = SeedDataDefaults.User.Username,
            UsernameLower = SeedDataDefaults.User.Username.ToLowerInvariant(),
            Email = SeedDataDefaults.User.Email,
            Cpu = DefaultCpu,
            Active = ActiveFlagValue,
            Power = SeedDataDefaults.Power.Total,
            PowerExperimentations = SeedDataDefaults.Power.Experimentations,
            PowerExperimentationTime = 0,
            Badge = new Dictionary<string, object?>(badge, StringComparer.Ordinal)
        };

        var respondentUser = new UserDocument
        {
            Id = SeedDataDefaults.Messaging.RespondentId,
            Username = SeedDataDefaults.Messaging.RespondentUsername,
            UsernameLower = SeedDataDefaults.Messaging.RespondentUsername.ToLowerInvariant(),
            Cpu = DefaultCpu,
            Active = ActiveFlagValue,
            Badge = new Dictionary<string, object?>(badge, StringComparer.Ordinal)
        };

        return users.InsertManyAsync([primaryUser, respondentUser], cancellationToken: cancellationToken);
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
                Type = RoomObjectType.Mineral.ToDocumentValue(),
                Room = SeedDataDefaults.World.StartRoom,
                MineralType = SeedDataDefaults.World.MineralType,
                Density = SeedDataDefaults.World.MineralDensity
            },
            new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                UserId = SeedDataDefaults.World.InvaderUser,
                Type = RoomObjectType.InvaderCore.ToDocumentValue(),
                Room = SeedDataDefaults.World.SecondaryRoom,
                Level = 2
            },
            new RoomObjectDocument
            {
                Id = ObjectId.Parse(SeedDataDefaults.PowerCreeps.ActiveId),
                UserId = SeedDataDefaults.User.Id,
                Type = RoomObjectType.PowerCreep.ToDocumentValue(),
                Room = SeedDataDefaults.PowerCreeps.ActiveRoom,
                Shard = SeedDataDefaults.PowerCreeps.ActiveShardName,
                X = SeedDataDefaults.PowerCreeps.ActiveX,
                Y = SeedDataDefaults.PowerCreeps.ActiveY,
                Hits = SeedDataDefaults.PowerCreeps.ActiveHits,
                HitsMax = SeedDataDefaults.PowerCreeps.ActiveHitsMax,
                TicksToLive = SeedDataDefaults.PowerCreeps.ActiveTicksToLive,
                Store = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [ResourceTypes.Ops] = SeedDataDefaults.PowerCreeps.ActiveStoreOps
                },
                StoreCapacity = SeedDataDefaults.PowerCreeps.ActiveStoreCapacity,
                Fatigue = 0
            },
            new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                Type = RoomObjectType.Controller.ToDocumentValue(),
                Room = SeedDataDefaults.World.SecondaryShardRoom,
                Shard = SeedDataDefaults.World.SecondaryShardName,
                Reservation = new RoomReservationDocument
                {
                    UserId = SeedDataDefaults.User.Id,
                    EndTime = SeedDataDefaults.World.GameTime + SeedDataDefaults.World.SecondaryShardSafeModeExpiry
                },
                Sign = new RoomSignDocument
                {
                    UserId = SeedDataDefaults.User.Id,
                    Text = SeedDataDefaults.World.SecondaryShardControllerSign,
                    Time = SeedDataDefaults.World.GameTime
                }
            },
            new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                Type = RoomObjectType.Mineral.ToDocumentValue(),
                Room = SeedDataDefaults.World.SecondaryShardRoom,
                Shard = SeedDataDefaults.World.SecondaryShardName,
                MineralType = SeedDataDefaults.World.SecondaryShardMineralType,
                Density = SeedDataDefaults.World.MineralDensity
            },
            new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                Type = RoomObjectType.Controller.ToDocumentValue(),
                Room = SeedDataDefaults.Bots.SecondaryShardRoom,
                Shard = SeedDataDefaults.World.SecondaryShardName,
                Level = 0
            },
            new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                Type = RoomObjectType.Controller.ToDocumentValue(),
                Room = SeedDataDefaults.Strongholds.SecondaryShardRoom,
                Shard = SeedDataDefaults.World.SecondaryShardName,
                Level = 0
            },
            new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                Type = RoomObjectType.Controller.ToDocumentValue(),
                Room = SeedDataDefaults.Intents.SecondaryShardRoom,
                Shard = SeedDataDefaults.World.SecondaryShardName,
                Level = 0
            },
            new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                Type = StructureType.InvaderCore.ToDocumentValue(),
                Room = SeedDataDefaults.Strongholds.SecondaryShardRoom,
                Shard = SeedDataDefaults.World.SecondaryShardName,
                Level = SeedDataDefaults.Strongholds.SecondaryShardCoreLevel,
                StrongholdId = SeedDataDefaults.Strongholds.SecondaryShardStrongholdId,
                TemplateName = SeedDataDefaults.Strongholds.SecondaryShardTemplate,
                NextExpandTime = SeedDataDefaults.World.GameTime + 5000
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
            Status = RoomDocumentFields.RoomStatusValues.Normal,
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
            Status = RoomDocumentFields.RoomStatusValues.OutOfBorders,
            Novice = false,
            RespawnArea = false,
            Owner = null,
            Controller = new RoomControllerDocument
            {
                Level = 0
            },
            EnergyAvailable = 0
        };

        var shardRoom = new RoomDocument
        {
            Id = SeedDataDefaults.World.SecondaryShardRoom,
            Shard = SeedDataDefaults.World.SecondaryShardName,
            Status = RoomDocumentFields.RoomStatusValues.Normal,
            Novice = false,
            RespawnArea = false,
            Owner = null,
            Controller = new RoomControllerDocument
            {
                Level = 0
            },
            EnergyAvailable = 0
        };

        var botShardRoom = new RoomDocument
        {
            Id = SeedDataDefaults.Bots.SecondaryShardRoom,
            Shard = SeedDataDefaults.World.SecondaryShardName,
            Status = RoomDocumentFields.RoomStatusValues.Normal,
            Novice = false,
            RespawnArea = false,
            Controller = new RoomControllerDocument { Level = 0 },
            EnergyAvailable = 0
        };

        var strongholdShardRoom = new RoomDocument
        {
            Id = SeedDataDefaults.Strongholds.SecondaryShardRoom,
            Shard = SeedDataDefaults.World.SecondaryShardName,
            Status = RoomDocumentFields.RoomStatusValues.Normal,
            Novice = false,
            RespawnArea = false,
            Controller = new RoomControllerDocument { Level = 0 },
            EnergyAvailable = 0
        };

        var intentShardRoom = new RoomDocument
        {
            Id = SeedDataDefaults.Intents.SecondaryShardRoom,
            Shard = SeedDataDefaults.World.SecondaryShardName,
            Status = RoomDocumentFields.RoomStatusValues.Normal,
            Novice = false,
            RespawnArea = false,
            Controller = new RoomControllerDocument { Level = 0 },
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
            },
            new ReplaceOneModel<RoomDocument>(Builders<RoomDocument>.Filter.Eq(room => room.Id, shardRoom.Id), shardRoom)
            {
                IsUpsert = true
            },
            new ReplaceOneModel<RoomDocument>(Builders<RoomDocument>.Filter.Eq(room => room.Id, botShardRoom.Id), botShardRoom)
            {
                IsUpsert = true
            },
            new ReplaceOneModel<RoomDocument>(Builders<RoomDocument>.Filter.Eq(room => room.Id, strongholdShardRoom.Id), strongholdShardRoom)
            {
                IsUpsert = true
            },
            new ReplaceOneModel<RoomDocument>(Builders<RoomDocument>.Filter.Eq(room => room.Id, intentShardRoom.Id), intentShardRoom)
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
            },
            new RoomTerrainDocument
            {
                Id = ObjectId.GenerateNewId(),
                Room = SeedDataDefaults.World.SecondaryShardRoom,
                Shard = SeedDataDefaults.World.SecondaryShardName,
                Type = "terrain",
                Terrain = new string('2', 2500)
            },
            new RoomTerrainDocument
            {
                Id = ObjectId.GenerateNewId(),
                Room = SeedDataDefaults.Bots.SecondaryShardRoom,
                Shard = SeedDataDefaults.World.SecondaryShardName,
                Type = "terrain",
                Terrain = new string('0', 2500)
            },
            new RoomTerrainDocument
            {
                Id = ObjectId.GenerateNewId(),
                Room = SeedDataDefaults.Strongholds.SecondaryShardRoom,
                Shard = SeedDataDefaults.World.SecondaryShardName,
                Type = "terrain",
                Terrain = new string('0', 2500)
            },
            new RoomTerrainDocument
            {
                Id = ObjectId.GenerateNewId(),
                Room = SeedDataDefaults.Intents.SecondaryShardRoom,
                Shard = SeedDataDefaults.World.SecondaryShardName,
                Type = "terrain",
                Terrain = new string('0', 2500)
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

    private static Task SeedRoomIntentsAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<RoomIntentDocument>(RoomsIntentsCollectionName);
        var documents = new[]
        {
            new RoomIntentDocument
            {
                Id = ObjectId.GenerateNewId(),
                Room = SeedDataDefaults.World.StartRoom,
                Shard = null,
                Users = new Dictionary<string, RoomIntentUserDocument>(StringComparer.Ordinal)
                {
                    [SeedDataDefaults.User.Id] = new()
                    {
                        ObjectsManual = new Dictionary<string, BsonDocument>(StringComparer.Ordinal)
                        {
                            ["seed-controller"] = new(IntentKeys.Move, new BsonDocument
                            {
                                ["direction"] = 2,
                                ["id"] = "seed-controller"
                            })
                        }
                    }
                }
            },
            new RoomIntentDocument
            {
                Id = ObjectId.GenerateNewId(),
                Room = SeedDataDefaults.Intents.SecondaryShardRoom,
                Shard = SeedDataDefaults.World.SecondaryShardName,
                Users = new Dictionary<string, RoomIntentUserDocument>(StringComparer.Ordinal)
                {
                    [SeedDataDefaults.User.Id] = new()
                    {
                        ObjectsManual = new Dictionary<string, BsonDocument>(StringComparer.Ordinal)
                        {
                            [SeedDataDefaults.Intents.SecondaryShardObjectId] = new(IntentKeys.Move, new BsonDocument
                            {
                                ["direction"] = 1,
                                ["id"] = SeedDataDefaults.Intents.SecondaryShardObjectId
                            })
                        }
                    }
                }
            }
        };

        return collection.InsertManyAsync(documents, cancellationToken: cancellationToken);
    }

    private static Task SeedUserIntentsAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<UserIntentDocument>(UsersIntentsCollectionName);
        var document = new UserIntentDocument
        {
            Id = ObjectId.GenerateNewId(),
            UserId = SeedDataDefaults.User.Id,
            Intents = new BsonDocument("notify", new BsonArray
            {
                new BsonDocument
                {
                    [UserDocumentFields.NotificationFields.Message] = "Seed message",
                    ["groupInterval"] = 5
                }
            })
        };

        return collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    private static async Task SeedUserMessagesAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<UserMessageDocument>(UsersMessagesCollectionName);
        var now = DateTime.UtcNow;
        var outbound = new UserMessageDocument
        {
            Id = ObjectId.GenerateNewId(),
            UserId = SeedDataDefaults.Messaging.RespondentId,
            RespondentId = SeedDataDefaults.User.Id,
            Date = now,
            Type = UserMessagingConstants.MessageTypes.Outgoing,
            Text = SeedDataDefaults.Messaging.SampleText,
            Unread = false
        };

        var inbound = new UserMessageDocument
        {
            Id = ObjectId.GenerateNewId(),
            UserId = SeedDataDefaults.User.Id,
            RespondentId = SeedDataDefaults.Messaging.RespondentId,
            Date = now,
            Type = UserMessagingConstants.MessageTypes.Incoming,
            Text = SeedDataDefaults.Messaging.SampleText,
            Unread = true,
            OutMessageId = outbound.Id
        };

        await collection.InsertManyAsync([outbound, inbound], cancellationToken: cancellationToken).ConfigureAwait(false);

        var notifications = database.GetCollection<UserNotificationDocument>(UsersNotificationsCollectionName);
        var notification = new UserNotificationDocument
        {
            Id = ObjectId.GenerateNewId(),
            UserId = SeedDataDefaults.User.Id,
            Message = $"New message from {SeedDataDefaults.Messaging.RespondentUsername}",
            Date = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Type = UserMessagingConstants.NotificationTypeMessage,
            Count = 1
        };

        await notifications.InsertOneAsync(notification, cancellationToken: cancellationToken).ConfigureAwait(false);
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
                Type = MarketOrderTypes.Sell,
                RoomName = SeedDataDefaults.World.StartRoom,
                ResourceType = ResourceTypes.Energy,
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
                Type = MarketOrderTypes.Buy,
                RoomName = "W2N2",
                ResourceType = ResourceTypes.Energy,
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
                ResourceType = ResourceTypes.Energy,
                Date = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Transactions = 10,
                Volume = 5000,
                AveragePrice = 4.8,
                StandardDeviation = 0.3
            },
            new()
            {
                Id = ObjectId.GenerateNewId(),
                ResourceType = ResourceTypes.Energy,
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

    private static Task SeedPowerCreepsAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<PowerCreepDocument>(UsersPowerCreepsCollectionName);
        var documents = new[]
        {
            new PowerCreepDocument
            {
                Id = ObjectId.Parse(SeedDataDefaults.PowerCreeps.ActiveId),
                UserId = SeedDataDefaults.User.Id,
                Name = SeedDataDefaults.PowerCreeps.ActiveName,
                ClassName = SeedDataDefaults.PowerCreeps.ClassName,
                Level = 3,
                HitsMax = SeedDataDefaults.PowerCreeps.ActiveHitsMax,
                Store = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [ResourceTypes.Ops] = SeedDataDefaults.PowerCreeps.ActiveStoreOps
                },
                StoreCapacity = SeedDataDefaults.PowerCreeps.ActiveStoreCapacity,
                SpawnCooldownTime = null,
                Shard = SeedDataDefaults.PowerCreeps.ActiveShardName,
                Powers = new Dictionary<string, PowerCreepPowerDocument>(StringComparer.Ordinal)
                {
                    ["1"] = new() { Level = 2 },
                    ["2"] = new() { Level = 1 }
                }
            },
            new PowerCreepDocument
            {
                Id = ObjectId.Parse(SeedDataDefaults.PowerCreeps.DormantId),
                UserId = SeedDataDefaults.User.Id,
                Name = SeedDataDefaults.PowerCreeps.DormantName,
                ClassName = SeedDataDefaults.PowerCreeps.ClassName,
                Level = 1,
                HitsMax = 2000,
                Store = new Dictionary<string, int>(StringComparer.Ordinal),
                StoreCapacity = 200,
                SpawnCooldownTime = 0,
                Powers = new Dictionary<string, PowerCreepPowerDocument>(StringComparer.Ordinal)
                {
                    ["1"] = new() { Level = 1 }
                }
            }
        };

        return collection.InsertManyAsync(documents, cancellationToken: cancellationToken);
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
