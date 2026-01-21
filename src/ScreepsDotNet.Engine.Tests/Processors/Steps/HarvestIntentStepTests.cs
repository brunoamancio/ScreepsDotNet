namespace ScreepsDotNet.Engine.Tests.Processors.Steps;
using ScreepsDotNet.Engine.Tests.Processors.Helpers;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;

public sealed class HarvestIntentStepTests
{
    private readonly HarvestIntentStep _step = new(new ResourceDropHelper());
    private readonly RecordingStatsSink _statsSink = new();

    [Fact]
    public async Task ExecuteAsync_HarvestsSourceAndUpdatesStats()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Work], capacity: 50);
        var source = CreateSource("source1", 11, 10, energy: 300);
        var context = CreateContext([creep, source], CreateHarvestIntents("user1", creep.Id, source.Id), statsSink: _statsSink);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == source.Id);
        Assert.Equal(296, Payload.Energy);
        Assert.Equal(4, Payload.InvaderHarvested);

        var creepPatch = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null && p.Payload.ActionLog is not null);
        Assert.Equal(4, creepPatch.Payload.Store![ResourceTypes.Energy]);
        Assert.Equal(11, creepPatch.Payload.ActionLog!.Harvest!.X);
        Assert.Equal(10, creepPatch.Payload.ActionLog.Harvest!.Y);

        Assert.Equal(4, _statsSink.EnergyHarvested["user1"]);
    }

    [Fact]
    public async Task ExecuteAsync_DropsOverflowWhenStoreFull()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", [BodyPartType.Work], capacity: 1);
        var source = CreateSource("source1", 11, 10, energy: 50);
        var context = CreateContext([creep, source], CreateHarvestIntents("user1", creep.Id, source.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var drop = Assert.Single(writer.Upserts);
        Assert.Equal(RoomObjectTypes.Resource, drop.Type);
        Assert.Equal(ResourceTypes.Energy, drop.ResourceType);
        Assert.Equal(1, drop.ResourceAmount);

        var (ObjectId, Payload) = writer.Patches.Last(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(1, Payload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task ExecuteAsync_HarvestsMineralWithExtractor()
    {
        var creep = CreateCreep("creep1", 20, 20, "user1", [BodyPartType.Work, BodyPartType.Work], capacity: 100);
        var mineral = CreateMineral("mineral1", 21, 20, ResourceTypes.Hydrogen, amount: 10);
        var extractor = CreateExtractor("extractor1", 21, 20, "user1");
        var controller = CreateController("controller1", 25, 25, "user1", level: 3);

        var objects = new[] { creep, mineral, extractor, controller };
        var context = CreateContext(objects, CreateHarvestIntents("user1", creep.Id, mineral.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == mineral.Id);
        Assert.Equal(8, Payload.MineralAmount);

        var extractorPatch = writer.Patches.Single(p => p.ObjectId == extractor.Id && p.Payload.Cooldown.HasValue);
        Assert.Equal(ScreepsGameConstants.ExtractorCooldown, extractorPatch.Payload.Cooldown);
    }

    [Fact]
    public async Task ExecuteAsync_HarvestsDepositAndSetsCooldown()
    {
        var creep = CreateCreep("creep1", 30, 30, "user1", [BodyPartType.Work, BodyPartType.Work, BodyPartType.Work], capacity: 100);
        var deposit = CreateDeposit("deposit1", 31, 30, ResourceTypes.Mist, harvested: 0, cooldownTime: null);
        var context = CreateContext([creep, deposit], CreateHarvestIntents("user1", creep.Id, deposit.Id), gameTime: 200);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == deposit.Id && p.Payload.Harvested.HasValue);
        Assert.True(Payload.Harvested > 0);
        Assert.True(Payload.CooldownTime > context.State.GameTime);
        Assert.True(Payload.DecayTime > context.State.GameTime);
    }

    private static RoomProcessorContext CreateContext(
        IEnumerable<RoomObjectSnapshot> objects,
        RoomIntentSnapshot intents,
        int gameTime = 100,
        ICreepStatsSink? statsSink = null)
    {
        var objectMap = objects.ToDictionary(o => o.Id, o => o, StringComparer.Ordinal);
        var state = new RoomState(
            "W1N1",
            gameTime,
            null,
            objectMap,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            intents,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);

        return new RoomProcessorContext(state, new FakeMutationWriter(), statsSink ?? new NullCreepStatsSink(), new NullGlobalMutationWriter());
    }

    private static RoomIntentSnapshot CreateHarvestIntents(string userId, string creepId, string targetId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId)
        });

        var record = new IntentRecord(IntentKeys.Harvest, [argument]);
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [creepId] = [record]
        };

        var envelope = new IntentEnvelope(
            userId,
            objectIntents,
            new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal));

        var users = new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
        {
            [userId] = envelope
        };

        return new RoomIntentSnapshot("W1N1", "shard0", users);
    }

    private static RoomObjectSnapshot CreateCreep(
        string id,
        int x,
        int y,
        string userId,
        IReadOnlyList<BodyPartType> body,
        int capacity)
        => new(
            id,
            RoomObjectTypes.Creep,
            "W1N1",
            "shard0",
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
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: capacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: body.Select(part => new CreepBodyPartSnapshot(part, ScreepsGameConstants.BodyPartHitPoints, null)).ToArray());

    private static RoomObjectSnapshot CreateSource(string id, int x, int y, int energy)
        => new(
            id,
            RoomObjectTypes.Source,
            "W1N1",
            "shard0",
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
            StructureType: RoomObjectTypes.Source,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            IsSpawning: null,
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
            Energy: energy,
            InvaderHarvested: 0);

    private static RoomObjectSnapshot CreateMineral(string id, int x, int y, string resourceType, int amount)
        => new(
            id,
            RoomObjectTypes.Mineral,
            "W1N1",
            "shard0",
            null,
            x,
            y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: 1,
            MineralType: resourceType,
            DepositType: null,
            StructureType: RoomObjectTypes.Mineral,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            MineralAmount: amount);

    private static RoomObjectSnapshot CreateExtractor(string id, int x, int y, string userId)
        => new(
            id,
            RoomObjectTypes.Extractor,
            "W1N1",
            "shard0",
            userId,
            x,
            y,
            Hits: 500,
            HitsMax: 500,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Extractor,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateController(string id, int x, int y, string userId, int level)
        => new(
            id,
            RoomObjectTypes.Controller,
            "W1N1",
            "shard0",
            userId,
            x,
            y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: level,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Controller,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateDeposit(string id, int x, int y, string depositType, int harvested, int? cooldownTime)
        => new(
            id,
            RoomObjectTypes.Deposit,
            "W1N1",
            "shard0",
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
            DepositType: depositType,
            StructureType: RoomObjectTypes.Deposit,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            Harvested: harvested,
            CooldownTime: cooldownTime);

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

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

        public void Reset()
        {
            Patches.Clear();
            Upserts.Clear();
            Removals.Clear();
        }
    }

    private sealed class RecordingStatsSink : ICreepStatsSink
    {
        public Dictionary<string, int> EnergyHarvested { get; } = new(StringComparer.Ordinal);

        public void IncrementEnergyHarvested(string userId, int amount)
            => EnergyHarvested[userId] = EnergyHarvested.GetValueOrDefault(userId, 0) + amount;

        public void IncrementEnergyConstruction(string userId, int amount) { }
        public void IncrementEnergyCreeps(string userId, int amount) { }
        public void IncrementCreepsLost(string userId, int bodyParts) { }
        public void IncrementCreepsProduced(string userId, int bodyParts) { }
        public void IncrementSpawnRenewals(string userId) { }
        public void IncrementSpawnRecycles(string userId) { }
        public void IncrementSpawnCreates(string userId) { }
        public void IncrementTombstonesCreated(string userId) { }
        public void IncrementEnergyControl(string userId, int amount) { }
        public Task FlushAsync(int gameTime, CancellationToken token = default) => Task.CompletedTask;
    }
}
