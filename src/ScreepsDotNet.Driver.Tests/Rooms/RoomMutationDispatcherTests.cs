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
        public IBulkWriter<RoomFlagDocument> CreateRoomFlagsWriter() => new NullBulkWriter<RoomFlagDocument>();
        public IBulkWriter<UserDocument> CreateUsersWriter() => new NullBulkWriter<UserDocument>();
        public IBulkWriter<RoomDocument> CreateRoomsWriter() => roomsWriter;
        public IBulkWriter<BsonDocument> CreateTransactionsWriter() => new NullBulkWriter<BsonDocument>();
        public IBulkWriter<MarketOrderDocument> CreateMarketOrdersWriter() => new NullBulkWriter<MarketOrderDocument>();
        public IBulkWriter<MarketOrderDocument> CreateMarketIntershardOrdersWriter() => new NullBulkWriter<MarketOrderDocument>();
        public IBulkWriter<UserMoneyEntryDocument> CreateUsersMoneyWriter() => new NullBulkWriter<UserMoneyEntryDocument>();
        public IBulkWriter<BsonDocument> CreateUsersResourcesWriter() => new NullBulkWriter<BsonDocument>();
        public IBulkWriter<PowerCreepDocument> CreateUsersPowerCreepsWriter() => new NullBulkWriter<PowerCreepDocument>();
    }

    private sealed class FakeBulkWriter<T> : IBulkWriter<T>
        where T : class
    {
        public bool ExecuteCalled { get; private set; }
        public List<string> Removals { get; } = new();
        public List<(string Id, string DeltaJson)> Updates { get; } = new();

        public bool HasPendingOperations => Removals.Count > 0 || Updates.Count > 0;

        public void Insert(T entity, string? id = null)
        {
            // Inserts aren’t exercised in these tests; no-op keeps behavior predictable.
        }

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

        public void Update(T entity, object delta) => throw new NotImplementedException();

        public void Remove(string id) => Removals.Add(id);
        public void Remove(T entity) => throw new NotImplementedException();
        public void Increment(string id, string field, long amount) => throw new NotImplementedException();
        public void AddToSet(string id, string field, object value) => throw new NotImplementedException();
        public void Pull(string id, string field, object value) => throw new NotImplementedException();

        public Task ExecuteAsync(CancellationToken token = default)
        {
            ExecuteCalled = true;
            return Task.CompletedTask;
        }

        public void Clear() => throw new NotImplementedException();
    }

    private sealed class NullBulkWriter<T> : IBulkWriter<T>
        where T : class
    {
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

        public Task<IReadOnlyList<string>> DrainActiveRoomsAsync(CancellationToken token = default) => throw new NotImplementedException();
        public Task ActivateRoomsAsync(IEnumerable<string> roomNames, CancellationToken token = default) => throw new NotImplementedException();
        public Task<RoomObjectsPayload> GetRoomObjectsAsync(string roomName, CancellationToken token = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<RoomFlagDocument>> GetRoomFlagsAsync(string roomName, CancellationToken token = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<string, RoomTerrainDocument>> GetRoomTerrainAsync(string roomName, CancellationToken token = default) => throw new NotImplementedException();
        public Task<RoomDocument?> GetRoomInfoAsync(string roomName, CancellationToken token = default) => throw new NotImplementedException();
        public Task SaveRoomInfoAsync(RoomDocument room, CancellationToken token = default) => throw new NotImplementedException();
        public Task SetRoomStatusAsync(string roomName, string status, CancellationToken token = default) => throw new NotImplementedException();
        public Task<RoomIntentDocument?> GetRoomIntentsAsync(string roomName, CancellationToken token = default) => throw new NotImplementedException();
        public Task ClearRoomIntentsAsync(string roomName, CancellationToken token = default) => throw new NotImplementedException();

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

        public Task UpdateAccessibleRoomsListAsync(CancellationToken token = default) => throw new NotImplementedException();
        public Task UpdateRoomStatusDataAsync(CancellationToken token = default) => throw new NotImplementedException();
        public Task<InterRoomSnapshot> GetInterRoomSnapshotAsync(CancellationToken token = default) => throw new NotImplementedException();
    }
}
