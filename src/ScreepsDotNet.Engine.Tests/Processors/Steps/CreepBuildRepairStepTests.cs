namespace ScreepsDotNet.Engine.Tests.Processors.Steps;
using ScreepsDotNet.Engine.Tests.Processors.Helpers;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Structures;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;
using Xunit;

public sealed class CreepBuildRepairStepTests
{
    private readonly IStructureBlueprintProvider _blueprints = new StructureBlueprintProvider();
    private readonly IStructureSnapshotFactory _snapshotFactory = new StructureSnapshotFactory();

    [Fact]
    public async Task ExecuteAsync_BuildAdvancesProgress()
    {
        var creep = CreateCreep("creep1", energy: 50);
        var site = CreateConstructionSite("site1", RoomObjectTypes.Extension, progress: 0, progressTotal: 200);
        var (State, MutationWriter) = CreateContext([creep, site], CreateIntentSnapshot("user1", creep.Id, IntentKeys.Build, site.Id));
        var stats = new RecordingStatsSink();
        var step = new CreepBuildRepairStep(_blueprints, _snapshotFactory);

        await step.ExecuteAsync(new RoomProcessorContext(State, MutationWriter, stats, new NullGlobalMutationWriter()), TestContext.Current.CancellationToken);

        Assert.Contains(MutationWriter.Patches, patch => patch.ObjectId == site.Id && patch.Payload.Progress == 5);
        Assert.Contains(MutationWriter.Patches, patch => patch.ObjectId == creep.Id && patch.Payload.Store![RoomDocumentFields.RoomObject.Store.Energy] == 45);
        Assert.Equal(5, stats.EnergyConstruction["user1"]);
    }

    [Fact]
    public async Task ExecuteAsync_BuildCompletesSite()
    {
        var creep = CreateCreep("creep1", energy: 10);
        var site = CreateConstructionSite("site1", RoomObjectTypes.Extension, progress: 195, progressTotal: 200);
        var (State, MutationWriter) = CreateContext([creep, site], CreateIntentSnapshot("user1", creep.Id, IntentKeys.Build, site.Id));
        var stats = new RecordingStatsSink();
        var step = new CreepBuildRepairStep(_blueprints, _snapshotFactory);

        await step.ExecuteAsync(new RoomProcessorContext(State, MutationWriter, stats, new NullGlobalMutationWriter()), TestContext.Current.CancellationToken);

        Assert.Contains(MutationWriter.Removals, id => id == site.Id);
        Assert.Contains(MutationWriter.Upserts, upsert => upsert.Type == RoomObjectTypes.Extension);
    }

    [Fact]
    public async Task ExecuteAsync_RepairRestoresHits()
    {
        var creep = CreateCreep("creep1", energy: 20);
        var structure = CreateStructure("wall1", RoomObjectTypes.Wall, hits: 100, hitsMax: 1000);
        var (State, MutationWriter) = CreateContext([creep, structure], CreateIntentSnapshot("user1", creep.Id, IntentKeys.Repair, structure.Id));
        var stats = new RecordingStatsSink();
        var step = new CreepBuildRepairStep(_blueprints, _snapshotFactory);

        await step.ExecuteAsync(new RoomProcessorContext(State, MutationWriter, stats, new NullGlobalMutationWriter()), TestContext.Current.CancellationToken);

        Assert.Contains(MutationWriter.Patches, patch => patch.ObjectId == structure.Id && patch.Payload.Hits == 200);
        Assert.Contains(MutationWriter.Patches, patch => patch.ObjectId == creep.Id && patch.Payload.Store![RoomDocumentFields.RoomObject.Store.Energy] == 19);
    }

    private static (RoomState State, FakeMutationWriter MutationWriter) CreateContext(
        IReadOnlyList<RoomObjectSnapshot> objects,
        RoomIntentSnapshot intents)
    {
        var map = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal);
        foreach (var obj in objects)
            map[obj.Id] = obj;

        var state = new RoomState(
            "W1N1",
            1000,
            new RoomInfoSnapshot("room", null, null, false, false, null, null, ControllerLevel.Level3, null, null, null, null),
            map,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            intents,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal)
            {
                ["terrain"] = new("terrain", "W1N1", null, null, new string('0', 2500))
            },
            []);

        return (state, new FakeMutationWriter());
    }

    private static RoomIntentSnapshot CreateIntentSnapshot(string userId, string creepId, string intentName, string targetId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId)
        });

        var intentRecords = new List<IntentRecord> { new(intentName, [argument]) };
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [creepId] = intentRecords
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

    private static RoomObjectSnapshot CreateCreep(string id, int energy)
        => new(
            id,
            RoomObjectTypes.Creep,
            "W1N1",
            "shard0",
            "user1",
            10,
            10,
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
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RoomDocumentFields.RoomObject.Store.Energy] = energy
            },
            StoreCapacity: 50,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body:
            [
                new(BodyPartType.Work, ScreepsGameConstants.BodyPartHitPoints, null)
            ],
            IsSpawning: false);

    private static RoomObjectSnapshot CreateConstructionSite(string id, string structureType, int progress, int progressTotal)
        => new(
            id,
            RoomObjectTypes.ConstructionSite,
            "W1N1",
            "shard0",
            "user1",
            11,
            10,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: id,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: structureType,
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
            Progress: progress,
            ProgressTotal: progressTotal);

    private static RoomObjectSnapshot CreateStructure(string id, string type, int hits, int hitsMax)
        => new(
            id,
            type,
            "W1N1",
            "shard0",
            "user1",
            12,
            10,
            Hits: hits,
            HitsMax: hitsMax,
            Fatigue: null,
            TicksToLive: null,
            Name: id,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: type,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            IsSpawning: null);

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
        public Dictionary<string, int> EnergyConstruction { get; } = new(StringComparer.Ordinal);

        public void IncrementEnergyCreeps(string userId, int amount) { }
        public void IncrementCreepsLost(string userId, int bodyParts) { }
        public void IncrementCreepsProduced(string userId, int bodyParts) { }
        public void IncrementSpawnRenewals(string userId) { }
        public void IncrementSpawnRecycles(string userId) { }
        public void IncrementSpawnCreates(string userId) { }
        public void IncrementTombstonesCreated(string userId) { }
        public void IncrementEnergyConstruction(string userId, int amount)
            => EnergyConstruction[userId] = EnergyConstruction.GetValueOrDefault(userId, 0) + amount;
        public void IncrementEnergyHarvested(string userId, int amount) { }
        public void IncrementEnergyControl(string userId, int amount) { }
        public Task FlushAsync(int gameTime, CancellationToken token = default) => Task.CompletedTask;
    }
}
