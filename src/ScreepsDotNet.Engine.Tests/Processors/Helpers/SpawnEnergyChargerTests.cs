namespace ScreepsDotNet.Engine.Tests.Processors.Helpers;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;

public sealed class SpawnEnergyChargerTests
{
    private readonly SpawnEnergyAllocator _allocator = new();
    private readonly TestCreepStatsSink _stats = new();
    private readonly SpawnEnergyCharger _charger;

    public SpawnEnergyChargerTests()
        => _charger = new SpawnEnergyCharger(_allocator);

    [Fact]
    public void TryCharge_UsesSpawnEnergy_WhenAvailable()
    {
        var spawn = CreateSpawn("spawn1", energy: 200);
        var context = CreateContext([spawn]);
        var ledger = new Dictionary<string, int>(StringComparer.Ordinal);

        var result = _charger.TryCharge(context, spawn, 150, null, ledger);

        Assert.True(result.Success);
        var writer = (FakeMutationWriter)context.MutationWriter;
        var patch = Assert.Single(writer.Patches);
        Assert.Equal(spawn.Id, patch.ObjectId);
        Assert.Equal(50, patch.Payload.Store![RoomDocumentFields.RoomObject.Store.Energy]);
        Assert.Equal(50, ledger[spawn.Id]);
        Assert.Equal(150, _stats.LastEnergyIncrement);
    }

    [Fact]
    public void TryCharge_PrioritizesPreferredStructures()
    {
        var spawn = CreateSpawn("spawn1", energy: 50);
        var extension = CreateExtension("ext1", energy: 80);
        var context = CreateContext([spawn, extension]);
        var ledger = new Dictionary<string, int>(StringComparer.Ordinal);

        var result = _charger.TryCharge(context, spawn, 60, [extension.Id], ledger);

        Assert.True(result.Success);
        var writer = (FakeMutationWriter)context.MutationWriter;
        var extensionPatch = Assert.Single(writer.Patches, p => p.ObjectId == extension.Id);
        Assert.Equal(20, extensionPatch.Payload.Store![RoomDocumentFields.RoomObject.Store.Energy]);
        Assert.False(ledger.ContainsKey(spawn.Id));
        Assert.Equal(20, ledger[extension.Id]);
    }

    [Fact]
    public void TryCharge_Fails_WhenInsufficientEnergy()
    {
        var spawn = CreateSpawn("spawn1", energy: 20);
        var context = CreateContext([spawn]);
        var ledger = new Dictionary<string, int>(StringComparer.Ordinal);

        var result = _charger.TryCharge(context, spawn, 50, null, ledger);

        Assert.False(result.Success);
        var writer = (FakeMutationWriter)context.MutationWriter;
        Assert.Empty(writer.Patches);
        Assert.Empty(ledger);
        Assert.Equal(0, _stats.LastEnergyIncrement);
    }

    private RoomProcessorContext CreateContext(IReadOnlyList<RoomObjectSnapshot> objects)
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
            null,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);

        return new RoomProcessorContext(state, new FakeMutationWriter(), _stats);
    }

    private static RoomObjectSnapshot CreateSpawn(string id, int energy)
        => new(
            id,
            RoomObjectTypes.Spawn,
            "W1N1",
            "shard0",
            "user1",
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
            Spawning: null,
            Body: Array.Empty<CreepBodyPartSnapshot>());

    private static RoomObjectSnapshot CreateExtension(string id, int energy)
        => new(
            id,
            RoomObjectTypes.Extension,
            "W1N1",
            "shard0",
            "user1",
            11,
            20,
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
            Body: []);

    private sealed class FakeMutationWriter : IRoomMutationWriter
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

        public void Reset() { }
    }

    private sealed class TestCreepStatsSink : ICreepStatsSink
    {
        public int LastEnergyIncrement { get; private set; }

        public void IncrementEnergyCreeps(string userId, int amount)
            => LastEnergyIncrement += amount;

        public void IncrementCreepsLost(string userId, int bodyParts) { }

        public void IncrementCreepsProduced(string userId, int bodyParts) { }

        public void IncrementSpawnRenewals(string userId) { }

        public void IncrementSpawnRecycles(string userId) { }

        public void IncrementSpawnCreates(string userId) { }

        public void IncrementTombstonesCreated(string userId) { }

        public void IncrementEnergyConstruction(string userId, int amount) { }

        public void IncrementEnergyHarvested(string userId, int amount) { }

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;
    }
}
