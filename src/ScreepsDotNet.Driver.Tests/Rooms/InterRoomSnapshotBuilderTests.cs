using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Services.Rooms;
using ScreepsDotNet.Driver.Tests.TestDoubles;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using ScreepsDotNet.Common.Constants;

namespace ScreepsDotNet.Driver.Tests.Rooms;

public sealed class InterRoomSnapshotBuilderTests
{
    static InterRoomSnapshotBuilderTests()
    {
        RegisterClassMap<RoomObjectDocument>();
        RegisterClassMap<RoomDocument>();
        RegisterClassMap<UserDocument>();
        RegisterClassMap<MarketOrderDocument>();
        RegisterClassMap<PowerCreepDocument>();
        RegisterClassMap<PowerCreepPowerDocument>();
        RegisterClassMap<UserIntentDocument>();
    }

    private static void RegisterClassMap<TDocument>()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(TDocument)))
            BsonClassMap.RegisterClassMap<TDocument>(map => map.AutoMap());
    }

    [Fact]
    public async Task BuildAsync_MapsDocumentsIntoContracts()
    {
        var dataService = new StubRoomDataService();
        var builder = new InterRoomSnapshotBuilder(dataService);

        var snapshot = await builder.BuildAsync(123, TestContext.Current.CancellationToken);

        Assert.Equal(123, snapshot.GameTime);
        var creep = Assert.Single(snapshot.MovingCreeps);
        Assert.Equal(RoomObjectTypes.Creep, creep.Type);

        Assert.True(snapshot.AccessibleRooms.ContainsKey("W0N0"));
        Assert.Equal("W0N0", snapshot.AccessibleRooms["W0N0"].RoomName);

        var special = Assert.Single(snapshot.SpecialRoomObjects);
        Assert.Equal(RoomObjectTypes.PowerBank, special.Type);

        var order = Assert.Single(snapshot.Market.Orders);
        Assert.False(string.IsNullOrWhiteSpace(order.Id));
        Assert.Equal("userA", order.UserId);
        Assert.True(snapshot.Market.Users.ContainsKey("userA"));

        var powerCreep = Assert.Single(snapshot.Market.PowerCreeps);
        Assert.Equal("PowerAlpha", powerCreep.Name);
        Assert.Single(powerCreep.Powers);

        var intent = Assert.Single(snapshot.Market.UserIntents);
        Assert.Equal("userA", intent.UserId);
        var intentRecord = Assert.Single(intent.Intents);
        Assert.Equal("attack", intentRecord.Name);
        var argument = Assert.Single(intentRecord.Arguments);
        Assert.True(argument.Fields.TryGetValue("room", out var roomField));
        Assert.Equal("W0N0", roomField.TextValue);
    }

    private sealed class StubRoomDataService : RoomDataServiceDouble
    {
        public override Task<InterRoomSnapshot> GetInterRoomSnapshotAsync(int gameTime, CancellationToken token = default)
        {
            var creep = new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                Type = RoomObjectTypes.Creep,
                Room = "W0N0",
                X = 10,
                Y = 20,
                Store = new Dictionary<string, int> { ["energy"] = 50 }
            };

            var special = new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                Type = RoomObjectTypes.PowerBank,
                Room = "W0N0",
                X = 25,
                Y = 25
            };

            var room = new RoomDocument
            {
                Id = "W0N0",
                Shard = "shard0",
                Status = "normal"
            };

            var order = new MarketOrderDocument
            {
                Id = ObjectId.Parse("64c19d382c00000000000001"),
                UserId = "userA",
                Type = MarketOrderTypes.Sell,
                ResourceType = "energy",
                Price = 1000,
                Amount = 100,
                RemainingAmount = 90,
                TotalAmount = 100,
                RoomName = "W0N1",
                Active = true
            };

            var user = new UserDocument
            {
                Id = "userA",
                Username = "Alpha",
                Cpu = 100,
                Power = 10,
                Money = 50,
                Active = 1
            };

            var powerCreep = new PowerCreepDocument
            {
                Id = ObjectId.Parse("64c19d382c00000000000002"),
                UserId = "userA",
                Name = "PowerAlpha",
                ClassName = "operator",
                Level = 5,
                Powers = new Dictionary<string, PowerCreepPowerDocument>
                {
                    ["1"] = new() { Level = 2 }
                }
            };

            var intent = new UserIntentDocument
            {
                Id = ObjectId.Parse("64c19d382c00000000000003"),
                UserId = "userA",
                Intents = new BsonDocument("attack", new BsonDocument("room", "W0N0"))
            };

            var market = new InterRoomMarketSnapshot(
                new List<MarketOrderDocument> { order },
                new List<UserDocument> { user },
                new List<PowerCreepDocument> { powerCreep },
                new List<UserIntentDocument> { intent },
                "shard0");

            var snapshot = new InterRoomSnapshot(
                gameTime,
                new List<RoomObjectDocument> { creep },
                new Dictionary<string, RoomDocument>(StringComparer.Ordinal) { ["W0N0"] = room },
                new List<RoomObjectDocument> { special },
                market);

            return Task.FromResult(snapshot);
        }
    }
}
