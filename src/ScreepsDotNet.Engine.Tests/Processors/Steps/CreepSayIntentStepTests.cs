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

public sealed class CreepSayIntentStepTests
{
    private readonly CreepSayIntentStep _step = new();

    [Fact]
    public async Task ExecuteAsync_CreepSays_SetsActionLog()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1");
        var context = CreateContext([creep], CreateSayIntents("user1", creep.Id, "hello", isPublic: true));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal("creep1", objectId);
        Assert.NotNull(payload.ActionLog);
        Assert.NotNull(payload.ActionLog.Say);
        Assert.Equal("hello", payload.ActionLog.Say.Message);
        Assert.True(payload.ActionLog.Say.IsPublic);
    }

    [Fact]
    public async Task ExecuteAsync_MessageExceeds10Chars_TruncatesTo10()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1");
        var context = CreateContext([creep], CreateSayIntents("user1", creep.Id, "this is a very long message", isPublic: false));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, payload) = Assert.Single(writer.Patches);
        Assert.NotNull(payload.ActionLog?.Say);
        Assert.Equal("this is a ", payload.ActionLog.Say.Message);
        Assert.False(payload.ActionLog.Say.IsPublic);
    }

    [Fact]
    public async Task ExecuteAsync_CreepIsSpawning_DoesNotSetActionLog()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", isSpawning: true);
        var context = CreateContext([creep], CreateSayIntents("user1", creep.Id, "hello", isPublic: true));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task ExecuteAsync_NotACreep_DoesNotSetActionLog()
    {
        var spawn = CreateSpawn("spawn1", 10, 10, "user1");
        var context = CreateContext([spawn], CreateSayIntents("user1", spawn.Id, "hello", isPublic: true));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task ExecuteAsync_NoIntents_DoesNothing()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1");
        var emptyIntents = new RoomIntentSnapshot("W1N1", null, new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal));
        var context = CreateContext([creep], emptyIntents);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyMessage_SetsEmptyActionLog()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1");
        var context = CreateContext([creep], CreateSayIntents("user1", creep.Id, "", isPublic: true));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, payload) = Assert.Single(writer.Patches);
        Assert.NotNull(payload.ActionLog?.Say);
        Assert.Equal("", payload.ActionLog.Say.Message);
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

    private static RoomObjectSnapshot CreateCreep(string id, int x, int y, string userId, bool isSpawning = false)
        => new(
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
            new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: 0,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            [],
            IsSpawning: isSpawning);

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

    private static RoomIntentSnapshot CreateSayIntents(string userId, string objectId, string message, bool isPublic)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            ["message"] = new(IntentFieldValueKind.Text, TextValue: message),
            ["isPublic"] = new(IntentFieldValueKind.Boolean, BooleanValue: isPublic)
        });

        var record = new IntentRecord(IntentKeys.Say, [argument]);

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
