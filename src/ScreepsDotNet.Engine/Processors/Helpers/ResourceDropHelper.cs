namespace ScreepsDotNet.Engine.Processors.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;

internal interface IResourceDropHelper
{
    ResourceDropContext CreateContext();

    void DropOverflowResources(
        RoomProcessorContext context,
        RoomObjectSnapshot origin,
        Dictionary<string, int> mutableStore,
        int overflow,
        Dictionary<string, int> storePatch,
        ResourceDropContext dropContext);

    void DropResource(
        RoomProcessorContext context,
        RoomObjectSnapshot origin,
        string resourceType,
        int amount,
        ResourceDropContext dropContext);
}

internal sealed class ResourceDropHelper : IResourceDropHelper
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    public ResourceDropContext CreateContext()
        => new();

    public void DropOverflowResources(
        RoomProcessorContext context,
        RoomObjectSnapshot origin,
        Dictionary<string, int> mutableStore,
        int overflow,
        Dictionary<string, int> storePatch,
        ResourceDropContext dropContext)
    {
        if (overflow <= 0)
            return;

        foreach (var resource in EnumerateStoreResources(mutableStore))
        {
            if (overflow <= 0)
                break;

            if (!mutableStore.TryGetValue(resource, out var amount) || amount <= 0)
                continue;

            var dropAmount = Math.Min(amount, overflow);

            mutableStore[resource] = amount - dropAmount;
            storePatch[resource] = mutableStore[resource];
            overflow -= dropAmount;

            DropResource(context, origin, resource, dropAmount, dropContext);
        }
    }

    public void DropResource(
        RoomProcessorContext context,
        RoomObjectSnapshot origin,
        string resourceType,
        int amount,
        ResourceDropContext dropContext)
    {
        if (amount <= 0)
            return;

        var remaining = amount;
        var container = FindContainer(context.State.Objects, origin);
        if (container is not null)
        {
            var entry = dropContext.GetContainerEntry(container);
            var transferred = TransferToContainer(context, entry, resourceType, remaining);
            remaining -= transferred;
        }

        if (remaining <= 0)
            return;

        if (TryStackDrop(context, origin, resourceType, remaining, dropContext))
            return;

        CreateDrop(context, origin, resourceType, remaining, dropContext);
    }

    private static IEnumerable<string> EnumerateStoreResources(IReadOnlyDictionary<string, int> store)
    {
        if (store.Count == 0)
            yield break;

        var seen = new HashSet<string>(Comparer);
        foreach (var resource in ScreepsGameConstants.ResourceOrder)
        {
            if (store.ContainsKey(resource) && seen.Add(resource))
                yield return resource;
        }

        foreach (var resource in store.Keys)
        {
            if (seen.Add(resource))
                yield return resource;
        }
    }

    private static RoomObjectSnapshot? FindContainer(
        IReadOnlyDictionary<string, RoomObjectSnapshot> objects,
        RoomObjectSnapshot origin)
    {
        foreach (var obj in objects.Values)
        {
            if (!string.Equals(obj.RoomName, origin.RoomName, StringComparison.Ordinal))
                continue;

            if (!string.Equals(obj.Shard, origin.Shard, StringComparison.Ordinal))
                continue;

            if (obj.X != origin.X || obj.Y != origin.Y)
                continue;

            if (!IsContainer(obj))
                continue;

            return obj;
        }

        return null;
    }

    private static bool IsContainer(RoomObjectSnapshot obj)
        => string.Equals(obj.Type, RoomObjectTypes.Container, StringComparison.Ordinal) ||
           string.Equals(obj.StructureType, RoomObjectTypes.Container, StringComparison.Ordinal);

    private static int TransferToContainer(
        RoomProcessorContext context,
        ContainerLedgerEntry entry,
        string resourceType,
        int amount)
    {
        if ((entry.Container.Hits ?? 1) <= 0)
            return 0;

        var capacity = entry.Container.StoreCapacity ?? 0;
        if (capacity <= 0)
            return 0;

        var currentTotal = entry.Store.Count == 0 ? 0 : entry.Store.Values.Sum();
        var remainingCapacity = capacity - currentTotal;
        if (remainingCapacity <= 0)
            return 0;

        var transfer = Math.Min(remainingCapacity, amount);
        if (transfer <= 0)
            return 0;

        var current = entry.Store.TryGetValue(resourceType, out var existing) ? existing : 0;
        var newValue = current + transfer;
        entry.Store[resourceType] = newValue;

        context.MutationWriter.Patch(entry.Container.Id, new RoomObjectPatchPayload
        {
            Store = new Dictionary<string, int>(1, Comparer)
            {
                [resourceType] = newValue
            }
        });

        return transfer;
    }

    private static bool TryStackDrop(
        RoomProcessorContext context,
        RoomObjectSnapshot origin,
        string resourceType,
        int amount,
        ResourceDropContext dropContext)
    {
        var key = DropLedgerKey.Create(origin, resourceType);

        if (dropContext.TryGetDrop(key, out var entry) && entry is not null)
        {
            var updatedAmount = (entry.Snapshot.ResourceAmount ?? 0) + amount;
            var updatedSnapshot = entry.Snapshot with { ResourceAmount = updatedAmount };
            dropContext.RecordDrop(key, updatedSnapshot);
            context.MutationWriter.Upsert(updatedSnapshot);
            return true;
        }

        var existing = FindExistingDrop(context.State.Objects, origin, resourceType);
        if (existing is null)
            return false;

        var newAmount = (existing.ResourceAmount ?? 0) + amount;
        var updated = existing with { ResourceAmount = newAmount };
        dropContext.RecordDrop(key, updated);
        context.MutationWriter.Upsert(updated);
        return true;
    }

    private static void CreateDrop(
        RoomProcessorContext context,
        RoomObjectSnapshot origin,
        string resourceType,
        int amount,
        ResourceDropContext dropContext)
    {
        if (amount <= 0)
            return;

        var drop = new RoomObjectSnapshot(
            Guid.NewGuid().ToString("N"),
            RoomObjectTypes.Resource,
            origin.RoomName,
            origin.Shard,
            null,
            origin.X,
            origin.Y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(0, Comparer),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(0, Comparer),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(0, Comparer),
            Spawning: null,
            Body: [],
            ResourceType: resourceType,
            ResourceAmount: amount);

        var key = DropLedgerKey.Create(origin, resourceType);
        dropContext.RecordDrop(key, drop);
        context.MutationWriter.Upsert(drop);
    }

    private static RoomObjectSnapshot? FindExistingDrop(
        IReadOnlyDictionary<string, RoomObjectSnapshot> objects,
        RoomObjectSnapshot origin,
        string resourceType)
    {
        foreach (var obj in objects.Values)
        {
            if (!string.Equals(obj.RoomName, origin.RoomName, StringComparison.Ordinal))
                continue;

            if (!string.Equals(obj.Shard, origin.Shard, StringComparison.Ordinal))
                continue;

            if (obj.X != origin.X || obj.Y != origin.Y)
                continue;

            if (!string.Equals(obj.Type, RoomObjectTypes.Resource, StringComparison.Ordinal))
                continue;

            if (!string.Equals(obj.ResourceType, resourceType, StringComparison.Ordinal))
                continue;

            return obj;
        }

        return null;
    }
}

