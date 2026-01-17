using System.Text.Json;
using MongoDB.Bson;
using ScreepsDotNet.Driver.Abstractions.Bulk;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Driver.Services.Rooms;
using ScreepsDotNet.Driver.Tests.TestDoubles;
using ScreepsDotNet.Driver.Tests.TestSupport;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Tests.Rooms;

public sealed class RoomMutationDispatcherTests
{
    [Fact]
    public async Task ApplyAsync_PatchesAndRemovals_InvokesWritersAndSavesLogs()
    {
        var objectsWriter = new FakeBulkWriter<RoomObjectDocument>();
        var roomsWriter = new FakeBulkWriter<RoomDocument>();
        var factory = new FakeBulkWriterFactory(objectsWriter, roomsWriter);
        var roomService = new StubRoomDataService();
        var environment = new FakeEnvironmentService();
        var dispatcher = new RoomMutationDispatcher(factory, roomService, environment, new PassThroughBlueprintEnricher());

        var batch = new RoomMutationBatch(
            RoomName: "W0N0",
            ObjectUpserts: [],
            ObjectPatches: [new RoomObjectPatch("obj1", new RoomObjectPatchPayload { Hits = 100 })],
            ObjectDeletes: ["obj2"],
            RoomInfoPatch: null,
            MapView: new RoomIntentMapView("W0N0", 1, []),
            EventLog: new RoomIntentEventLog("W0N0", 123, []));

        await dispatcher.ApplyAsync(batch, CancellationToken.None);

        Assert.True(objectsWriter.ExecuteCalled);
        Assert.Collection(objectsWriter.Updates,
            update => {
                Assert.Equal("obj1", update.Id);
                var doc = BsonDocument.Parse(update.DeltaJson);
                Assert.Equal(100, doc["hits"].AsInt32);
            });
        Assert.Contains("obj2", objectsWriter.Removals);
        Assert.NotNull(roomService.EventLog);
        Assert.NotNull(roomService.MapView);

        var eventLogJson = roomService.EventLog!.Split(':', 2)[1];
        using (var document = JsonDocument.Parse(eventLogJson)) {
            Assert.Equal("W0N0", document.RootElement.GetProperty("room").GetString());
            Assert.Equal(123, document.RootElement.GetProperty("tick").GetInt32());
        }

        var mapViewJson = roomService.MapView!.Split(':', 2)[1];
        using (var document = JsonDocument.Parse(mapViewJson)) {
            Assert.Equal("W0N0", document.RootElement.GetProperty("room").GetString());
            Assert.Equal(1, document.RootElement.GetProperty("timestamp").GetInt64());
        }
        Assert.False(roomsWriter.ExecuteCalled);
    }

    [Fact]
    public async Task ApplyAsync_WithRoomInfoPatch_WritesRoomDocument()
    {
        var objectsWriter = new FakeBulkWriter<RoomObjectDocument>();
        var roomsWriter = new FakeBulkWriter<RoomDocument>();
        var factory = new FakeBulkWriterFactory(objectsWriter, roomsWriter);
        var roomService = new StubRoomDataService();
        var environment = new FakeEnvironmentService();
        var dispatcher = new RoomMutationDispatcher(factory, roomService, environment, new PassThroughBlueprintEnricher());

        var batch = new RoomMutationBatch(
            RoomName: "W1N1",
            ObjectUpserts: [],
            ObjectPatches: [],
            ObjectDeletes: [],
            RoomInfoPatch: new RoomInfoPatchPayload { Status = "normal" },
            MapView: null,
            EventLog: null);

        await dispatcher.ApplyAsync(batch, CancellationToken.None);

        Assert.True(roomsWriter.ExecuteCalled);
        Assert.Collection(roomsWriter.Updates,
            update => {
                Assert.Equal("W1N1", update.Id);
                var doc = BsonDocument.Parse(update.DeltaJson);
                Assert.Equal("normal", doc["status"].AsString);
            });
        Assert.False(objectsWriter.ExecuteCalled);
    }

