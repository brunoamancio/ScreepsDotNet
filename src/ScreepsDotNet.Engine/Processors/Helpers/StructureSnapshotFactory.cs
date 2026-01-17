namespace ScreepsDotNet.Engine.Processors.Helpers;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Structures;
using ScreepsDotNet.Driver.Contracts;

internal interface IStructureSnapshotFactory
{
    RoomObjectSnapshot CreateStructureSnapshot(StructureBlueprint blueprint, StructureCreationOptions options);
}

internal sealed record StructureCreationOptions(
    string RoomName,
    string? Shard,
    string? UserId,
    int X,
    int Y,
    int GameTime,
    int? ControllerLevel,
    bool OnSwamp,
    bool OnWall,
    string? ObjectId = null,
    string? Name = null);

internal sealed class StructureSnapshotFactory : IStructureSnapshotFactory
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    public RoomObjectSnapshot CreateStructureSnapshot(StructureBlueprint blueprint, StructureCreationOptions options)
    {
        ArgumentNullException.ThrowIfNull(blueprint);

        var objectId = string.IsNullOrWhiteSpace(options.ObjectId)
            ? Guid.NewGuid().ToString("N")
            : options.ObjectId!;

        var (hits, hitsMax) = CalculateHitProfile(blueprint, options);
        var (store, storeCapacity, storeCapacityResource) = BuildStoreProfiles(blueprint, options);
        var decayTime = CalculateDecayTime(blueprint, options);

        return new RoomObjectSnapshot(
            objectId,
            blueprint.Type,
            options.RoomName,
            options.Shard,
            options.UserId,
            options.X,
            options.Y,
            hits,
            hitsMax,
            Fatigue: null,
            TicksToLive: null,
            Name: options.Name,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: blueprint.Type,
            Store: store,
            StoreCapacity: storeCapacity,
            StoreCapacityResource: storeCapacityResource,
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(0, Comparer),
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
            ActionLog: null);
    }

    private static (int? Hits, int? HitsMax) CalculateHitProfile(StructureBlueprint blueprint, StructureCreationOptions options)
    {
        var hits = blueprint.Hits.Hits;
        var hitsMax = blueprint.Hits.HitsMax;

        if (blueprint.Road is not null)
        {
            hits = blueprint.Road.BaseHits;
            if (options.OnSwamp)
                hits *= blueprint.Road.SwampMultiplier;
            if (options.OnWall)
                hits *= blueprint.Road.WallMultiplier;
            hitsMax = hits;
        }

        if (blueprint.Rampart is not null && options.ControllerLevel.HasValue)
        {
            var level = options.ControllerLevel.Value;
            if (blueprint.Rampart.HitsMaxByControllerLevel.TryGetValue(level, out var rampartHitsMax) && rampartHitsMax > 0)
                hitsMax = rampartHitsMax;
        }

        return (hits, hitsMax);
    }

    private static (IReadOnlyDictionary<string, int> Store, int? StoreCapacity, IReadOnlyDictionary<string, int> StoreCapacityResource) BuildStoreProfiles(
        StructureBlueprint blueprint,
        StructureCreationOptions options)
    {
        var store = blueprint.Store.InitialStore.Count == 0
            ? new Dictionary<string, int>(0, Comparer)
            : new Dictionary<string, int>(blueprint.Store.InitialStore, Comparer);

        var storeCapacityResource = blueprint.Store.StoreCapacityResource.Count == 0
            ? new Dictionary<string, int>(0, Comparer)
            : new Dictionary<string, int>(blueprint.Store.StoreCapacityResource, Comparer);

        if (blueprint.Store.ControllerLevelCapacity is not null && options.ControllerLevel.HasValue)
        {
            var level = options.ControllerLevel.Value;
            if (blueprint.Store.ControllerLevelCapacity.TryGetValue(level, out var capacity))
                storeCapacityResource[RoomDocumentFields.RoomObject.Store.Energy] = capacity;
        }

        return (store, blueprint.Store.StoreCapacity, storeCapacityResource);
    }

    private static int? CalculateDecayTime(StructureBlueprint blueprint, StructureCreationOptions options)
    {
        if (blueprint.Decay is not { HasDecay: true })
            return null;

        var interval = blueprint.Decay.OwnedIntervalTicks.HasValue && !string.IsNullOrWhiteSpace(options.UserId)
            ? blueprint.Decay.OwnedIntervalTicks
            : blueprint.Decay.IntervalTicks;

        return interval.HasValue ? options.GameTime + interval.Value : null;
    }
}
