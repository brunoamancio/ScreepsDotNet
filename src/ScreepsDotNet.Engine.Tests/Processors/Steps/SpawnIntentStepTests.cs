namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

using System;
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

public sealed class SpawnIntentStepTests
{
    private readonly SpawnIntentStep _step;
    private readonly TestCreepStatsSink _statsSink;

    public SpawnIntentStepTests()
    {
        var bodyHelper = new BodyAnalysisHelper();
        var parser = new SpawnIntentParser(bodyHelper);
        var stateReader = new SpawnStateReader();
        var energyAllocator = new SpawnEnergyAllocator();
        _statsSink = new TestCreepStatsSink();
        var energyCharger = new SpawnEnergyCharger(energyAllocator);
        var deathProcessor = new CreepDeathProcessor();
        _step = new SpawnIntentStep(parser, stateReader, energyCharger, deathProcessor);
    }

    [Fact]
    public async Task ExecuteAsync_StartsSpawn_WhenCreateIntentValid()
    {
        var spawn = CreateSpawn("spawn1", energy: 200);
        var extension = CreateExtension("ext1", energy: 100);
        var envelope = new SpawnIntentEnvelope(
            new CreateCreepIntent("Worker1", [BodyPartType.Move, BodyPartType.Work], [Direction.Top], [extension.Id]),
            null,
            null,
            null,
            false);

        var context = CreateContext([spawn, extension], CreateIntents(spawn.Id, envelope), statsSink: _statsSink);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var spawnSpawningPatch = writer.Patches.First(p => p.ObjectId == spawn.Id && p.Payload.Spawning is not null);
        Assert.Equal("Worker1", spawnSpawningPatch.Payload.Spawning!.Name);
        Assert.Equal(106, spawnSpawningPatch.Payload.Spawning.SpawnTime);

        var spawnStorePatch = writer.Patches.First(p => p.ObjectId == spawn.Id && p.Payload.Store is not null);
        Assert.Equal(150, spawnStorePatch.Payload.Store![RoomDocumentFields.RoomObject.Store.Energy]);

        var extensionPatch = writer.Patches.Single(p => p.ObjectId == extension.Id);
        Assert.Equal(0, extensionPatch.Payload.Store![RoomDocumentFields.RoomObject.Store.Energy]);

        var placeholder = Assert.Single(writer.Upserts);
        Assert.Equal(RoomObjectTypes.Creep, placeholder.Type);
        Assert.Equal("Worker1", placeholder.Name);
        Assert.True(placeholder.IsSpawning);
        Assert.Equal(0, placeholder.Store.GetValueOrDefault(RoomDocumentFields.RoomObject.Store.Energy));
        Assert.Equal(2 * ScreepsGameConstants.BodyPartHitPoints, placeholder.Hits);
        Assert.Equal(2, _statsSink.CreepsProduced);
        Assert.Equal(1, _statsSink.SpawnCreates);
    }

    [Fact]
    public async Task ExecuteAsync_SetsDirections_WhenSpawnIsBusy()
    {
        var spawning = new RoomSpawnSpawningSnapshot("Worker1", 15, 115, [Direction.Top]);
        var spawn = CreateSpawn("spawn1", energy: 200, spawning: spawning);
        var envelope = new SpawnIntentEnvelope(
            null,
            null,
            null,
            new SetSpawnDirectionsIntent([Direction.TopLeft, Direction.Left]),
            false);

        var context = CreateContext([spawn], CreateIntents(spawn.Id, envelope), statsSink: _statsSink);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var patch = writer.Patches.Single(p => p.Payload.Spawning is not null);
        Assert.Equal([Direction.TopLeft, Direction.Left], patch.Payload.Spawning!.Directions);
    }

    [Fact]
    public async Task ExecuteAsync_CancelsSpawning_WhenRequested()
    {
        var spawning = new RoomSpawnSpawningSnapshot("Worker1", 15, 115, [Direction.Top]);
        var spawn = CreateSpawn("spawn1", energy: 200, spawning: spawning);
        var placeholder = CreatePlaceholderCreep("creep-placeholder", spawn);
        var envelope = new SpawnIntentEnvelope(
            null,
            null,
            null,
            null,
            true);

        var context = CreateContext([spawn, placeholder], CreateIntents(spawn.Id, envelope), statsSink: _statsSink);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var patch = writer.Patches.Single();
        Assert.True(patch.Payload.ClearSpawning);
        Assert.Contains(placeholder.Id, writer.Removals);
    }