    private sealed class FakeBulkWriterFactory(FakeBulkWriter<RoomObjectDocument> objectsWriter, FakeBulkWriter<RoomDocument> roomsWriter)
        : IBulkWriterFactory
    {
        public IBulkWriter<RoomObjectDocument> CreateRoomObjectsWriter() => objectsWriter;
        public IBulkWriter<RoomFlagDocument> CreateRoomFlagsWriter() => NullBulkWriter<RoomFlagDocument>.Instance;
        public IBulkWriter<UserDocument> CreateUsersWriter() => NullBulkWriter<UserDocument>.Instance;
        public IBulkWriter<RoomDocument> CreateRoomsWriter() => roomsWriter;
        public IBulkWriter<BsonDocument> CreateTransactionsWriter() => NullBulkWriter<BsonDocument>.Instance;
        public IBulkWriter<MarketOrderDocument> CreateMarketOrdersWriter() => NullBulkWriter<MarketOrderDocument>.Instance;
        public IBulkWriter<MarketOrderDocument> CreateMarketIntershardOrdersWriter() => NullBulkWriter<MarketOrderDocument>.Instance;
        public IBulkWriter<UserMoneyEntryDocument> CreateUsersMoneyWriter() => NullBulkWriter<UserMoneyEntryDocument>.Instance;
        public IBulkWriter<BsonDocument> CreateUsersResourcesWriter() => NullBulkWriter<BsonDocument>.Instance;
        public IBulkWriter<PowerCreepDocument> CreateUsersPowerCreepsWriter() => NullBulkWriter<PowerCreepDocument>.Instance;
    }

    private sealed class FakeBulkWriter<T> : IBulkWriter<T>
        where T : class
    {
        public bool ExecuteCalled { get; private set; }
        public List<string> Removals { get; } = [];
        public List<(string Id, string DeltaJson)> Updates { get; } = [];

        public bool HasPendingOperations => Removals.Count > 0 || Updates.Count > 0;

        public void Insert(T entity, string? id = null) { }

        public void Update(string id, object delta)
        {
            var document = delta switch
            {
                string s => BsonDocument.Parse(s),
                BsonDocument doc => doc,
                _ => delta.ToBsonDocument()
            };
            var json = document.ToJson();
            Updates.Add((id, json));
        }

        public void Update(T entity, object delta) => Update(string.Empty, delta);

        public void Remove(string id) => Removals.Add(id);
        public void Remove(T entity) { }
        public void Increment(string id, string field, long amount) { }
        public void AddToSet(string id, string field, object value) { }
        public void Pull(string id, string field, object value) { }

        public Task ExecuteAsync(CancellationToken token = default)
        {
            ExecuteCalled = true;
            return Task.CompletedTask;
        }

        public void Clear()
        {
            Removals.Clear();
            Updates.Clear();
            ExecuteCalled = false;
        }
    }

    private sealed class NullBulkWriter<T> : IBulkWriter<T>
        where T : class
    {
        public static NullBulkWriter<T> Instance { get; } = new();

        public bool HasPendingOperations => false;
        public void Insert(T entity, string? id = null) { }
        public void Update(string id, object delta) { }
        public void Update(T entity, object delta) { }
        public void Remove(string id) { }
        public void Remove(T entity) { }
        public void Increment(string id, string field, long amount) { }
        public void AddToSet(string id, string field, object value) { }
        public void Pull(string id, string field, object value) { }
        public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
        public void Clear() { }
    }

    private sealed class StubRoomDataService : RoomDataServiceDouble
    {
        public string? EventLog { get; private set; }
        public string? MapView { get; private set; }

        public override Task SaveRoomEventLogAsync(string roomName, string eventLogJson, CancellationToken token = default)
        {
            EventLog = $"{roomName}:{eventLogJson}";
            return Task.CompletedTask;
        }

        public override Task SaveMapViewAsync(string roomName, string mapViewJson, CancellationToken token = default)
        {
            MapView = $"{roomName}:{mapViewJson}";
            return Task.CompletedTask;
        }
    }

    private sealed class PassThroughBlueprintEnricher : IRoomObjectBlueprintEnricher
    {
        public RoomObjectSnapshot Enrich(RoomObjectSnapshot snapshot, int? gameTime = null) => snapshot;
    }
}
