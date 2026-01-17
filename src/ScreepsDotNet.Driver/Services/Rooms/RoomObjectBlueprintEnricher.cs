namespace ScreepsDotNet.Driver.Services.Rooms;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Structures;
using ScreepsDotNet.Driver.Contracts;

internal interface IRoomObjectBlueprintEnricher
{
    RoomObjectSnapshot Enrich(RoomObjectSnapshot snapshot, int? gameTime = null);
}

internal sealed class RoomObjectBlueprintEnricher(IStructureBlueprintProvider blueprintProvider) : IRoomObjectBlueprintEnricher
{
    public RoomObjectSnapshot Enrich(RoomObjectSnapshot snapshot, int? gameTime = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!blueprintProvider.TryGet(snapshot.Type, out var blueprint) || blueprint is null)
            return snapshot;

        var hits = snapshot.Hits ?? blueprint.Hits.Hits;
        var hitsMax = snapshot.HitsMax ?? blueprint.Hits.HitsMax;
        var storeCapacity = snapshot.StoreCapacity ?? blueprint.Store.StoreCapacity;

        var updatedStore = EnsureResourceEntries(snapshot.Store, blueprint.Store.InitialStore, out var storeChanged);
        updatedStore = EnsureSpawnCooldownEntry(blueprint, updatedStore, out var cooldownChanged);

        var updatedCapacity = EnsureResourceEntries(snapshot.StoreCapacityResource, blueprint.Store.StoreCapacityResource, out var capacityChanged);
        var decayTime = snapshot.DecayTime ?? CalculateDecayTime(snapshot, blueprint, gameTime);

        if (!storeChanged && !cooldownChanged && !capacityChanged && hits == snapshot.Hits && hitsMax == snapshot.HitsMax && storeCapacity == snapshot.StoreCapacity && decayTime == snapshot.DecayTime)
            return snapshot;

        return snapshot with
        {
            Hits = hits,
            HitsMax = hitsMax,
            StoreCapacity = storeCapacity,
            Store = updatedStore,
            StoreCapacityResource = updatedCapacity,
            DecayTime = decayTime
        };
    }

    private static IReadOnlyDictionary<string, int> EnsureResourceEntries(IReadOnlyDictionary<string, int> existing, IReadOnlyDictionary<string, int> defaults, out bool changed)
    {
        changed = false;
        if (defaults.Count == 0)
            return existing;

        Dictionary<string, int>? buffer = null;
        foreach (var (key, value) in defaults) {
            if (existing.ContainsKey(key))
                continue;

            buffer ??= new Dictionary<string, int>(existing, StringComparer.Ordinal);
            buffer[key] = value;
            changed = true;
        }

        return buffer ?? existing;
    }

    private static IReadOnlyDictionary<string, int> EnsureSpawnCooldownEntry(
        StructureBlueprint blueprint,
        IReadOnlyDictionary<string, int> existing,
        out bool changed)
    {
        changed = false;
        if (!RequiresSpawnCooldownEntry(blueprint.Type) || existing.ContainsKey(RoomDocumentFields.RoomObject.SpawnCooldownTime))
            return existing;

        var buffer = new Dictionary<string, int>(existing, StringComparer.Ordinal)
        {
            [RoomDocumentFields.RoomObject.SpawnCooldownTime] = blueprint.Cooldown?.InitialCooldown ?? 0
        };
        changed = true;
        return buffer;
    }

    private static bool RequiresSpawnCooldownEntry(string type)
        => string.Equals(type, RoomObjectTypes.Spawn, StringComparison.Ordinal) ||
           string.Equals(type, RoomObjectTypes.PowerSpawn, StringComparison.Ordinal);

    private static int? CalculateDecayTime(RoomObjectSnapshot snapshot, StructureBlueprint blueprint, int? gameTime)
    {
        if (snapshot.DecayTime.HasValue || !gameTime.HasValue)
            return snapshot.DecayTime;

        var decay = blueprint.Decay;
        if (decay is not { HasDecay: true })
            return null;

        var ownedInterval = string.IsNullOrWhiteSpace(snapshot.UserId) ? null : decay.OwnedIntervalTicks;
        var interval = ownedInterval ?? decay.IntervalTicks;
        if (interval is not > 0)
            return null;

        return gameTime.Value + interval.Value;
    }
}