    [Fact]
    public async Task ExecuteAsync_RenewsCreep_WhenAdjacent()
    {
        var spawn = CreateSpawn("spawn1", energy: 200);
        var creep = CreateCreep("creep1", spawn.X + 1, spawn.Y, [BodyPartType.Move, BodyPartType.Work, BodyPartType.Carry], ticksToLive: 100);
        var envelope = new SpawnIntentEnvelope(
            null,
            new RenewCreepIntent(creep.Id),
            null,
            null,
            false);

        var context = CreateContext([spawn, creep], CreateIntents(spawn.Id, envelope), statsSink: _statsSink);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var creepPatch = writer.Patches.Single(p => p.ObjectId == creep.Id);
        Assert.Equal(300, creepPatch.Payload.TicksToLive);
        Assert.NotNull(creepPatch.Payload.ActionLog);
        Assert.Equal(spawn.X, creepPatch.Payload.ActionLog!.Healed!.X);
        Assert.Equal(spawn.Y, creepPatch.Payload.ActionLog!.Healed!.Y);

        var spawnPatch = writer.Patches.First(p => p.ObjectId == spawn.Id && p.Payload.Store is not null);
        Assert.Equal(173, spawnPatch.Payload.Store![RoomDocumentFields.RoomObject.Store.Energy]);
        Assert.Equal(1, _statsSink.SpawnRenewals);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRenew_WhenNotAdjacent()
    {
        var spawn = CreateSpawn("spawn1", energy: 200);
        var creep = CreateCreep("creep1", spawn.X + 3, spawn.Y, [BodyPartType.Move], ticksToLive: 100);
        var envelope = new SpawnIntentEnvelope(
            null,
            new RenewCreepIntent(creep.Id),
            null,
            null,
            false);

        var context = CreateContext([spawn, creep], CreateIntents(spawn.Id, envelope), statsSink: _statsSink);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(writer.Patches, p => p.ObjectId == creep.Id);
    }

    [Fact]
    public async Task ExecuteAsync_RenewDropsOverflow_WhenBoostsCleared()
    {
        var spawn = CreateSpawn("spawn1", energy: 300);
        var baseCreep = CreateCreep("creep1", spawn.X + 1, spawn.Y, [BodyPartType.Carry], ticksToLive: 100);
        var boostedBody = new[]
        {
            new CreepBodyPartSnapshot(BodyPartType.Carry, ScreepsGameConstants.BodyPartHitPoints, "UH2O")
        };
        var creepStore = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [RoomDocumentFields.RoomObject.Store.Energy] = 100
        };
        var creep = baseCreep with
        {
            Body = boostedBody,
            Store = creepStore,
            StoreCapacity = 100
        };

        var envelope = new SpawnIntentEnvelope(
            null,
            new RenewCreepIntent(creep.Id),
            null,
            null,
            false);

        var context = CreateContext([spawn, creep], CreateIntents(spawn.Id, envelope), statsSink: _statsSink);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var creepPatch = writer.Patches.Single(p => p.ObjectId == creep.Id);
        Assert.NotNull(creepPatch.Payload.Body);
        Assert.Equal(50, creepPatch.Payload.StoreCapacity);
        Assert.Equal(50, creepPatch.Payload.Store![RoomDocumentFields.RoomObject.Store.Energy]);
        Assert.NotNull(creepPatch.Payload.ActionLog);
        Assert.Equal(spawn.X, creepPatch.Payload.ActionLog!.Healed!.X);
        Assert.Equal(spawn.Y, creepPatch.Payload.ActionLog!.Healed!.Y);
        Assert.Equal(1, _statsSink.SpawnRenewals);

        var drop = Assert.Single(writer.Upserts, u => u.Type == RoomObjectTypes.Resource);
        Assert.Equal(ResourceTypes.Energy, drop.ResourceType);
        Assert.Equal(50, drop.ResourceAmount);
    }

    [Fact]
    public async Task ExecuteAsync_RecyclesCreep_WhenRequested()
    {
        var spawn = CreateSpawn("spawn1", energy: 200);
        var creep = CreateCreep("creep1", spawn.X, spawn.Y + 1, [BodyPartType.Move]);
        var envelope = new SpawnIntentEnvelope(
            null,
            null,
            new RecycleCreepIntent(creep.Id),
            null,
            false);

        var context = CreateContext([spawn, creep], CreateIntents(spawn.Id, envelope), statsSink: _statsSink);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Contains(creep.Id, writer.Removals);

        var actionLogPatch = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.ActionLog is not null);
        Assert.Equal(100, actionLogPatch.Payload.ActionLog!.Die!.Time);

        var tombstone = Assert.Single(writer.Upserts);
        Assert.Equal(RoomObjectTypes.Tombstone, tombstone.Type);
        Assert.Equal(creep.Name, tombstone.CreepName);
        Assert.Equal(3, tombstone.Store.GetValueOrDefault(RoomDocumentFields.RoomObject.Store.Energy));

        var spawnPatch = writer.Patches.Single(p => p.ObjectId == spawn.Id && p.Payload.Store is not null);
        Assert.Equal(203, spawnPatch.Payload.Store![RoomDocumentFields.RoomObject.Store.Energy]);
        Assert.Equal(1, _statsSink.SpawnRecycles);
        Assert.Equal(1, _statsSink.TombstonesCreated);
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

