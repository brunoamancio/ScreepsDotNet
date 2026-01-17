namespace ScreepsDotNet.Common.Structures;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Common.Constants;

/// <summary>
/// Canonical contract describing the default state of structures when inserted or updated by the engine.
/// </summary>
public sealed record StructureBlueprint(
    string Type,
    StructureHitProfile Hits,
    StructureStoreProfile Store,
    StructureOwnershipProfile Ownership,
    StructureDecayProfile? Decay = null,
    StructureCooldownProfile? Cooldown = null,
    StructureRampartProfile? Rampart = null,
    StructureRoadProfile? Road = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);

public sealed record StructureHitProfile(int Hits, int HitsMax)
{
    public static StructureHitProfile Zero { get; } = new(0, 0);
}

public sealed record StructureStoreProfile(
    IReadOnlyDictionary<string, int> InitialStore,
    int? StoreCapacity,
    IReadOnlyDictionary<string, int> StoreCapacityResource,
    IReadOnlyDictionary<int, int>? ControllerLevelCapacity = null)
{
    public static StructureStoreProfile Empty { get; } = new(new Dictionary<string, int>(0, StringComparer.Ordinal), null, new Dictionary<string, int>(0, StringComparer.Ordinal));
}

public sealed record StructureOwnershipProfile(bool RequiresOwner, bool NotifyWhenAttacked);

public sealed record StructureDecayProfile(int? Amount, int? IntervalTicks, int? OwnedIntervalTicks = null)
{
    public bool HasDecay =>
        Amount.GetValueOrDefault() > 0 &&
        (IntervalTicks.GetValueOrDefault() > 0 || OwnedIntervalTicks.GetValueOrDefault() > 0);
}

public sealed record StructureCooldownProfile(int? InitialCooldown, int? CooldownTimeOffset);

public sealed record StructureRampartProfile(IReadOnlyDictionary<int, int> HitsMaxByControllerLevel);

public sealed record StructureRoadProfile(int BaseHits, int SwampMultiplier, int WallMultiplier, int DecayTime, int DecayAmount);

/// <summary>
/// Registry of structure blueprints shared between driver and engine layers.
/// </summary>
public static class StructureBlueprintRegistry
{
    private static readonly Dictionary<string, StructureBlueprint> Blueprints = new(StringComparer.Ordinal);

    private const string Energy = RoomDocumentFields.RoomObject.Store.Energy;
    private const string Power = ResourceTypes.Power;
    private const string Ghodium = ResourceTypes.Ghodium;

    static StructureBlueprintRegistry()
        => RegisterDefaults();

    public static bool TryGetBlueprint(string? type, out StructureBlueprint? blueprint)
    {
        if (string.IsNullOrWhiteSpace(type)) {
            blueprint = null;
            return false;
        }

        return Blueprints.TryGetValue(type, out blueprint);
    }

    public static IReadOnlyDictionary<string, StructureBlueprint> GetAll()
        => Blueprints;

    public static void RegisterBlueprint(StructureBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        if (string.IsNullOrWhiteSpace(blueprint.Type))
            throw new ArgumentException("Blueprint type is required.", nameof(blueprint));

        Blueprints[blueprint.Type] = blueprint;
    }

    private static void RegisterDefaults()
    {
        RegisterBlueprint(CreateSpawnBlueprint());
        RegisterBlueprint(CreateExtensionBlueprint());
        RegisterBlueprint(CreateRoadBlueprint());
        RegisterBlueprint(CreateConstructedWallBlueprint());
        RegisterBlueprint(CreateRampartBlueprint());
        RegisterBlueprint(CreateLinkBlueprint());
        RegisterBlueprint(CreateStorageBlueprint());
        RegisterBlueprint(CreateTowerBlueprint());
        RegisterBlueprint(CreateObserverBlueprint());
        RegisterBlueprint(CreatePowerSpawnBlueprint());
        RegisterBlueprint(CreateExtractorBlueprint());
        RegisterBlueprint(CreateLabBlueprint());
        RegisterBlueprint(CreateTerminalBlueprint());
        RegisterBlueprint(CreateContainerBlueprint());
        RegisterBlueprint(CreateNukerBlueprint());
        RegisterBlueprint(CreateFactoryBlueprint());
    }

    private static StructureBlueprint CreateSpawnBlueprint()
        => new(
            RoomObjectTypes.Spawn,
            new StructureHitProfile(ScreepsGameConstants.SpawnHits, ScreepsGameConstants.SpawnHits),
            new StructureStoreProfile(
                CreateResourceDictionary((Energy, 0)),
                null,
                CreateResourceDictionary((Energy, ScreepsGameConstants.SpawnEnergyCapacity))),
            new StructureOwnershipProfile(RequiresOwner: true, NotifyWhenAttacked: true));

