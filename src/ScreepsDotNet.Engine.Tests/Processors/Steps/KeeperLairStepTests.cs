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
using Xunit;

public sealed class KeeperLairStepTests
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    [Fact]
    public async Task ExecuteAsync_NoKeeper_SetsSpawnTimer()
    {
        // Arrange
        var lair = CreateKeeperLair("lair1", 25, 25);
        var state = CreateState(1000, [lair]);
        var writer = new RecordingMutationWriter();
        var step = new KeeperLairStep();

        // Act
        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink(), new NullGlobalMutationWriter(), new NullNotificationSink()), TestContext.Current.CancellationToken);

        // Assert
        var patch = Assert.Single(writer.Patches);
        Assert.Equal("lair1", patch.ObjectId);
        var payload = Assert.IsType<RoomObjectPatchPayload>(patch.Payload);
        Assert.Equal(1300, payload.NextRegenerationTime);  // gameTime (1000) + 300
    }

    [Fact]
    public async Task ExecuteAsync_KeeperExists_DoesNotSetSpawnTimer()
    {
        // Arrange
        var lair = CreateKeeperLair("lair1", 25, 25);
        var keeper = CreateKeeper("lair1", 25, 25, hits: 5000);
        var state = CreateState(1000, [lair, keeper]);
        var writer = new RecordingMutationWriter();
        var step = new KeeperLairStep();

        // Act
        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink(), new NullGlobalMutationWriter(), new NullNotificationSink()), TestContext.Current.CancellationToken);

        // Assert - no mutations (keeper healthy)
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task ExecuteAsync_KeeperWeakHits_SetsSpawnTimer()
    {
        // Arrange
        var lair = CreateKeeperLair("lair1", 25, 25);
        var keeper = CreateKeeper("lair1", 25, 25, hits: 4999);  // < 5000
        var state = CreateState(1000, [lair, keeper]);
        var writer = new RecordingMutationWriter();
        var step = new KeeperLairStep();

        // Act
        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink(), new NullGlobalMutationWriter(), new NullNotificationSink()), TestContext.Current.CancellationToken);

        // Assert
        var patch = Assert.Single(writer.Patches);
        Assert.Equal("lair1", patch.ObjectId);
        var payload = Assert.IsType<RoomObjectPatchPayload>(patch.Payload);
        Assert.Equal(1300, payload.NextRegenerationTime);  // gameTime (1000) + 300
    }

    [Fact]
    public async Task ExecuteAsync_SpawnTimerExpired_SpawnsKeeper()
    {
        // Arrange
        var lair = CreateKeeperLair("lair1", 25, 25, nextRegenerationTime: 1299);
        var state = CreateState(1299, [lair]);  // gameTime >= nextSpawnTime - 1
        var writer = new RecordingMutationWriter();
        var step = new KeeperLairStep();

        // Act
        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink(), new NullGlobalMutationWriter(), new NullNotificationSink()), TestContext.Current.CancellationToken);

        // Assert - spawns keeper + clears timer
        Assert.Equal(2, writer.Upserts.Count + writer.Patches.Count);

        var upsert = Assert.Single(writer.Upserts);
        Assert.Equal("Keeperlair1", upsert.Document.Name);
        Assert.Equal(NpcUserIds.SourceKeeper, upsert.Document.UserId);
        Assert.Equal(25, upsert.Document.X);
        Assert.Equal(25, upsert.Document.Y);
        Assert.Equal(5000, upsert.Document.Hits);
        Assert.Equal(5000, upsert.Document.HitsMax);

        var patch = Assert.Single(writer.Patches);
        Assert.Equal("lair1", patch.ObjectId);
        var payload = Assert.IsType<RoomObjectPatchPayload>(patch.Payload);
        Assert.Null(payload.NextRegenerationTime);
    }

    [Fact]
    public async Task ExecuteAsync_SpawnTimerExpired_RemovesOldKeeper()
    {
        // Arrange
        var lair = CreateKeeperLair("lair1", 25, 25, nextRegenerationTime: 1299);
        var oldKeeper = CreateKeeper("lair1", 10, 10, hits: 100);  // Old weak keeper
        var state = CreateState(1299, [lair, oldKeeper]);
        var writer = new RecordingMutationWriter();
        var step = new KeeperLairStep();

        // Act
        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink(), new NullGlobalMutationWriter(), new NullNotificationSink()), TestContext.Current.CancellationToken);

        // Assert - removes old + spawns new + clears timer
        var removal = Assert.Single(writer.Removals);
        Assert.Equal(oldKeeper.Id, removal);

        var upsert = Assert.Single(writer.Upserts);
        Assert.Equal("Keeperlair1", upsert.Document.Name);
    }

    [Fact]
    public async Task ExecuteAsync_SpawnedKeeper_HasCorrectBody()
    {
        // Arrange
        var lair = CreateKeeperLair("lair1", 25, 25, nextRegenerationTime: 1299);
        var state = CreateState(1299, [lair]);
        var writer = new RecordingMutationWriter();
        var step = new KeeperLairStep();

        // Act
        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink(), new NullGlobalMutationWriter(), new NullNotificationSink()), TestContext.Current.CancellationToken);

        // Assert - verify body composition
        var upsert = Assert.Single(writer.Upserts);
        var body = upsert.Document.Body;

        // 17 TOUGH + 13 MOVE + 10 ATTACK + 10 RANGED_ATTACK = 50 parts
        Assert.Equal(50, body.Count);

        var toughCount = body.Count(p => p.Type == BodyPartType.Tough);
        var moveCount = body.Count(p => p.Type == BodyPartType.Move);
        var attackCount = body.Count(p => p.Type == BodyPartType.Attack);
        var rangedAttackCount = body.Count(p => p.Type == BodyPartType.RangedAttack);

        Assert.Equal(17, toughCount);
        Assert.Equal(13, moveCount);
        Assert.Equal(10, attackCount);
        Assert.Equal(10, rangedAttackCount);

        // Verify all parts have 100 HP
        Assert.All(body, part => Assert.Equal(ScreepsGameConstants.BodyPartHitPoints, part.Hits));
    }

    [Fact]
    public async Task ExecuteAsync_SpawnTimerNotExpired_DoesNothing()
    {
        // Arrange
        var lair = CreateKeeperLair("lair1", 25, 25, nextRegenerationTime: 1300);
        var state = CreateState(1298, [lair]);  // gameTime < nextSpawnTime - 1
        var writer = new RecordingMutationWriter();
        var step = new KeeperLairStep();

        // Act
        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink(), new NullGlobalMutationWriter(), new NullNotificationSink()), TestContext.Current.CancellationToken);

        // Assert - no mutations
        Assert.Empty(writer.Patches);
        Assert.Empty(writer.Upserts);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleKeeperLairs_ProcessesAll()
    {
        // Arrange
        var lair1 = CreateKeeperLair("lair1", 10, 10);
        var lair2 = CreateKeeperLair("lair2", 40, 40);
        var state = CreateState(1000, [lair1, lair2]);
        var writer = new RecordingMutationWriter();
        var step = new KeeperLairStep();

        // Act
        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink(), new NullGlobalMutationWriter(), new NullNotificationSink()), TestContext.Current.CancellationToken);

        // Assert - both lairs get spawn timers
        Assert.Equal(2, writer.Patches.Count);
        Assert.Contains(writer.Patches, p => p.ObjectId == "lair1");
        Assert.Contains(writer.Patches, p => p.ObjectId == "lair2");
    }

    private static RoomObjectSnapshot CreateKeeperLair(string id, int x, int y, int? nextRegenerationTime = null)
        => new(
            id,
            RoomObjectTypes.KeeperLair,
            "W5N5",
            null,
            null,
            x,
            y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.KeeperLair,
            Store: new Dictionary<string, int>(Comparer),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(Comparer),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            NextRegenerationTime: nextRegenerationTime);

    private static RoomObjectSnapshot CreateKeeper(string lairId, int x, int y, int hits)
        => new(
            $"keeper{lairId}",
            RoomObjectTypes.Creep,
            "W5N5",
            null,
            NpcUserIds.SourceKeeper,
            x,
            y,
            Hits: hits,
            HitsMax: 5000,
            Fatigue: 0,
            TicksToLive: null,
            Name: $"Keeper{lairId}",
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(Comparer) { [ResourceTypes.Energy] = 0 },
            StoreCapacity: 0,
            StoreCapacityResource: new Dictionary<string, int>(Comparer),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body:
            [
                new(BodyPartType.Tough, ScreepsGameConstants.BodyPartHitPoints, null),
                new(BodyPartType.Move, ScreepsGameConstants.BodyPartHitPoints, null),
                new(BodyPartType.Attack, ScreepsGameConstants.BodyPartHitPoints, null),
                new(BodyPartType.RangedAttack, ScreepsGameConstants.BodyPartHitPoints, null)
            ]);

    private static RoomState CreateState(int gameTime, IReadOnlyList<RoomObjectSnapshot> objects)
    {
        var map = objects.ToDictionary(obj => obj.Id, obj => obj, Comparer);
        var roomInfo = new RoomInfoSnapshot(
            RoomName: "W5N5",
            Shard: null,
            Status: null,
            IsNoviceArea: null,
            IsRespawnArea: null,
            OpenTime: null,
            OwnerUserId: null,
            ControllerLevel: null,
            EnergyAvailable: null,
            NextNpcMarketOrder: null,
            PowerBankTime: null,
            InvaderGoal: null,
            Type: RoomType.Keeper);
        var result = new RoomState(
            "W5N5",
            gameTime,
            roomInfo,
            map,
            new Dictionary<string, UserState>(Comparer),
            null,
            new Dictionary<string, RoomTerrainSnapshot>(Comparer),
            []);
        return result;
    }

    private sealed class RecordingMutationWriter : IRoomMutationWriter
    {
        public List<RoomObjectUpsert> Upserts { get; } = [];
        public List<RoomObjectPatch> Patches { get; } = [];
        public List<string> Removals { get; } = [];

        public void Upsert(RoomObjectSnapshot document) => Upserts.Add(new RoomObjectUpsert(document));

        public void Patch(string objectId, RoomObjectPatchPayload patch)
            => Patches.Add(new RoomObjectPatch(objectId, patch));

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

        public void Reset()
        {
            Upserts.Clear();
            Patches.Clear();
            Removals.Clear();
        }
    }

    private sealed record RoomObjectUpsert(RoomObjectSnapshot Document);
    private sealed record RoomObjectPatch(string ObjectId, RoomObjectPatchPayload Payload);
}