        return new RoomProcessorContext(state, new FakeMutationWriter(), statsSink ?? new NullCreepStatsSink());
    }

    private static RoomIntentSnapshot CreateIntents(string spawnId, SpawnIntentEnvelope envelope, string userId = "user1")
    {
        var spawnIntents = new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal)
        {
            [spawnId] = envelope
        };

        var intentEnvelope = new IntentEnvelope(
            userId,
            new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal),
            spawnIntents,
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal));

        return new RoomIntentSnapshot(
            "W1N1",
            "shard0",
            new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
            {
                [userId] = intentEnvelope
            });
    }

    private static RoomObjectSnapshot CreateSpawn(
        string id,
        int energy,
        string userId = "user1",
        RoomSpawnSpawningSnapshot? spawning = null)
        => new(
            id,
            RoomObjectTypes.Spawn,
            "W1N1",
            "shard0",
            userId,
            10,
            20,
            Hits: 5000,
            HitsMax: 5000,
            Fatigue: null,
            TicksToLive: null,
            Name: "Spawn1",
            Level: 1,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Spawn,
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RoomDocumentFields.RoomObject.Store.Energy] = energy
            },
            StoreCapacity: 300,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(),
            Spawning: spawning,
            Body: Array.Empty<CreepBodyPartSnapshot>());

    private static RoomObjectSnapshot CreateExtension(string id, int energy, string userId = "user1")
        => new(
            id,
            RoomObjectTypes.Extension,
            "W1N1",
            "shard0",
            userId,
            12,
            21,
            Hits: 1000,
            HitsMax: 1000,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Extension,
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RoomDocumentFields.RoomObject.Store.Energy] = energy
            },
            StoreCapacity: 50,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(),
            Spawning: null,
            Body: Array.Empty<CreepBodyPartSnapshot>());

    private static RoomObjectSnapshot CreateCreep(
        string id,
        int x,
        int y,
        BodyPartType[] body,
        int ticksToLive = 100,
        string userId = "user1")
        => new(
            id,
            RoomObjectTypes.Creep,
            "W1N1",
            "shard0",
            userId,
            x,
            y,
            Hits: 300,
            HitsMax: 300,
            Fatigue: 0,
            TicksToLive: ticksToLive,
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
            Effects: new Dictionary<string, object?>(),
            Spawning: null,
            Body: CreateBodySnapshot(body));

    private static RoomObjectSnapshot CreatePlaceholderCreep(string id, RoomObjectSnapshot spawn)
        => new(
            id,
            RoomObjectTypes.Creep,
            spawn.RoomName,
            spawn.Shard,
            spawn.UserId,
            spawn.X,
            spawn.Y,
            Hits: 0,
            HitsMax: 0,
            Fatigue: 0,
            TicksToLive: null,
            Name: spawn.Spawning?.Name ?? id,
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
            Effects: new Dictionary<string, object?>(),
            Spawning: null,
            Body: Array.Empty<CreepBodyPartSnapshot>(),
            IsSpawning: true);

    private static IReadOnlyList<CreepBodyPartSnapshot> CreateBodySnapshot(BodyPartType[] parts)
    {
        if (parts.Length == 0)
            return Array.Empty<CreepBodyPartSnapshot>();

        var result = new CreepBodyPartSnapshot[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            result[i] = new CreepBodyPartSnapshot(parts[i], ScreepsGameConstants.BodyPartHitPoints, null);

        return result;
    }

    private sealed class TestCreepStatsSink : ICreepStatsSink
    {
        public int EnergyCreeps { get; private set; }
        public int CreepsLost { get; private set; }
        public int CreepsProduced { get; private set; }
        public int SpawnRenewals { get; private set; }
        public int SpawnRecycles { get; private set; }
        public int SpawnCreates { get; private set; }
        public int TombstonesCreated { get; private set; }
        public int EnergyConstruction { get; private set; }
        public int EnergyHarvested { get; private set; }

        public void IncrementEnergyCreeps(string userId, int amount)
            => EnergyCreeps += amount;

        public void IncrementCreepsLost(string userId, int bodyParts)
            => CreepsLost += bodyParts;

        public void IncrementCreepsProduced(string userId, int bodyParts)
            => CreepsProduced += bodyParts;

        public void IncrementSpawnRenewals(string userId)
            => SpawnRenewals++;

        public void IncrementSpawnRecycles(string userId)
            => SpawnRecycles++;

        public void IncrementSpawnCreates(string userId)
            => SpawnCreates++;

        public void IncrementTombstonesCreated(string userId)
            => TombstonesCreated++;

        public void IncrementEnergyConstruction(string userId, int amount)
            => EnergyConstruction += amount;

        public void IncrementEnergyHarvested(string userId, int amount)
            => EnergyHarvested += amount;

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;
    }

    private sealed class FakeMutationWriter : IRoomMutationWriter
    {
        public List<(string ObjectId, RoomObjectPatchPayload Payload)> Patches { get; } = [];
        public List<string> Removals { get; } = [];
        public List<RoomObjectSnapshot> Upserts { get; } = [];

        public void Upsert(RoomObjectSnapshot document)
            => Upserts.Add(document);

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
}