    private static StructureBlueprint CreateExtensionBlueprint()
        => new(
            RoomObjectTypes.Extension,
            new StructureHitProfile(ScreepsGameConstants.ExtensionHits, ScreepsGameConstants.ExtensionHits),
            new StructureStoreProfile(
                CreateResourceDictionary((Energy, 0)),
                null,
                CreateResourceDictionary((Energy, 0)),
                ScreepsGameConstants.ExtensionEnergyCapacityByControllerLevel),
            new StructureOwnershipProfile(RequiresOwner: true, NotifyWhenAttacked: false));

    private static StructureBlueprint CreateRoadBlueprint()
        => new(
            RoomObjectTypes.Road,
            new StructureHitProfile(ScreepsGameConstants.RoadHits, ScreepsGameConstants.RoadHits),
            StructureStoreProfile.Empty,
            new StructureOwnershipProfile(RequiresOwner: false, NotifyWhenAttacked: false),
            Decay: new StructureDecayProfile(
                ScreepsGameConstants.RoadDecayAmount,
                ScreepsGameConstants.RoadDecayInterval),
            Road: new StructureRoadProfile(
                ScreepsGameConstants.RoadHits,
                ScreepsGameConstants.RoadSwampMultiplier,
                ScreepsGameConstants.RoadWallMultiplier,
                ScreepsGameConstants.RoadDecayInterval,
                ScreepsGameConstants.RoadDecayAmount));

    private static StructureBlueprint CreateConstructedWallBlueprint()
        => new(
            RoomObjectTypes.ConstructedWall,
            new StructureHitProfile(ScreepsGameConstants.WallHits, ScreepsGameConstants.WallHitsMax),
            StructureStoreProfile.Empty,
            new StructureOwnershipProfile(RequiresOwner: false, NotifyWhenAttacked: false));

    private static StructureBlueprint CreateRampartBlueprint()
        => new(
            RoomObjectTypes.Rampart,
            new StructureHitProfile(ScreepsGameConstants.RampartHits, ScreepsGameConstants.RampartHits),
            StructureStoreProfile.Empty,
            new StructureOwnershipProfile(RequiresOwner: true, NotifyWhenAttacked: false),
            Decay: new StructureDecayProfile(
                ScreepsGameConstants.RampartDecayAmount,
                ScreepsGameConstants.RampartDecayInterval),
            Rampart: new StructureRampartProfile(ScreepsGameConstants.RampartHitsMaxByControllerLevel));

    private static StructureBlueprint CreateLinkBlueprint()
        => new(
            RoomObjectTypes.Link,
            new StructureHitProfile(ScreepsGameConstants.LinkHits, ScreepsGameConstants.LinkHitsMax),
            new StructureStoreProfile(
                CreateResourceDictionary((Energy, 0)),
                null,
                CreateResourceDictionary((Energy, ScreepsGameConstants.LinkCapacity))),
            new StructureOwnershipProfile(RequiresOwner: true, NotifyWhenAttacked: false),
            Cooldown: new StructureCooldownProfile(InitialCooldown: 0, CooldownTimeOffset: null));

    private static StructureBlueprint CreateStorageBlueprint()
        => new(
            RoomObjectTypes.Storage,
            new StructureHitProfile(ScreepsGameConstants.StorageHits, ScreepsGameConstants.StorageHits),
            new StructureStoreProfile(
                CreateResourceDictionary((Energy, 0)),
                ScreepsGameConstants.StorageCapacity,
                CreateResourceDictionary()),
            new StructureOwnershipProfile(RequiresOwner: true, NotifyWhenAttacked: false));

    private static StructureBlueprint CreateTowerBlueprint()
        => new(
            RoomObjectTypes.Tower,
            new StructureHitProfile(ScreepsGameConstants.TowerHits, ScreepsGameConstants.TowerHits),
            new StructureStoreProfile(
                CreateResourceDictionary((Energy, 0)),
                null,
                CreateResourceDictionary((Energy, ScreepsGameConstants.TowerCapacity))),
            new StructureOwnershipProfile(RequiresOwner: true, NotifyWhenAttacked: false));

    private static StructureBlueprint CreateObserverBlueprint()
        => new(
            RoomObjectTypes.Observer,
            new StructureHitProfile(ScreepsGameConstants.ObserverHits, ScreepsGameConstants.ObserverHits),
            StructureStoreProfile.Empty,
            new StructureOwnershipProfile(RequiresOwner: true, NotifyWhenAttacked: false));

    private static StructureBlueprint CreatePowerSpawnBlueprint()
        => new(
            RoomObjectTypes.PowerSpawn,
            new StructureHitProfile(ScreepsGameConstants.PowerSpawnHits, ScreepsGameConstants.PowerSpawnHits),
            new StructureStoreProfile(
                CreateResourceDictionary((Energy, 0), (Power, 0)),
                null,
                CreateResourceDictionary(
                    (Energy, ScreepsGameConstants.PowerSpawnEnergyCapacity),
                    (Power, ScreepsGameConstants.PowerSpawnPowerCapacity))),
            new StructureOwnershipProfile(RequiresOwner: true, NotifyWhenAttacked: false));

