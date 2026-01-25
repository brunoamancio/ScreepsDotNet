namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Extensions;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;
using ScreepsDotNet.Engine.Tests.Processors.Helpers;
using Xunit;

public sealed class SpawnSpawningStepTests
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    [Fact]
    public async Task ExecuteAsync_CompletesSpawn_WhenOpenTileExists()
    {
        var spawn = CreateSpawn("spawn1", 10, 10, new RoomSpawnSpawningSnapshot("worker1", 9, 101, []));
        var placeholder = CreateCreep("creep1", "worker1", "user1", spawn.X, spawn.Y, spawning: true);

        var state = CreateState(101, [spawn, placeholder]);
        var writer = new RecordingMutationWriter();
        var death = new RecordingDeathProcessor();
        var step = new SpawnSpawningStep(new SpawnStateReader(), death);

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink(), new NullGlobalMutationWriter(), new NullNotificationSink()), TestContext.Current.CancellationToken);

        var upsert = Assert.Single(writer.Upserts);
        Assert.Equal("creep1", upsert.Document.Id);
        Assert.Equal(10, upsert.Document.X); // Direction.Top => x unchanged
        Assert.Equal(9, upsert.Document.Y);
        Assert.False(upsert.Document.IsSpawning);

        var patch = Assert.Single(writer.Patches);
        Assert.Equal("spawn1", patch.ObjectId);
        var payload = Assert.IsType<RoomObjectPatchPayload>(patch.Payload);
        Assert.True(payload.ClearSpawning);
    }

    [Fact]
    public async Task ExecuteAsync_DelaysSpawn_WhenAllTilesBlocked()
    {
        var spawning = new RoomSpawnSpawningSnapshot("worker1", 9, 200, []);
        var spawn = CreateSpawn("spawn1", 10, 10, spawning);
        var placeholder = CreateCreep("creep1", "worker1", "user1", spawn.X, spawn.Y, spawning: true);
        var blockers = CreateRingOfWalls(spawn);

        var state = CreateState(200, [spawn, placeholder, .. blockers]);
        var writer = new RecordingMutationWriter();
        var step = new SpawnSpawningStep(new SpawnStateReader(), new RecordingDeathProcessor());

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink(), new NullGlobalMutationWriter(), new NullNotificationSink()), TestContext.Current.CancellationToken);

        Assert.Empty(writer.Upserts);
        var patch = Assert.Single(writer.Patches);
        Assert.Equal("spawn1", patch.ObjectId);
        var payload = Assert.IsType<RoomObjectPatchPayload>(patch.Payload);
        Assert.False(payload.ClearSpawning);
        Assert.NotNull(payload.Spawning);
        Assert.Equal(201, payload.Spawning!.SpawnTime);
    }

    [Fact]
    public async Task ExecuteAsync_PerformsSpawnStomp_WhenOnlyHostileBlocks()
    {
        var spawn = CreateSpawn("spawn1", 10, 10, new RoomSpawnSpawningSnapshot("worker1", 9, 300, []));
        var placeholder = CreateCreep("creep1", "worker1", "user1", spawn.X, spawn.Y, spawning: true);
        var hostile = CreateCreep("hostile", "hostile", "user2", spawn.X, spawn.Y - 1);
        var blockers = CreateRingOfWalls(spawn, skip: hostile);

        var state = CreateState(300, [spawn, placeholder, hostile, .. blockers]);
        var writer = new RecordingMutationWriter();
        var death = new RecordingDeathProcessor();
        var step = new SpawnSpawningStep(new SpawnStateReader(), death);

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink(), new NullGlobalMutationWriter(), new NullNotificationSink()), TestContext.Current.CancellationToken);

        Assert.Single(death.Creeps);
        Assert.Equal("hostile", death.Creeps[0].Id);

        var upsert = Assert.Single(writer.Upserts);
        Assert.Equal("creep1", upsert.Document.Id);
        Assert.Equal(hostile.X, upsert.Document.X);
        Assert.Equal(hostile.Y, upsert.Document.Y);

        var patch = Assert.Single(writer.Patches);
        var payload = Assert.IsType<RoomObjectPatchPayload>(patch.Payload);
        Assert.True(payload.ClearSpawning);
    }

    private static RoomObjectSnapshot CreateSpawn(string id, int x, int y, RoomSpawnSpawningSnapshot? spawning = null)
        => new(
            id,
            RoomObjectTypes.Spawn,
            "W1N1",
            null,
            "user1",
            x,
            y,
            Hits: ScreepsGameConstants.SpawnHits,
            HitsMax: ScreepsGameConstants.SpawnHits,
            Fatigue: null,
            TicksToLive: null,
            Name: id,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Spawn,
            Store: new Dictionary<string, int>(Comparer),
            StoreCapacity: ScreepsGameConstants.SpawnEnergyCapacity,
            StoreCapacityResource: new Dictionary<string, int>(Comparer),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: spawning,
            Body: []);

    private static RoomObjectSnapshot CreateCreep(
        string id,
        string name,
        string userId,
        int x,
        int y,
        bool spawning = false)
        => new(
            id,
            RoomObjectTypes.Creep,
            "W1N1",
            null,
            userId,
            x,
            y,
            Hits: 100,
            HitsMax: 100,
            Fatigue: 0,
            TicksToLive: spawning ? null : 1000,
            Name: name,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(Comparer),
            StoreCapacity: ScreepsGameConstants.CarryCapacity,
            StoreCapacityResource: new Dictionary<string, int>(Comparer),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body:
            [
                new(BodyPartType.Move, ScreepsGameConstants.BodyPartHitPoints, null)
            ],
            IsSpawning: spawning);

    private static IReadOnlyList<RoomObjectSnapshot> CreateRingOfWalls(RoomObjectSnapshot spawn, RoomObjectSnapshot? skip = null)
    {
        var walls = new List<RoomObjectSnapshot>();
        foreach (var (X, Y) in DirectionExtensions.GetAllOffsets().Values) {
            var targetX = spawn.X + X;
            var targetY = spawn.Y + Y;
            if (skip is not null && skip.X == targetX && skip.Y == targetY)
                continue;

            walls.Add(new RoomObjectSnapshot(
                $"wall{targetX}_{targetY}",
                RoomObjectTypes.ConstructedWall,
                spawn.RoomName,
                spawn.Shard,
                spawn.UserId,
                targetX,
                targetY,
                Hits: 100,
                HitsMax: 100,
                Fatigue: null,
                TicksToLive: null,
                Name: null,
                Level: null,
                Density: null,
                MineralType: null,
                DepositType: null,
                StructureType: RoomObjectTypes.ConstructedWall,
                Store: new Dictionary<string, int>(Comparer),
                StoreCapacity: null,
                StoreCapacityResource: new Dictionary<string, int>(Comparer),
                Reservation: null,
                Sign: null,
                Structure: null,
                Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
                Spawning: null,
                Body: []));
        }

        return walls;
    }

    private static RoomState CreateState(int gameTime, IReadOnlyList<RoomObjectSnapshot> objects)
    {
        var map = objects.ToDictionary(obj => obj.Id, obj => obj, Comparer);
        return new RoomState(
            "W1N1",
            gameTime,
            null,
            map,
            new Dictionary<string, UserState>(Comparer),
            null,
            new Dictionary<string, RoomTerrainSnapshot>(Comparer),
            []);
    }

    private sealed class RecordingMutationWriter : IRoomMutationWriter
    {
        public List<RoomObjectUpsert> Upserts { get; } = [];
        public List<RoomObjectPatch> Patches { get; } = [];

        public void Upsert(RoomObjectSnapshot document) => Upserts.Add(new RoomObjectUpsert(document));

        public void Patch(string objectId, RoomObjectPatchPayload patch)
            => Patches.Add(new RoomObjectPatch(objectId, patch));

        public void Remove(string objectId) { }
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
        }
    }

    private sealed class RecordingDeathProcessor : ICreepDeathProcessor
    {
        public List<RoomObjectSnapshot> Creeps { get; } = [];

        public void Process(RoomProcessorContext context, RoomObjectSnapshot creep, CreepDeathOptions options, IDictionary<string, int> energyLedger)
            => Creeps.Add(creep);
    }

}
