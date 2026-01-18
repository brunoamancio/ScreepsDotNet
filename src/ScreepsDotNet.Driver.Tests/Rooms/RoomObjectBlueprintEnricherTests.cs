namespace ScreepsDotNet.Driver.Tests.Rooms;

using System.Collections.Generic;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Structures;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Driver.Services.Rooms;

public sealed class RoomObjectBlueprintEnricherTests
{
    private readonly IStructureBlueprintProvider _blueprints = new StructureBlueprintProvider();

    [Fact]
    public void Enrich_RoadWithoutDecay_AddsDecayTime()
    {
        var enricher = new RoomObjectBlueprintEnricher(_blueprints);
        var snapshot = CreateSnapshot(
            RoomObjectTypes.Road,
            store: new Dictionary<string, int>(0, StringComparer.Ordinal),
            decayTime: null);

        var result = enricher.Enrich(snapshot, gameTime: 1_000);

        Assert.Equal(1_000 + ScreepsGameConstants.RoadDecayInterval, result.DecayTime);
    }

    [Fact]
    public void Enrich_SpawnWithoutCooldown_AddsStoreEntry()
    {
        var enricher = new RoomObjectBlueprintEnricher(_blueprints);
        var snapshot = CreateSnapshot(
            RoomObjectTypes.Spawn,
            store: new Dictionary<string, int>(0, StringComparer.Ordinal));

        var result = enricher.Enrich(snapshot, gameTime: null);

        Assert.True(result.Store.ContainsKey(RoomDocumentFields.RoomObject.SpawnCooldownTime));
    }

    private static RoomObjectSnapshot CreateSnapshot(
        string type,
        IReadOnlyDictionary<string, int> store,
        int? decayTime = null)
        => new(
            Id: "obj",
            Type: type,
            RoomName: "W0N0",
            Shard: "shard0",
            UserId: "user",
            X: 10,
            Y: 10,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: type,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: type,
            Store: store,
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(0, StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, PowerEffectSnapshot>(0, StringComparer.Ordinal),
            Spawning: null,
            Body: [],
            IsSpawning: null,
            UserSummoned: null,
            StrongholdId: null,
            DeathTime: null,
            DecayTime: decayTime,
            CreepId: null,
            CreepName: null,
            CreepTicksToLive: null,
            CreepSaying: null,
            ResourceType: null,
            ResourceAmount: null,
            Progress: null,
            ProgressTotal: null,
            ActionLog: null);
}