    private static StructureBlueprint CreateExtractorBlueprint()
        => new(
            RoomObjectTypes.Extractor,
            new StructureHitProfile(ScreepsGameConstants.ExtractorHits, ScreepsGameConstants.ExtractorHits),
            StructureStoreProfile.Empty,
            new StructureOwnershipProfile(RequiresOwner: true, NotifyWhenAttacked: false),
            Cooldown: new StructureCooldownProfile(InitialCooldown: 0, CooldownTimeOffset: ScreepsGameConstants.ExtractorCooldown));

    private static StructureBlueprint CreateLabBlueprint()
        => new(
            RoomObjectTypes.Lab,
            new StructureHitProfile(ScreepsGameConstants.LabHits, ScreepsGameConstants.LabHits),
            new StructureStoreProfile(
                CreateResourceDictionary((Energy, 0)),
                ScreepsGameConstants.LabEnergyCapacity + ScreepsGameConstants.LabMineralCapacity,
                CreateResourceDictionary((Energy, ScreepsGameConstants.LabEnergyCapacity))),
            new StructureOwnershipProfile(RequiresOwner: true, NotifyWhenAttacked: false),
            Cooldown: new StructureCooldownProfile(InitialCooldown: 0, CooldownTimeOffset: null),
            Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["labMineralCapacity"] = ScreepsGameConstants.LabMineralCapacity
            });

    private static StructureBlueprint CreateTerminalBlueprint()
        => new(
            RoomObjectTypes.Terminal,
            new StructureHitProfile(ScreepsGameConstants.TerminalHits, ScreepsGameConstants.TerminalHits),
            new StructureStoreProfile(
                CreateResourceDictionary((Energy, 0)),
                ScreepsGameConstants.TerminalCapacity,
                CreateResourceDictionary()),
            new StructureOwnershipProfile(RequiresOwner: true, NotifyWhenAttacked: false),
            Cooldown: new StructureCooldownProfile(InitialCooldown: 0, CooldownTimeOffset: ScreepsGameConstants.TerminalCooldown));

    private static StructureBlueprint CreateContainerBlueprint()
        => new(
            RoomObjectTypes.Container,
            new StructureHitProfile(ScreepsGameConstants.ContainerHits, ScreepsGameConstants.ContainerHits),
            new StructureStoreProfile(
                CreateResourceDictionary((Energy, 0)),
                ScreepsGameConstants.ContainerCapacity,
                CreateResourceDictionary()),
            new StructureOwnershipProfile(RequiresOwner: false, NotifyWhenAttacked: false),
            Decay: new StructureDecayProfile(
                ScreepsGameConstants.ContainerDecayAmount,
                ScreepsGameConstants.ContainerDecayInterval,
                ScreepsGameConstants.ContainerDecayOwnedInterval));

    private static StructureBlueprint CreateNukerBlueprint()
        => new(
            RoomObjectTypes.Nuker,
            new StructureHitProfile(ScreepsGameConstants.NukerHits, ScreepsGameConstants.NukerHits),
            new StructureStoreProfile(
                CreateResourceDictionary((Energy, 0), (Ghodium, 0)),
                null,
                CreateResourceDictionary(
                    (Energy, ScreepsGameConstants.NukerEnergyCapacity),
                    (ResourceTypes.Ghodium, ScreepsGameConstants.NukerGhodiumCapacity))),
            new StructureOwnershipProfile(RequiresOwner: true, NotifyWhenAttacked: false),
            Cooldown: new StructureCooldownProfile(InitialCooldown: null, CooldownTimeOffset: ScreepsGameConstants.NukerCooldown));

    private static StructureBlueprint CreateFactoryBlueprint()
        => new(
            RoomObjectTypes.Factory,
            new StructureHitProfile(ScreepsGameConstants.FactoryHits, ScreepsGameConstants.FactoryHits),
            new StructureStoreProfile(
                CreateResourceDictionary((Energy, 0)),
                ScreepsGameConstants.FactoryCapacity,
                CreateResourceDictionary()),
            new StructureOwnershipProfile(RequiresOwner: true, NotifyWhenAttacked: false),
            Cooldown: new StructureCooldownProfile(InitialCooldown: 0, CooldownTimeOffset: null));

    private static IReadOnlyDictionary<string, int> CreateResourceDictionary(params (string Resource, int Amount)[] entries)
    {
        if (entries.Length == 0)
            return new Dictionary<string, int>(0, StringComparer.Ordinal);

        var result = new Dictionary<string, int>(entries.Length, StringComparer.Ordinal);
        foreach (var (resource, amount) in entries) {
            if (string.IsNullOrWhiteSpace(resource))
                continue;

            result[resource] = amount;
        }

        return result;
    }
}
