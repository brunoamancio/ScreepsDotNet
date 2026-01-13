using MongoDB.Bson;
using ScreepsDotNet.Driver.Abstractions.Bulk;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Driver.Services.Rooms;
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
        var dispatcher = new RoomMutationDispatcher(factory, roomService);

        var batch = new RoomMutationBatch(
            RoomName: "W0N0",
            ObjectUpserts: Array.Empty<RoomObjectUpsert>(),
            ObjectPatches: [new RoomObjectPatch("obj1", """{ "hits": 100 }""")],
            ObjectDeletes: ["obj2"],
            RoomInfoPatch: null,
            MapViewJson: """{ "view": true }""",
            EventLogJson: """{ "events": [] }""");

        await dispatcher.ApplyAsync(batch, CancellationToken.None);

        Assert.True(objectsWriter.ExecuteCalled);
        Assert.Collection(objectsWriter.Updates,
            update =>
            {
                Assert.Equal("obj1", update.Id);
                var doc = BsonDocument.Parse(update.DeltaJson);
                Assert.Equal(100, doc["hits"].AsInt32);
            });
        Assert.Contains("obj2", objectsWriter.Removals);
        Assert.Equal("W0N0:{ \"events\": [] }", roomService.EventLog);
        Assert.Equal("W0N0:{ \"view\": true }", roomService.MapView);
        Assert.False(roomsWriter.ExecuteCalled);
    }

    [Fact]
    public async Task ApplyAsync_WithRoomInfoPatch_WritesRoomDocument()
    {
        var objectsWriter = new FakeBulkWriter<RoomObjectDocument>();
        var roomsWriter = new FakeBulkWriter<RoomDocument>();
        var factory = new FakeBulkWriterFactory(objectsWriter, roomsWriter);
        var roomService = new StubRoomDataService();
        var dispatcher = new RoomMutationDispatcher(factory, roomService);

        var batch = new RoomMutationBatch(
            RoomName: "W1N1",
            ObjectUpserts: Array.Empty<RoomObjectUpsert>(),
            ObjectPatches: Array.Empty<RoomObjectPatch>(),
            ObjectDeletes: Array.Empty<string>(),
            RoomInfoPatch: new RoomInfoPatch("""{ "status": "normal" }"""),
            MapViewJson: null,
            EventLogJson: null);

        await dispatcher.ApplyAsync(batch, CancellationToken.None);

        Assert.True(roomsWriter.ExecuteCalled);
        Assert.Collection(roomsWriter.Updates,
            update =>
            {
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
        public List<string> Removals { get; } = new();
        public List<(string Id, string DeltaJson)> Updates { get; } = new();

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

    private sealed class StubRoomDataService : IRoomDataService
    {
        public string? EventLog { get; private set; }
        public string? MapView { get; private set; }

        public Task<IReadOnlyList<string>> DrainActiveRoomsAsync(CancellationToken token = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task ActivateRoomsAsync(IEnumerable<string> roomNames, CancellationToken token = default)
            => Task.CompletedTask;

        public Task<RoomObjectsPayload> GetRoomObjectsAsync(string roomName, CancellationToken token = default)
            => Task.FromResult(new RoomObjectsPayload(new Dictionary<string, RoomObjectDocument>(), new Dictionary<string, UserDocument>()));

        public Task<IReadOnlyList<RoomFlagDocument>> GetRoomFlagsAsync(string roomName, CancellationToken token = default)
            => Task.FromResult<IReadOnlyList<RoomFlagDocument>>(Array.Empty<RoomFlagDocument>());

        public Task<IReadOnlyDictionary<string, RoomTerrainDocument>> GetRoomTerrainAsync(string roomName, CancellationToken token = default)
            => Task.FromResult<IReadOnlyDictionary<string, RoomTerrainDocument>>(new Dictionary<string, RoomTerrainDocument>());

        public Task<RoomDocument?> GetRoomInfoAsync(string roomName, CancellationToken token = default)
            => Task.FromResult<RoomDocument?>(null);

        public Task SaveRoomInfoAsync(RoomDocument room, CancellationToken token = default)
            => Task.CompletedTask;

        public Task SetRoomStatusAsync(string roomName, string status, CancellationToken token = default)
            => Task.CompletedTask;

        public Task<RoomIntentDocument?> GetRoomIntentsAsync(string roomName, CancellationToken token = default)
            => Task.FromResult<RoomIntentDocument?>(null);

        public Task ClearRoomIntentsAsync(string roomName, CancellationToken token = default)
            => Task.CompletedTask;
        public Task SaveRoomEventLogAsync(string roomName, string eventLogJson, CancellationToken token = default)
        {
            EventLog = $"{roomName}:{eventLogJson}";
            return Task.CompletedTask;
        }

        public Task SaveMapViewAsync(string roomName, string mapViewJson, CancellationToken token = default)
        {
            MapView = $"{roomName}:{mapViewJson}";
            return Task.CompletedTask;
        }

        public Task UpdateAccessibleRoomsListAsync(CancellationToken token = default) => Task.CompletedTask;

        public Task UpdateRoomStatusDataAsync(CancellationToken token = default) => Task.CompletedTask;

        public Task<InterRoomSnapshot> GetInterRoomSnapshotAsync(CancellationToken token = default)
            => Task.FromResult(new InterRoomSnapshot(0, Array.Empty<RoomObjectDocument>(),
                new Dictionary<string, RoomDocument>(), Array.Empty<RoomObjectDocument>(),
                new InterRoomMarketSnapshot(Array.Empty<MarketOrderDocument>(), Array.Empty<UserDocument>(),
                    Array.Empty<PowerCreepDocument>(), Array.Empty<UserIntentDocument>(), string.Empty)));
    }
}
