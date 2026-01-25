namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;
using ScreepsDotNet.Engine.Tests.Processors.Helpers;

public sealed class CreepSuicideIntentStepTests
{
    private readonly ICreepDeathProcessor _deathProcessor = new CreepDeathProcessor();
    private readonly CreepSuicideIntentStep _step;

    public CreepSuicideIntentStepTests() => _step = new CreepSuicideIntentStep(_deathProcessor);

    [Fact]
    public async Task ExecuteAsync_CreepSuicides_RemovesCreep()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1");
        var context = CreateContext([creep], CreateSuicideIntents("user1", creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var removal = Assert.Single(writer.Removals);
        Assert.Equal("creep1", removal);
    }

    [Fact]
    public async Task ExecuteAsync_CreepSuicides_CreatesTombstone()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1");
        var context = CreateContext([creep], CreateSuicideIntents("user1", creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var tombstone = Assert.Single(writer.Upserts);
        Assert.Equal(RoomObjectTypes.Tombstone, tombstone.Type);
        Assert.Equal(10, tombstone.X);
        Assert.Equal(10, tombstone.Y);
        Assert.Equal("creep1", tombstone.CreepId);
    }

    [Fact]
    public async Task ExecuteAsync_CreepIsSpawning_DoesNotSuicide()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", isSpawning: true);
        var context = CreateContext([creep], CreateSuicideIntents("user1", creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Removals);
        Assert.Empty(writer.Upserts);
    }

    [Fact]
    public async Task ExecuteAsync_NotACreep_DoesNotSuicide()
    {
        var spawn = CreateSpawn("spawn1", 10, 10, "user1");
        var context = CreateContext([spawn], CreateSuicideIntents("user1", spawn.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Removals);
        Assert.Empty(writer.Upserts);
    }

    [Fact]
    public async Task ExecuteAsync_NoIntents_DoesNothing()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1");
        var emptyIntents = new RoomIntentSnapshot("W1N1", null, new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal));
        var context = CreateContext([creep], emptyIntents);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Removals);
        Assert.Empty(writer.Upserts);
    }

    [Fact]
    public async Task ExecuteAsync_CreepWithStore_TombstoneHasStore()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", storeEnergy: 100);
        var context = CreateContext([creep], CreateSuicideIntents("user1", creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var tombstone = Assert.Single(writer.Upserts);
        // Suicide doesn't drop body part resources (0% drop rate), but store is transferred
        Assert.Single(tombstone.Store);
        Assert.Equal(100, tombstone.Store[ResourceTypes.Energy]);
    }

    [Fact]
    public async Task ExecuteAsync_CreepWithBody_TombstoneHasBody()
    {
        var body = new List<CreepBodyPartSnapshot>
        {
            new(BodyPartType.Move, 100, null),
            new(BodyPartType.Work, 100, null),
            new(BodyPartType.Carry, 100, null)
        };
        var creep = CreateCreep("creep1", 10, 10, "user1", body: body);
        var context = CreateContext([creep], CreateSuicideIntents("user1", creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var tombstone = Assert.Single(writer.Upserts);
        Assert.Equal(3, tombstone.Body.Count);
        Assert.Contains(tombstone.Body, p => p.Type == BodyPartType.Move);
        Assert.Contains(tombstone.Body, p => p.Type == BodyPartType.Work);
        Assert.Contains(tombstone.Body, p => p.Type == BodyPartType.Carry);
    }

    private static RoomProcessorContext CreateContext(IEnumerable<RoomObjectSnapshot> objects, RoomIntentSnapshot intents)
    {
        var objectMap = objects.ToDictionary(o => o.Id, o => o, StringComparer.Ordinal);

        var state = new RoomState(
            "W1N1",
            100,
            null,
            objectMap,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            intents,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);

        return new RoomProcessorContext(state, new FakeMutationWriter(), new NullCreepStatsSink(), new NullGlobalMutationWriter(), new NullNotificationSink());
    }

    private static RoomObjectSnapshot CreateCreep(string id, int x, int y, string userId, bool isSpawning = false, int storeEnergy = 0, IReadOnlyList<CreepBodyPartSnapshot>? body = null)
    {
        var defaultBody = new List<CreepBodyPartSnapshot>
        {
            new(BodyPartType.Move, 100, null),
            new(BodyPartType.Work, 100, null)
        };

        var store = storeEnergy > 0
            ? new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = storeEnergy }
            : new Dictionary<string, int>(StringComparer.Ordinal);

        return new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Creep,
            "W1N1",
            "shard0",
            userId,
            x,
            y,
            100,
            100,
            0,
            1500,
            Name: "TestCreep",
            Level: null,
            null,
            null,
            null,
            null,
            store,
            StoreCapacity: storeEnergy > 0 ? 100 : 0,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            body ?? defaultBody,
            IsSpawning: isSpawning);
    }

    private static RoomObjectSnapshot CreateSpawn(string id, int x, int y, string userId)
        => new(
            id,
            RoomObjectTypes.Spawn,
            "W1N1",
            "shard0",
            userId,
            x,
            y,
            5000,
            5000,
            Fatigue: null,
            TicksToLive: null,
            Name: "Spawn1",
            Level: null,
            null,
            null,
            null,
            null,
            new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: 300,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            []);

    private static RoomIntentSnapshot CreateSuicideIntents(string userId, string objectId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal));
        var record = new IntentRecord(IntentKeys.Suicide, [argument]);

        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [objectId] = [record]
        };

        var envelope = new IntentEnvelope(
            userId,
            objectIntents,
            new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal),
            null);

        var users = new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
        {
            [userId] = envelope
        };

        return new RoomIntentSnapshot("W1N1", null, users);
    }

    private sealed class FakeMutationWriter : IRoomMutationWriter
    {
        public List<(string ObjectId, RoomObjectPatchPayload Payload)> Patches { get; } = [];
        public List<RoomObjectSnapshot> Upserts { get; } = [];
        public List<string> Removals { get; } = [];

        public void Upsert(RoomObjectSnapshot document) => Upserts.Add(document);

        public void Patch(string objectId, RoomObjectPatchPayload patch) => Patches.Add((objectId, patch));

        public void Remove(string objectId) => Removals.Add(objectId);

        public void SetRoomInfoPatch(RoomInfoPatchPayload patch) { }

        public void SetEventLog(IRoomEventLogPayload? eventLog) { }

        public void SetMapView(IRoomMapViewPayload? mapView) { }

#pragma warning disable CA1822 // Mark members as static
        public int GetMutationCount() => 0;
#pragma warning restore CA1822

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

#pragma warning disable CA1822 // Method cannot be static as it implements interface member
        public bool TryGetPendingPatch(string objectId, out RoomObjectPatchPayload patch) { patch = new RoomObjectPatchPayload(); return false; }
#pragma warning restore CA1822

        public void Reset()
        {
            Patches.Clear();
            Upserts.Clear();
            Removals.Clear();
        }
    }
}
