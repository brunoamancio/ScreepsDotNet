using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;

namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

public sealed class CreepLifecycleStepTests
{
    [Fact]
    public async Task ExecuteAsync_DecrementsTicksToLive()
    {
        var creep = CreateCreep(ticksToLive: 5);
        var context = CreateContext(creep);
        var step = new CreepLifecycleStep(new NullDeathProcessor());

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        var patch = ((RecordingMutationWriter)context.MutationWriter).Patches[0];
        Assert.Equal("creep1", patch.ObjectId);
        Assert.Equal(4, patch.Payload.TicksToLive);
    }

    [Fact]
    public async Task ExecuteAsync_KillsCreepWhenTicksToLiveWouldExpire()
    {
        var creep = CreateCreep(ticksToLive: 1);
        var deathProcessor = new RecordingDeathProcessor();
        var context = CreateContext(creep);
        var step = new CreepLifecycleStep(deathProcessor);

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(((RecordingMutationWriter)context.MutationWriter).Patches);
        Assert.Single(deathProcessor.Creeps);
        Assert.Equal("creep1", deathProcessor.Creeps[0].Id);
    }

    private static RoomProcessorContext CreateContext(RoomObjectSnapshot primary, params RoomObjectSnapshot[] additional)
    {
        var objects = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal)
        {
            [primary.Id] = primary
        };

        foreach (var extra in additional)
            objects[extra.Id] = extra;

        var state = new RoomState(
            primary.RoomName,
            10,
            null,
            objects,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            null,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            Array.Empty<RoomFlagSnapshot>());

        return new RoomProcessorContext(
            state,
            new RecordingMutationWriter(),
            new NullCreepStatsSink());
    }

    private static RoomObjectSnapshot CreateCreep(int ticksToLive, int hits = 100, string userId = "user1", bool userSummoned = false, string id = "creep1")
        => new(
            id,
            RoomObjectTypes.Creep,
            "W1N1",
            null,
            userId,
            10,
            10,
            Hits: hits,
            HitsMax: 100,
            Fatigue: 0,
            TicksToLive: ticksToLive,
            Name: "Worker",
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(StringComparer.Ordinal),
            Spawning: null,
            Body: [],
            UserSummoned: userSummoned);

    [Fact]
    public async Task ExecuteAsync_RemovesZeroHitCreeps()
    {
        var creep = CreateCreep(ticksToLive: 50, hits: 0);
        var deathProcessor = new RecordingDeathProcessor();
        var context = CreateContext(creep);
        var step = new CreepLifecycleStep(deathProcessor);

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(((RecordingMutationWriter)context.MutationWriter).Patches);
        Assert.Single(deathProcessor.Creeps);
        Assert.Equal("creep1", deathProcessor.Creeps[0].Id);
    }

    [Fact]
    public async Task ExecuteAsync_UserSummonedDiesWhenEnemyPlayerPresent()
    {
        var summoned = CreateCreep(ticksToLive: 100, hits: 50, userId: "Invader", userSummoned: true, id: "summoned");
        var hostile = CreateCreep(ticksToLive: 200, hits: 100, userId: "player1", id: "hostile");
        var controller = CreateController("ally");
        var deathProcessor = new RecordingDeathProcessor();
        var context = CreateContext(summoned, hostile, controller);
        var step = new CreepLifecycleStep(deathProcessor);

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Single(deathProcessor.Creeps);
        Assert.Equal("summoned", deathProcessor.Creeps[0].Id);
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
    }

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

        public void Reset()
            => Patches.Clear();
    }

    private static RoomObjectSnapshot CreateController(string userId)
        => new(
            "controller1",
            RoomObjectTypes.Controller,
            "W1N1",
            null,
            userId,
            5,
            5,
            Hits: 0,
            HitsMax: 0,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: 3,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(StringComparer.Ordinal),
            Spawning: null,
            Body: Array.Empty<CreepBodyPartSnapshot>());
}
