namespace ScreepsDotNet.Common.Structures;

using System;
using System.Collections.Generic;

/// <summary>
/// Canonical contract describing the default state of structures when the engine inserts or updates them.
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

public sealed record StructureStoreProfile(IReadOnlyDictionary<string, int> InitialStore, int? StoreCapacity, IReadOnlyDictionary<string, int> StoreCapacityResource)
{
    public static StructureStoreProfile Empty { get; } = new(new Dictionary<string, int>(0), null, new Dictionary<string, int>(0));
}

public sealed record StructureOwnershipProfile(bool RequiresOwner, bool NotifyWhenAttacked);

public sealed record StructureDecayProfile(int? Amount, int? IntervalTicks)
{
    public bool HasDecay => Amount.GetValueOrDefault() > 0 && IntervalTicks.GetValueOrDefault() > 0;
}

public sealed record StructureCooldownProfile(int? CooldownTicks, int? DefaultCooldownTime);

public sealed record StructureRampartProfile(IReadOnlyDictionary<int, int> HitsMaxByControllerLevel);

public sealed record StructureRoadProfile(int BaseHits, int SwampMultiplier, int WallMultiplier, int DecayTime, int DecayAmount);

/// <summary>
/// Registry of structure blueprints shared between driver and engine layers.
/// </summary>
public static class StructureBlueprintRegistry
{
    private static readonly Dictionary<string, StructureBlueprint> Blueprints = new(StringComparer.Ordinal);

    public static bool TryGetBlueprint(string? type, out StructureBlueprint? blueprint)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
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
}
