namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;
using Xunit;

public sealed class MovementIntentStepTests
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    [Fact]
    public async Task ExecuteAsync_MovesIntoAvailableTile()
    {
        var mover = CreateCreep("creepA", 10, 10);
        var state = CreateState(
            [mover],
            CreateIntentSnapshot(
                [("user1", "creepA", new MoveIntent(11, 10))]));
        var writer = new RecordingMutationWriter();
        var step = new MovementIntentStep(new NullDeathProcessor());

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink()), TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = Assert.Single(writer.Patches);
        Assert.Equal("creepA", ObjectId);
        Assert.Equal(11, Payload.Position?.X);
        Assert.Equal(10, Payload.Position?.Y);
        Assert.Equal(0, Payload.Fatigue);
    }

    [Fact]
    public async Task ExecuteAsync_AllowsEntryAfterOccupantLeaves()
    {
        var mover = CreateCreep("creepA", 10, 10);
        var blocker = CreateCreep("creepB", 11, 10);
        var intents = CreateIntentSnapshot(
            [
                ("user1", "creepA", new MoveIntent(11, 10)),
                ("user1", "creepB", new MoveIntent(10, 10))
            ]);
        var state = CreateState([mover, blocker], intents);
        var writer = new RecordingMutationWriter();
        var step = new MovementIntentStep(new NullDeathProcessor());

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink()), TestContext.Current.CancellationToken);

        Assert.Equal(2, writer.Patches.Count);
        var aPatch = writer.Patches.First(p => p.ObjectId == "creepA").Payload.Position;
        Assert.Equal((11, 10), (aPatch!.X, aPatch.Y));

        var bPatch = writer.Patches.First(p => p.ObjectId == "creepB").Payload.Position;
        Assert.Equal((10, 10), (bPatch!.X, bPatch.Y));
    }

    [Fact]
    public async Task ExecuteAsync_CrashesOutOfBoundsMove()
    {
        var mover = CreateCreep("creepA", 0, 0);
        var intents = CreateIntentSnapshot([("user1", "creepA", new MoveIntent(-1, 0))]);
        var state = CreateState([mover], intents);
        var writer = new RecordingMutationWriter();
        var death = new RecordingDeathProcessor();
        var step = new MovementIntentStep(death);

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink()), TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
        var crashed = Assert.Single(death.Creeps);
        Assert.Equal("creepA", crashed.Id);
    }

    [Fact]
    public async Task ExecuteAsync_OutOfBoundsWithAccessibleExitSchedulesTransfer()
    {
        var mover = CreateCreep("creepA", 0, 0);
        var intents = CreateIntentSnapshot([("user1", "creepA", new MoveIntent(-1, 0))]);
        var state = CreateState([mover], intents);
        var writer = new RecordingMutationWriter();
        var death = new RecordingDeathProcessor();
        var step = new MovementIntentStep(death);
        var exitTopology = new RoomExitTopology(
            null,
            null,
            null,
            new RoomExitDescriptor("W0S0", 10, true));
        var context = new RoomProcessorContext(state, writer, new NullCreepStatsSink(), exitTopology);

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.NotEmpty(writer.Patches);
        Assert.Empty(death.Creeps);
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.Payload.InterRoom is not null);
        Assert.NotNull(Payload.InterRoom);
        Assert.Equal("W0S0", Payload.InterRoom!.RoomName);
        Assert.Equal(49, Payload.InterRoom.X);
        Assert.Equal(0, Payload.InterRoom.Y);
    }

    [Fact]
    public async Task ExecuteAsync_OutOfBoundsCrashesWhenExitInaccessible()
    {
        var mover = CreateCreep("creepA", 49, 49);
        var intents = CreateIntentSnapshot([("user1", "creepA", new MoveIntent(50, 49))]);
        var state = CreateState([mover], intents);
        var writer = new RecordingMutationWriter();
        var death = new RecordingDeathProcessor();
        var step = new MovementIntentStep(death);
        var exitTopology = new RoomExitTopology(
            null,
            new RoomExitDescriptor("E1S0", 20, false),
            null,
            null);
        var context = new RoomProcessorContext(state, writer, new NullCreepStatsSink(), exitTopology);

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
        Assert.Contains(death.Creeps, c => c.Id == "creepA");
    }

    [Fact]
    public async Task ExecuteAsync_PortalMoveSchedulesTransfer()
    {
        var mover = CreateCreep("creepA", 10, 10);
        var portalDestination = new RoomPortalDestinationSnapshot("W0S0", 25, 0, "shard3");
        var portal = CreatePortal("portal1", 11, 10, portalDestination);

        var intents = CreateIntentSnapshot([("user1", "creepA", new MoveIntent(11, 10))]);
        var state = CreateState([mover, portal], intents);
        var writer = new RecordingMutationWriter();
        var step = new MovementIntentStep(new NullDeathProcessor());

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink()), TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.Payload.InterRoom is not null);
        Assert.NotNull(Payload.InterRoom);
        Assert.Equal("W0S0", Payload.InterRoom!.RoomName);
        Assert.Equal(25, Payload.InterRoom.X);
        Assert.Equal(0, Payload.InterRoom.Y);
        Assert.Equal("shard3", Payload.InterRoom.Shard);
    }

    [Fact]
    public async Task ExecuteAsync_IdleOnPortalSchedulesTransfer()
    {
        var mover = CreateCreep("creepA", 11, 10);
        var portalDestination = new RoomPortalDestinationSnapshot("W0S0", 20, 0, null);
        var portal = CreatePortal("portal1", 11, 10, portalDestination);

        var intents = CreateEmptyIntentSnapshot();
        var state = CreateState([mover, portal], intents);
        var writer = new RecordingMutationWriter();
        var step = new MovementIntentStep(new NullDeathProcessor());

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink()), TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.Payload.InterRoom is not null);
        Assert.NotNull(Payload.InterRoom);
        Assert.Equal("W0S0", Payload.InterRoom!.RoomName);
        Assert.Equal(20, Payload.InterRoom.X);
        Assert.Equal(0, Payload.InterRoom.Y);
        Assert.Null(Payload.InterRoom.Shard);
    }

    [Fact]
    public async Task ExecuteAsync_CrashesWhenBlockedByStructure()
    {
        var mover = CreateCreep("creepA", 10, 10);
        var wall = CreateStructure("wall1", RoomObjectTypes.ConstructedWall, 11, 10);
        var intents = CreateIntentSnapshot([("user1", "creepA", new MoveIntent(11, 10))]);
        var state = CreateState([mover, wall], intents);
        var writer = new RecordingMutationWriter();
        var death = new RecordingDeathProcessor();
        var step = new MovementIntentStep(death);

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink()), TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
        var crashed = Assert.Single(death.Creeps);
        Assert.Equal("creepA", crashed.Id);
    }

    [Fact]
    public async Task ExecuteAsync_CrashesAllEntrantsWhenTileIsFatal()
    {
        var creepA = CreateCreep("creepA", 10, 10);
        var creepB = CreateCreep("creepB", 10, 11);
        var spawn = CreateStructure("spawn1", RoomObjectTypes.Spawn, 11, 10);

        var intents = CreateIntentSnapshot(
            [
                ("user1", "creepA", new MoveIntent(11, 10)),
                ("user1", "creepB", new MoveIntent(11, 10))
            ]);

        var state = CreateState([creepA, creepB, spawn], intents);
        var writer = new RecordingMutationWriter();
        var death = new RecordingDeathProcessor();
        var step = new MovementIntentStep(death);

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink()), TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
        Assert.Equal(2, death.Creeps.Count);
        Assert.Contains(death.Creeps, c => c.Id == "creepA");
        Assert.Contains(death.Creeps, c => c.Id == "creepB");
    }

    [Fact]
    public async Task ExecuteAsync_AllowsSwapResolution()
    {
        var creepA = CreateCreep("creepA", 10, 10);
        var creepB = CreateCreep("creepB", 11, 10);
        var intents = CreateIntentSnapshot(
            [
                ("user1", "creepA", new MoveIntent(11, 10)),
                ("user1", "creepB", new MoveIntent(10, 10))
            ]);
        var state = CreateState([creepA, creepB], intents);
        var writer = new RecordingMutationWriter();
        var step = new MovementIntentStep(new NullDeathProcessor());

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink()), TestContext.Current.CancellationToken);

        Assert.Equal(2, writer.Patches.Count);
        Assert.Contains(writer.Patches, p => p.ObjectId == "creepA" && p.Payload.Position?.X == 11 && p.Payload.Position?.Y == 10);
        Assert.Contains(writer.Patches, p => p.ObjectId == "creepB" && p.Payload.Position?.X == 10 && p.Payload.Position?.Y == 10);
    }

    [Fact]
    public async Task ExecuteAsync_PulledCreepTakesPriority()
    {
        var puller = CreateCreep("puller", 10, 10);
        var pulled = CreateCreep("pulled", 10, 11);
        var rival = CreateCreep("rival", 12, 10, userId: "user2");

        var objectIntents = new[]
        {
            ("user1", "puller", CreateTargetIntentRecord(IntentKeys.Pull, "pulled"))
        };

        var intents = CreateIntentSnapshot(
            [
                ("user1", "puller", new MoveIntent(11, 10)),
                ("user1", "pulled", new MoveIntent(10, 10)),
                ("user2", "rival", new MoveIntent(11, 10))
            ],
            objectIntents);

        var state = CreateState([puller, pulled, rival], intents);
        var writer = new RecordingMutationWriter();
        var step = new MovementIntentStep(new NullDeathProcessor());

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink()), TestContext.Current.CancellationToken);

        Assert.Contains(writer.Patches, p => p.ObjectId == "puller" && p.Payload.Position?.X == 11 && p.Payload.Position?.Y == 10);
        Assert.Contains(writer.Patches, p => p.ObjectId == "pulled" && p.Payload.Position?.X == 10 && p.Payload.Position?.Y == 10);
        Assert.DoesNotContain(writer.Patches, p => p.ObjectId == "rival");
    }

    [Fact]
    public async Task ExecuteAsync_RespectsRampartPublicFlag()
    {
        var enemy = CreateCreep("enemy", 10, 10, userId: "enemy");
        var rampart = CreateRampart("ramp1", 11, 10, owner: "defender", isPublic: false);

        var intents = CreateIntentSnapshot([("enemy", "enemy", new MoveIntent(11, 10))]);
        var state = CreateState([enemy, rampart], intents);
        var writer = new RecordingMutationWriter();
        var death = new RecordingDeathProcessor();
        var step = new MovementIntentStep(death);

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink()), TestContext.Current.CancellationToken);
        Assert.Empty(writer.Patches);
        Assert.Contains(death.Creeps, c => c.Id == "enemy");

        var publicRampart = rampart with { IsPublic = true };
        state = CreateState([enemy, publicRampart], intents);
        writer.Reset();
        death.Reset();

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink()), TestContext.Current.CancellationToken);
        Assert.Contains(writer.Patches, p => p.ObjectId == "enemy");
        Assert.Empty(death.Creeps);
    }

    [Fact]
    public async Task ExecuteAsync_CrashesPullChainWhenTargetIsFatal()
    {
        var puller = CreateCreep("puller", 10, 10);
        var pulled = CreateCreep("pulled", 10, 11);
        var wall = CreateStructure("wall1", RoomObjectTypes.ConstructedWall, 11, 10);

        var objectIntents = new[]
        {
            ("user1", "puller", CreateTargetIntentRecord(IntentKeys.Pull, "pulled"))
        };

        var intents = CreateIntentSnapshot(
            [
                ("user1", "puller", new MoveIntent(11, 10)),
                ("user1", "pulled", new MoveIntent(11, 10))
            ],
            objectIntents);

        var state = CreateState([puller, pulled, wall], intents);
        var writer = new RecordingMutationWriter();
        var death = new RecordingDeathProcessor();
        var step = new MovementIntentStep(death);

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink()), TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
        Assert.Equal(2, death.Creeps.Count);
        Assert.Contains(death.Creeps, c => c.Id == "puller");
        Assert.Contains(death.Creeps, c => c.Id == "pulled");
    }

    [Fact]
    public async Task ExecuteAsync_PowerCreepCrashesOnPortal()
    {
        var powerCreep = CreatePowerCreep("pc1", 10, 10);
        var portal = CreatePortal("portal1", 11, 10);
        var intents = CreateIntentSnapshot([("user1", "pc1", new MoveIntent(11, 10))]);
        var state = CreateState([powerCreep, portal], intents);
        var writer = new RecordingMutationWriter();
        var death = new RecordingDeathProcessor();
        var step = new MovementIntentStep(death);

        await step.ExecuteAsync(new RoomProcessorContext(state, writer, new NullCreepStatsSink()), TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
        Assert.Contains(death.Creeps, c => c.Id == "pc1");
    }

    private static RoomIntentSnapshot CreateIntentSnapshot(
        IReadOnlyList<(string UserId, string CreepId, MoveIntent Move)> creepMoves,
        IReadOnlyList<(string UserId, string ObjectId, IntentRecord Intent)>? objectIntents = null)
    {
        var envelopes = new Dictionary<string, IntentEnvelope>(Comparer);

        foreach (var group in creepMoves.GroupBy(move => move.UserId, Comparer)) {
            var creepIntentMap = new Dictionary<string, CreepIntentEnvelope>(Comparer);
            foreach (var (userId, creepId, move) in group) {
                creepIntentMap[creepId] = new CreepIntentEnvelope(
                    move,
                    null,
                    null,
                    new Dictionary<string, object?>(0));
            }

            var userObjectIntents = objectIntents?
                .Where(entry => string.Equals(entry.UserId, group.Key, StringComparison.Ordinal))
                .GroupBy(entry => entry.ObjectId, Comparer)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<IntentRecord>)g.Select(e => e.Intent).ToList(),
                    Comparer)
                ?? new Dictionary<string, IReadOnlyList<IntentRecord>>(Comparer);

            envelopes[group.Key] = new IntentEnvelope(
                group.Key,
                userObjectIntents,
                new Dictionary<string, SpawnIntentEnvelope>(0, Comparer),
                creepIntentMap);
        }

        return new RoomIntentSnapshot("W1N1", null, envelopes);
    }

    private static RoomIntentSnapshot CreateEmptyIntentSnapshot()
    {
        var envelopes = new Dictionary<string, IntentEnvelope>(Comparer)
        {
            ["user1"] = new("user1",
                            new Dictionary<string, IReadOnlyList<IntentRecord>>(Comparer),
                            new Dictionary<string, SpawnIntentEnvelope>(Comparer),
                            new Dictionary<string, CreepIntentEnvelope>(Comparer))
        };

        return new RoomIntentSnapshot("W1N1", null, envelopes);
    }

    private static RoomState CreateState(IReadOnlyList<RoomObjectSnapshot> objects, RoomIntentSnapshot intents)
    {
        var map = objects.ToDictionary(obj => obj.Id, obj => obj, Comparer);
        return new RoomState(
            "W1N1",
            100,
            null,
            map,
            new Dictionary<string, UserState>(Comparer),
            intents,
            new Dictionary<string, RoomTerrainSnapshot>(Comparer),
            []);
    }

    private static RoomObjectSnapshot CreateCreep(string id, int x, int y, string userId = "user1")
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
            TicksToLive: 1000,
            Name: id,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(Comparer),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(Comparer),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(Comparer),
            Spawning: null,
            Body:
            [
                new(BodyPartType.Move, ScreepsGameConstants.BodyPartHitPoints, null)
            ]);

    private static RoomObjectSnapshot CreatePowerCreep(string id, int x, int y, string userId = "user1")
        => new(
            id,
            RoomObjectTypes.PowerCreep,
            "W1N1",
            null,
            userId,
            x,
            y,
            Hits: 100,
            HitsMax: 100,
            Fatigue: 0,
            TicksToLive: 1000,
            Name: id,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(Comparer),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(Comparer),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(Comparer),
            Spawning: null,
            Body:
            [
                new(BodyPartType.Move, ScreepsGameConstants.BodyPartHitPoints, null)
            ]);

    private static RoomObjectSnapshot CreateRampart(string id, int x, int y, string owner, bool isPublic)
        => new(
            id,
            RoomObjectTypes.Rampart,
            "W1N1",
            null,
            owner,
            x,
            y,
            Hits: 1000,
            HitsMax: ScreepsGameConstants.RampartHits,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Rampart,
            Store: new Dictionary<string, int>(Comparer),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(Comparer),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(Comparer),
            Spawning: null,
            Body: [],
            IsSpawning: null,
            UserSummoned: null,
            IsPublic: isPublic);

    private static RoomObjectSnapshot CreatePortal(string id, int x, int y, RoomPortalDestinationSnapshot? destination = null)
        => new(
            id,
            RoomObjectTypes.Portal,
            "W1N1",
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
            StructureType: RoomObjectTypes.Portal,
            Store: new Dictionary<string, int>(Comparer),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(Comparer),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(Comparer),
            Spawning: null,
            Body: [],
            PortalDestination: destination);

    private static RoomObjectSnapshot CreateStructure(string id, string type, int x, int y, string? owner = "system")
        => new(
            id,
            type,
            "W1N1",
            null,
            owner,
            x,
            y,
            Hits: 1000,
            HitsMax: 1000,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: type,
            Store: new Dictionary<string, int>(Comparer),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(Comparer),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(Comparer),
            Spawning: null,
            Body: []);

    private static IntentRecord CreateTargetIntentRecord(string name, string targetId)
        => new(
            name,
            [
                new IntentArgument(
                                   new Dictionary<string, IntentFieldValue>(Comparer)
                                   {
                                       [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId)
                                   })
            ]);

    private sealed class RecordingMutationWriter : IRoomMutationWriter
    {
        public List<(string ObjectId, RoomObjectPatchPayload Payload)> Patches { get; } = [];

        public void Upsert(RoomObjectSnapshot document) { }

        public void Patch(string objectId, RoomObjectPatchPayload patch)
            => Patches.Add((objectId, patch));

        public void Remove(string objectId) { }

        public void SetRoomInfoPatch(RoomInfoPatchPayload patch) { }

        public void SetEventLog(IRoomEventLogPayload? eventLog) { }

        public void SetMapView(IRoomMapViewPayload? mapView) { }

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

        public void Reset() => Patches.Clear();
    }

    private sealed class NullDeathProcessor : ICreepDeathProcessor
    {
        public void Process(RoomProcessorContext context, RoomObjectSnapshot creep, CreepDeathOptions options, IDictionary<string, int> energyLedger) { }
    }

    private sealed class RecordingDeathProcessor : ICreepDeathProcessor
    {
        public List<RoomObjectSnapshot> Creeps { get; } = [];

        public void Process(RoomProcessorContext context, RoomObjectSnapshot creep, CreepDeathOptions options, IDictionary<string, int> energyLedger)
            => Creeps.Add(creep);

        public void Reset() => Creeps.Clear();
    }
}
