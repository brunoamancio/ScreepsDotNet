namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;
using ScreepsDotNet.Engine.Tests.Processors.Helpers;

/// <summary>
/// Tests for RampartIntentStep processing rampart setPublic intents.
/// </summary>
public sealed class RampartIntentStepTests
{
    private readonly RampartIntentStep _step = new();

    [Fact]
    public async Task ExecuteAsync_SetPublicTrue_SetsIsPublicTrue()
    {
        // Arrange
        var rampart = CreateRampart("rampart1", "user1", x: 25, y: 25, isPublic: false);
        var context = CreateContext(new[] { rampart }, CreateSetPublicIntent("user1", rampart.Id, isPublic: true));
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (ObjectId, Payload) = Assert.Single(writer.Patches);
        Assert.Equal(rampart.Id, ObjectId);
        Assert.True(Payload.IsPublic);
    }

    [Fact]
    public async Task ExecuteAsync_SetPublicFalse_SetsIsPublicFalse()
    {
        // Arrange
        var rampart = CreateRampart("rampart1", "user1", x: 25, y: 25, isPublic: true);
        var context = CreateContext(new[] { rampart }, CreateSetPublicIntent("user1", rampart.Id, isPublic: false));
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (ObjectId, Payload) = Assert.Single(writer.Patches);
        Assert.Equal(rampart.Id, ObjectId);
        Assert.False(Payload.IsPublic);
    }

    [Fact]
    public async Task ExecuteAsync_NotOwner_NoPatches()
    {
        // Arrange
        var rampart = CreateRampart("rampart1", "user1", x: 25, y: 25, isPublic: false);
        var context = CreateContext(new[] { rampart }, CreateSetPublicIntent("user2", rampart.Id, isPublic: true));
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - no changes since user2 doesn't own the rampart
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task ExecuteAsync_ObjectNotRampart_NoPatches()
    {
        // Arrange
        var tower = CreateTower("tower1", "user1", x: 25, y: 25);
        var context = CreateContext(new[] { tower }, CreateSetPublicIntent("user1", tower.Id, isPublic: true));
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - no changes since it's not a rampart
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task ExecuteAsync_RampartNotFound_NoPatches()
    {
        // Arrange
        var rampart = CreateRampart("rampart1", "user1", x: 25, y: 25, isPublic: false);
        var context = CreateContext(new[] { rampart }, CreateSetPublicIntent("user1", "nonexistent", isPublic: true));
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task ExecuteAsync_NoIntents_NoPatches()
    {
        // Arrange
        var rampart = CreateRampart("rampart1", "user1", x: 25, y: 25, isPublic: false);
        var context = CreateContext(new[] { rampart }, intents: null);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleSetPublicIntentsSameRampart_AppliesAll()
    {
        // Arrange
        var rampart = CreateRampart("rampart1", "user1", x: 25, y: 25, isPublic: false);

        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [rampart.Id] =
            [
                CreateSetPublicRecord(isPublic: true),
                CreateSetPublicRecord(isPublic: false),
                CreateSetPublicRecord(isPublic: true)
            ]
        };

        var envelope = new IntentEnvelope(
            "user1",
            objectIntents,
            new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal),
            null);

        var users = new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
        {
            ["user1"] = envelope
        };

        var intents = new RoomIntentSnapshot("W1N1", null, users);
        var context = CreateContext(new[] { rampart }, intents);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - should apply all intents
        Assert.Equal(3, writer.Patches.Count);
        Assert.True(writer.Patches[2].Payload.IsPublic); // Last one should be true
    }

    // Helper methods

    private static RoomObjectSnapshot CreateRampart(string id, string userId, int x, int y, bool? isPublic = null)
        => new(
            Id: id,
            Type: RoomObjectTypes.Rampart,
            RoomName: "W1N1",
            Shard: null,
            UserId: userId,
            X: x,
            Y: y,
            Hits: 1000,
            HitsMax: 1000000,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Rampart,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<Common.Types.PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            Spawning: null,
            IsSpawning: null,
            UserSummoned: null,
            IsPublic: isPublic,
            NotifyWhenAttacked: null,
            StrongholdId: null,
            DeathTime: null,
            DecayTime: null,
            CreepId: null,
            CreepName: null,
            CreepTicksToLive: null,
            CreepSaying: null,
            ResourceType: null,
            ResourceAmount: null,
            Progress: null,
            ProgressTotal: null,
            ActionLog: null,
            Energy: null,
            MineralAmount: null,
            InvaderHarvested: null,
            Harvested: null,
            Cooldown: null,
            CooldownTime: null,
            NextRegenerationTime: null,
            SafeMode: null,
            SafeModeAvailable: null,
            PortalDestination: null,
            Send: null,
            Powers: null,
            MemorySourceId: null,
            MemoryMove: null);

    private static RoomObjectSnapshot CreateTower(string id, string userId, int x, int y)
        => new(
            Id: id,
            Type: RoomObjectTypes.Tower,
            RoomName: "W1N1",
            Shard: null,
            UserId: userId,
            X: x,
            Y: y,
            Hits: 3000,
            HitsMax: 3000,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Tower,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<Common.Types.PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            Spawning: null,
            IsSpawning: null,
            UserSummoned: null,
            IsPublic: null,
            NotifyWhenAttacked: null,
            StrongholdId: null,
            DeathTime: null,
            DecayTime: null,
            CreepId: null,
            CreepName: null,
            CreepTicksToLive: null,
            CreepSaying: null,
            ResourceType: null,
            ResourceAmount: null,
            Progress: null,
            ProgressTotal: null,
            ActionLog: null,
            Energy: null,
            MineralAmount: null,
            InvaderHarvested: null,
            Harvested: null,
            Cooldown: null,
            CooldownTime: null,
            NextRegenerationTime: null,
            SafeMode: null,
            SafeModeAvailable: null,
            PortalDestination: null,
            Send: null,
            Powers: null,
            MemorySourceId: null,
            MemoryMove: null);

    private static RoomProcessorContext CreateContext(IEnumerable<RoomObjectSnapshot> objects, RoomIntentSnapshot? intents = null)
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

    private static RoomIntentSnapshot CreateSetPublicIntent(string userId, string objectId, bool isPublic)
    {
        var record = CreateSetPublicRecord(isPublic);

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

    private static IntentRecord CreateSetPublicRecord(bool isPublic)
    {
        var fields = new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.IsPublic] = new(IntentFieldValueKind.Number, NumberValue: isPublic ? 1 : 0)
        };

        var argument = new IntentArgument(fields);
        return new IntentRecord(IntentKeys.SetPublic, [argument]);
    }

    // Fake test helpers

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
