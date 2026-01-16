namespace ScreepsDotNet.Driver.Services.Rooms;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Common.Structures;
using ScreepsDotNet.Driver.Contracts;

internal interface IRoomObjectBlueprintEnricher
{
    RoomObjectSnapshot Enrich(RoomObjectSnapshot snapshot);
}

internal sealed class RoomObjectBlueprintEnricher(IStructureBlueprintProvider blueprintProvider) : IRoomObjectBlueprintEnricher
{
    public RoomObjectSnapshot Enrich(RoomObjectSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!blueprintProvider.TryGet(snapshot.Type, out var blueprint) || blueprint is null)
            return snapshot;

        var hits = snapshot.Hits ?? blueprint.Hits.Hits;
        var hitsMax = snapshot.HitsMax ?? blueprint.Hits.HitsMax;
        var storeCapacity = snapshot.StoreCapacity ?? blueprint.Store.StoreCapacity;

        var updatedStore = EnsureResourceEntries(snapshot.Store, blueprint.Store.InitialStore, out var storeChanged);
        var updatedCapacity = EnsureResourceEntries(snapshot.StoreCapacityResource, blueprint.Store.StoreCapacityResource, out var capacityChanged);

        if (!storeChanged && !capacityChanged && hits == snapshot.Hits && hitsMax == snapshot.HitsMax && storeCapacity == snapshot.StoreCapacity)
            return snapshot;

        return snapshot with
        {
            Hits = hits,
            HitsMax = hitsMax,
            StoreCapacity = storeCapacity,
            Store = updatedStore,
            StoreCapacityResource = updatedCapacity
        };
    }

    private static IReadOnlyDictionary<string, int> EnsureResourceEntries(IReadOnlyDictionary<string, int> existing, IReadOnlyDictionary<string, int> defaults, out bool changed)
    {
        changed = false;
        if (defaults.Count == 0)
            return existing;

        Dictionary<string, int>? buffer = null;
        foreach (var (key, value) in defaults)
        {
            if (existing.ContainsKey(key))
                continue;

            buffer ??= new Dictionary<string, int>(existing, StringComparer.Ordinal);
            buffer[key] = value;
            changed = true;
        }

        return buffer ?? existing;
    }
}