internal sealed class ResourceDropContext
{
    private readonly Dictionary<string, ContainerLedgerEntry> _containers = new(StringComparer.Ordinal);
    private readonly Dictionary<DropLedgerKey, DropLedgerEntry> _drops = new();

    public ContainerLedgerEntry GetContainerEntry(RoomObjectSnapshot container)
    {
        if (_containers.TryGetValue(container.Id, out var entry))
            return entry;

        entry = new ContainerLedgerEntry(container);
        _containers[container.Id] = entry;
        return entry;
    }

    public bool TryGetDrop(DropLedgerKey key, out DropLedgerEntry? entry)
        => _drops.TryGetValue(key, out entry);

    public void RecordDrop(DropLedgerKey key, RoomObjectSnapshot snapshot)
        => _drops[key] = new DropLedgerEntry(snapshot);
}

internal sealed class ContainerLedgerEntry
{
    public ContainerLedgerEntry(RoomObjectSnapshot container)
    {
        Container = container;
        ArgumentNullException.ThrowIfNull(container.Store);
        var sourceStore = container.Store;
        Store = sourceStore.Count == 0
            ? new Dictionary<string, int>(0, StringComparer.Ordinal)
            : new Dictionary<string, int>(sourceStore, StringComparer.Ordinal);
    }

    public RoomObjectSnapshot Container { get; }

    public Dictionary<string, int> Store { get; }
}

internal sealed class DropLedgerEntry(RoomObjectSnapshot snapshot)
{
    public RoomObjectSnapshot Snapshot { get; set; } = snapshot;
}

internal sealed record DropLedgerKey(string RoomName, string? Shard, int X, int Y, string ResourceType)
{
    public static DropLedgerKey Create(RoomObjectSnapshot origin, string resourceType)
        => new(origin.RoomName, origin.Shard, origin.X, origin.Y, resourceType);
}
