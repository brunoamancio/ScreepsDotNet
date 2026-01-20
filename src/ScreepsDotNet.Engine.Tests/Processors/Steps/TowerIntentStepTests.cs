namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;

public sealed class TowerIntentStepTests
{
    [Fact]
    public async Task ExecuteAsync_Attack_KillsCreep()
    {
        var tower = CreateTower("tower1", energy: 50);
        var creep = CreateCreep("creep1", hits: 100);
        var intents = CreateIntents(tower.Id, new IntentRecord(IntentKeys.Attack, [CreateArgument(creep.Id)]));
        var context = CreateContext([tower, creep], intents);
        var deathProcessor = new RecordingDeathProcessor();
        var step = new TowerIntentStep(deathProcessor);

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Single(deathProcessor.Calls);
        Assert.Equal(creep.Id, deathProcessor.Calls[0]);
    }

    [Fact]
    public async Task ExecuteAsync_Repair_IncrementsStats()
    {
        var tower = CreateTower("tower1", energy: 50);
        var wall = CreateStructure("wall1", hits: 1000, hitsMax: 5000);
        var intents = CreateIntents(tower.Id, new IntentRecord(IntentKeys.Repair, [CreateArgument(wall.Id)]));
        var statsSink = new TestStatsSink();
        var context = CreateContext([tower, wall], intents, statsSink);
        var step = new TowerIntentStep(new RecordingDeathProcessor());

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.True(statsSink.EnergyConstruction >= ScreepsGameConstants.TowerEnergyCost);
    }

    private static IntentArgument CreateArgument(string targetId)
        => new(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId)
        });

    private static RoomProcessorContext CreateContext(
        IEnumerable<RoomObjectSnapshot> objects,
        RoomIntentSnapshot intents,
        ICreepStatsSink? statsSink = null)
    {
        var map = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal);
        foreach (var obj in objects)
            map[obj.Id] = obj;

        var state = new RoomState(
            "W1N1",
            100,
            null,
            map,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            intents,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);

        return new RoomProcessorContext(state, new FakeMutationWriter(), statsSink ?? new NullCreepStatsSink());
    }

    private static RoomIntentSnapshot CreateIntents(string objectId, IntentRecord record, string userId = "user1")
    {
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [objectId] = [record]
        };

        var envelope = new IntentEnvelope(
            userId,
            objectIntents,
            new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal));

        return new RoomIntentSnapshot(
            "W1N1",
            "shard0",
            new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
            {
                [userId] = envelope
            });
    }

    private static RoomObjectSnapshot CreateTower(string id, int energy)
        => new(
            id,
            RoomObjectTypes.Tower,
            "W1N1",
            "shard0",
            "user1",
            10,
            10,
            Hits: 3000,
            HitsMax: 3000,
            Fatigue: null,
            TicksToLive: null,
            Name: "Tower1",
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Tower,
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RoomDocumentFields.RoomObject.Store.Energy] = energy
            },
            StoreCapacity: 1000,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateCreep(string id, int hits)
        => new(
            id,
            RoomObjectTypes.Creep,
            "W1N1",
            "shard0",
            "targetUser",
            12,
            10,
            Hits: hits,
            HitsMax: hits,
            Fatigue: 0,
            TicksToLive: 100,
            Name: id,
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
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateStructure(string id, int hits, int hitsMax)
        => new(
            id,
            RoomObjectTypes.Wall,
            "W1N1",
            "shard0",
            "targetUser",
            11,
            11,
            Hits: hits,
            HitsMax: hitsMax,
            Fatigue: null,
            TicksToLive: null,
            Name: id,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Wall,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private sealed class RecordingDeathProcessor : ICreepDeathProcessor
    {
        public List<string> Calls { get; } = [];

        public void Process(RoomProcessorContext context, RoomObjectSnapshot creep, CreepDeathOptions options, IDictionary<string, int> energyLedger)
            => Calls.Add(creep.Id);
    }

    private sealed class FakeMutationWriter : IRoomMutationWriter
    {
        public List<(string ObjectId, RoomObjectPatchPayload Payload)> Patches { get; } = [];
        public List<string> Removals { get; } = [];

        public void Upsert(RoomObjectSnapshot document) { }

        public void Patch(string objectId, RoomObjectPatchPayload patch)
            => Patches.Add((objectId, patch));

        public void Remove(string objectId)
        {
            if (!string.IsNullOrWhiteSpace(objectId))
                Removals.Add(objectId);
        }

        public void SetRoomInfoPatch(RoomInfoPatchPayload patch) { }

        public void SetEventLog(IRoomEventLogPayload? eventLog) { }

        public void SetMapView(IRoomMapViewPayload? mapView) { }

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

        public void Reset() { }
    }

    private sealed class TestStatsSink : ICreepStatsSink
    {
        public int EnergyConstruction { get; private set; }

        public void IncrementEnergyCreeps(string userId, int amount) { }
        public void IncrementCreepsLost(string userId, int bodyParts) { }
        public void IncrementCreepsProduced(string userId, int bodyParts) { }
        public void IncrementSpawnRenewals(string userId) { }
        public void IncrementSpawnRecycles(string userId) { }
        public void IncrementSpawnCreates(string userId) { }
        public void IncrementTombstonesCreated(string userId) { }
        public void IncrementEnergyConstruction(string userId, int amount)
            => EnergyConstruction += amount;
        public void IncrementEnergyHarvested(string userId, int amount) { }
        public void IncrementEnergyControl(string userId, int amount) { }
        public Task FlushAsync(int gameTime, CancellationToken token = default) => Task.CompletedTask;
    }
}
