namespace ScreepsDotNet.Engine.Processors.Steps;

using System;
using System.Collections.Generic;
using System.Linq;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;

/// <summary>
/// Processes spawn intents (create, setDirections, cancel) using the shared helpers.
/// </summary>
internal sealed class SpawnIntentStep(
    ISpawnIntentParser parser,
    ISpawnStateReader stateReader,
    ISpawnEnergyCharger energyCharger,
    ICreepDeathProcessor deathProcessor) : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var energyLedger = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (userId, envelope) in intents.Users)
        {
            if (envelope?.SpawnIntents is null || envelope.SpawnIntents.Count == 0)
                continue;

            foreach (var (objectId, spawnIntent) in envelope.SpawnIntents)
            {
                if (string.IsNullOrWhiteSpace(objectId))
                    continue;

                if (!TryResolveSpawn(context, objectId, userId, out var spawn))
                    continue;

                ProcessSpawn(context, spawn, spawnIntent, energyLedger);
            }
        }

        return Task.CompletedTask;
    }

    private void ProcessSpawn(
        RoomProcessorContext context,
        RoomObjectSnapshot spawn,
        SpawnIntentEnvelope rawIntent,
        Dictionary<string, int> energyLedger)
    {
        var parsed = parser.Parse(rawIntent);
        if (!parsed.Success || !parsed.HasIntents)
            return;

        var runtime = stateReader.GetState(context.State, spawn);

        if (parsed.CancelSpawning)
        {
            HandleCancel(context, spawn, runtime);
            return;
        }

        if (parsed.DirectionsIntent is not null)
            HandleSetDirections(context, spawn, runtime, parsed.DirectionsIntent);

        if (parsed.CreateIntent is not null)
            HandleCreate(context, spawn, runtime, parsed.CreateIntent, energyLedger);

        if (parsed.RenewIntent is not null)
            HandleRenew(context, spawn, runtime, parsed.RenewIntent, energyLedger);

        if (parsed.RecycleIntent is not null)
            HandleRecycle(context, spawn, runtime, parsed.RecycleIntent, energyLedger);
    }

    private void HandleCreate(
        RoomProcessorContext context,
        RoomObjectSnapshot spawn,
        SpawnRuntimeState runtime,
        ParsedCreateCreepIntent intent,
        Dictionary<string, int> energyLedger)
    {
        if (runtime.IsSpawning)
            return;

        var requiredEnergy = intent.Body.TotalEnergyCost;
        if (requiredEnergy <= 0)
            return;

        var charge = energyCharger.TryCharge(
            context,
            spawn,
            requiredEnergy,
            intent.EnergyStructureIds,
            energyLedger);

        if (!charge.Success)
            return;

        var needTime = CalculateNeedTime(spawn, intent.Body.SpawnTime, context.State.GameTime);
        var spawnTime = context.State.GameTime + needTime;
        var spawning = new RoomSpawnSpawningSnapshot(
            intent.Name,
            needTime,
            spawnTime,
            intent.Directions);

        InsertPlaceholderCreep(context, spawn, intent);
        IncrementCreepProduced(context, spawn, intent.Body.BodyParts.Count);
        if (!string.IsNullOrWhiteSpace(spawn.UserId))
            context.Stats.IncrementSpawnCreates(spawn.UserId!);

        context.MutationWriter.Patch(spawn.Id, new RoomObjectPatchPayload
        {
            Spawning = spawning
        });
    }

    private static int CalculateNeedTime(RoomObjectSnapshot spawn, int baseNeedTime, int gameTime)
    {
        // TODO: apply PWR_OPERATE_SPAWN modifiers once spawn effects are mapped.
        return baseNeedTime;
    }

    private static void InsertPlaceholderCreep(
        RoomProcessorContext context,
        RoomObjectSnapshot spawn,
        ParsedCreateCreepIntent intent)
    {
        var bodyParts = intent.Body.BodyParts;
        var body = new CreepBodyPartSnapshot[bodyParts.Count];
        for (var i = 0; i < bodyParts.Count; i++)
            body[i] = new CreepBodyPartSnapshot(bodyParts[i], ScreepsGameConstants.BodyPartHitPoints, null);

        var store = new Dictionary<string, int>(1, StringComparer.Ordinal)
        {
            [RoomDocumentFields.RoomObject.Store.Energy] = 0
        };

        var placeholder = new RoomObjectSnapshot(
            Guid.NewGuid().ToString("N"),
            RoomObjectTypes.Creep,
            spawn.RoomName,
            spawn.Shard,
            spawn.UserId,
            spawn.X,
            spawn.Y,
            Hits: intent.Body.TotalHits,
            HitsMax: intent.Body.TotalHits,
            Fatigue: 0,
            TicksToLive: null,
            Name: intent.Name,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: store,
            StoreCapacity: intent.Body.CarryCapacity,
            StoreCapacityResource: new Dictionary<string, int>(0, StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(0, StringComparer.Ordinal),
            Spawning: null,
            Body: body,
            IsSpawning: true,
            UserSummoned: null,
            StrongholdId: null,
            DeathTime: null,
            DecayTime: null,
            CreepId: null,
            CreepName: null,
            CreepTicksToLive: null,
            CreepSaying: null,
            ResourceType: null,
            ResourceAmount: null);

        context.MutationWriter.Upsert(placeholder);
    }

    private static void IncrementCreepProduced(RoomProcessorContext context, RoomObjectSnapshot spawn, int bodyParts)
    {
        if (string.IsNullOrWhiteSpace(spawn.UserId))
            return;

        context.Stats.IncrementCreepsProduced(spawn.UserId!, bodyParts);
    }

    private static void HandleSetDirections(
        RoomProcessorContext context,
        RoomObjectSnapshot spawn,
        SpawnRuntimeState runtime,
        ParsedSetDirectionsIntent intent)
    {
        if (!runtime.IsSpawning || runtime.Spawning is null)
            return;

        var updated = runtime.Spawning with { Directions = intent.Directions };
        context.MutationWriter.Patch(spawn.Id, new RoomObjectPatchPayload
        {
            Spawning = updated
        });
    }

    private static void HandleCancel(RoomProcessorContext context, RoomObjectSnapshot spawn, SpawnRuntimeState runtime)
    {
        if (!runtime.IsSpawning)
            return;

        RemovePendingCreep(context, runtime);

        context.MutationWriter.Patch(spawn.Id, new RoomObjectPatchPayload
        {
            ClearSpawning = true
        });
    }

    private void HandleRenew(RoomProcessorContext context, RoomObjectSnapshot spawn,
                             SpawnRuntimeState runtime, ParsedRenewIntent intent,
                             Dictionary<string, int> energyLedger)
    {
        if (runtime.IsSpawning)
            return;

        if (!TryResolveCreep(context, intent.TargetId, spawn.UserId, out var target))
            return;

        if (!IsAdjacent(spawn, target))
            return;

        if (target.Body.Count == 0 || HasClaimParts(target.Body))
            return;

        var effect = CalculateRenewEffect(target.Body.Count);
        if (effect <= 0)
            return;

        var currentTtl = Math.Clamp(target.TicksToLive ?? 0, 0, ScreepsGameConstants.CreepLifeTime);
        var allowed = ScreepsGameConstants.CreepLifeTime - currentTtl;
        if (allowed <= 0)
            return;

        effect = Math.Min(effect, allowed);

        var cost = CalculateRenewEnergyCost(target.Body);
        if (cost <= 0)
            return;

        var charge = energyCharger.TryCharge(
            context,
            spawn,
            cost,
            null,
            energyLedger);

        if (!charge.Success)
            return;

        IReadOnlyList<CreepBodyPartSnapshot>? cleanedBody = null;
        Dictionary<string, int>? storePatch = null;
        int? storeCapacityPatch = null;

        if (HasBoosts(target.Body))
        {
            cleanedBody = ClearBoosts(target.Body);
            var resultingBody = cleanedBody!;
            var newCapacity = CalculateCarryCapacity(resultingBody);
            var currentCapacity = target.StoreCapacity ?? 0;
            if (newCapacity != currentCapacity)
                storeCapacityPatch = newCapacity;

            var mutableStore = target.Store.Count == 0
                ? null
                : new Dictionary<string, int>(target.Store, StringComparer.Ordinal);

            if (mutableStore is not null)
            {
                var overflow = CalculateOverflow(mutableStore, newCapacity);
                if (overflow > 0)
                {
                    storePatch = new Dictionary<string, int>(StringComparer.Ordinal);
                    var containerLedger = new Dictionary<string, ContainerLedgerEntry>(StringComparer.Ordinal);
                    var dropLedger = new Dictionary<DropLedgerKey, DropLedgerEntry>();
                    DropOverflowResources(
                        context,
                        target,
                        mutableStore,
                        overflow,
                        storePatch,
                        containerLedger,
                        dropLedger);
                }
            }
        }

        var patch = new RoomObjectPatchPayload
        {
            TicksToLive = currentTtl + effect,
            Body = cleanedBody,
            Store = storePatch is { Count: > 0 } ? storePatch : null,
            StoreCapacity = storeCapacityPatch,
            ActionLog = new RoomObjectActionLogPatch(
                Healed: new RoomObjectActionLogHealed(spawn.X, spawn.Y))
        };

        context.MutationWriter.Patch(target.Id, patch);

        if (!string.IsNullOrWhiteSpace(spawn.UserId))
            context.Stats.IncrementSpawnRenewals(spawn.UserId!);
    }

    private void HandleRecycle(
        RoomProcessorContext context,
        RoomObjectSnapshot spawn,
        SpawnRuntimeState runtime,
        ParsedRecycleIntent intent,
        Dictionary<string, int> energyLedger)
    {
        if (runtime.IsSpawning)
            return;

        if (!TryResolveCreep(context, intent.TargetId, spawn.UserId, out var target))
            return;

        if (!IsAdjacent(spawn, target))
            return;

        context.MutationWriter.Patch(target.Id, new RoomObjectPatchPayload
        {
            ActionLog = new RoomObjectActionLogPatch(
                Die: new RoomObjectActionLogDie(context.State.GameTime))
        });

        deathProcessor.Process(
            context,
            target,
            new CreepDeathOptions(
                DropRate: 1,
                Spawn: spawn),
            energyLedger);

        if (!string.IsNullOrWhiteSpace(spawn.UserId))
            context.Stats.IncrementSpawnRecycles(spawn.UserId!);
    }

    private static bool TryResolveSpawn(RoomProcessorContext context, string objectId, string userId, out RoomObjectSnapshot spawn)
    {
        spawn = null!;
        if (!context.State.Objects.TryGetValue(objectId, out var candidate))
            return false;

        if (!string.Equals(candidate.Type, RoomObjectTypes.Spawn, StringComparison.Ordinal))
            return false;

        if (!string.Equals(candidate.UserId, userId, StringComparison.Ordinal))
            return false;

        spawn = candidate;
        return true;
    }

    private static bool TryResolveCreep(RoomProcessorContext context, string objectId, string? userId, out RoomObjectSnapshot creep)
    {
        creep = null!;
        if (!context.State.Objects.TryGetValue(objectId, out var candidate))
            return false;

        if (!string.Equals(candidate.Type, RoomObjectTypes.Creep, StringComparison.Ordinal))
            return false;

        if (!string.Equals(candidate.UserId, userId, StringComparison.Ordinal))
            return false;

        creep = candidate;
        return true;
    }

    private static bool IsAdjacent(RoomObjectSnapshot a, RoomObjectSnapshot b)
        => string.Equals(a.RoomName, b.RoomName, StringComparison.Ordinal) &&
           Math.Abs(a.X - b.X) <= 1 &&
           Math.Abs(a.Y - b.Y) <= 1;

    private static bool HasClaimParts(IReadOnlyList<CreepBodyPartSnapshot> body)
    {
        for (var i = 0; i < body.Count; i++)
        {
            if (body[i].Type == BodyPartType.Claim)
                return true;
        }

        return false;
    }

    private static bool HasBoosts(IReadOnlyList<CreepBodyPartSnapshot> body)
    {
        for (var i = 0; i < body.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(body[i].Boost))
                return true;
        }

        return false;
    }

    private static int CalculateCarryCapacity(IReadOnlyList<CreepBodyPartSnapshot> body)
    {
        if (body.Count == 0)
            return 0;

        var capacity = 0;
        for (var i = 0; i < body.Count; i++)
        {
            if (body[i].Type == BodyPartType.Carry && body[i].Hits > 0)
                capacity += ScreepsGameConstants.CarryCapacity;
        }

        return capacity;
    }

    private static int CalculateOverflow(IReadOnlyDictionary<string, int> store, int capacity)
    {
        if (store.Count == 0)
            return 0;

        var total = store.Values.Sum();
        return Math.Max(total - capacity, 0);
    }

    private static void DropOverflowResources(
        RoomProcessorContext context,
        RoomObjectSnapshot creep,
        Dictionary<string, int> mutableStore,
        int overflow,
        Dictionary<string, int> storePatch,
        Dictionary<string, ContainerLedgerEntry> containerLedger,
        Dictionary<DropLedgerKey, DropLedgerEntry> dropLedger)
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

            DropResource(
                context,
                creep,
                resource,
                dropAmount,
                containerLedger,
                dropLedger);
        }
    }

    private static void DropResource(
        RoomProcessorContext context,
        RoomObjectSnapshot creep,
        string resourceType,
        int amount,
        Dictionary<string, ContainerLedgerEntry> containerLedger,
        Dictionary<DropLedgerKey, DropLedgerEntry> dropLedger)
    {
        if (amount <= 0)
            return;

        var remaining = amount;
        var container = FindContainer(context.State.Objects, creep);
        if (container is not null)
        {
            var entry = GetContainerLedgerEntry(container, containerLedger);
            var transferred = TransferToContainer(context, entry, resourceType, remaining);
            remaining -= transferred;
        }

        if (remaining <= 0)
            return;

        if (TryStackDrop(context, creep, resourceType, remaining, dropLedger))
            return;

        CreateDrop(context, creep, resourceType, remaining, dropLedger);
    }

    private static IEnumerable<string> EnumerateStoreResources(IReadOnlyDictionary<string, int> store)
    {
        if (store.Count == 0)
            yield break;

        var seen = new HashSet<string>(StringComparer.Ordinal);
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

    private static ContainerLedgerEntry GetContainerLedgerEntry(
        RoomObjectSnapshot container,
        Dictionary<string, ContainerLedgerEntry> ledger)
    {
        if (ledger.TryGetValue(container.Id, out var entry))
            return entry;

        entry = new ContainerLedgerEntry(container);
        ledger[container.Id] = entry;
        return entry;
    }

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
            Store = new Dictionary<string, int>(1, StringComparer.Ordinal)
            {
                [resourceType] = newValue
            }
        });

        return transfer;
    }

    private static bool TryStackDrop(
        RoomProcessorContext context,
        RoomObjectSnapshot creep,
        string resourceType,
        int amount,
        Dictionary<DropLedgerKey, DropLedgerEntry> dropLedger)
    {
        var key = new DropLedgerKey(
            creep.RoomName,
            creep.Shard,
            creep.X,
            creep.Y,
            resourceType);

        if (dropLedger.TryGetValue(key, out var entry))
        {
            var updatedAmount = (entry.Snapshot.ResourceAmount ?? 0) + amount;
            var updatedSnapshot = entry.Snapshot with { ResourceAmount = updatedAmount };
            entry.Snapshot = updatedSnapshot;
            dropLedger[key] = entry;
            context.MutationWriter.Upsert(updatedSnapshot);
            return true;
        }

        var existing = FindExistingDrop(context.State.Objects, creep, resourceType);
        if (existing is null)
            return false;

        var newAmount = (existing.ResourceAmount ?? 0) + amount;
        var updated = existing with { ResourceAmount = newAmount };
        dropLedger[key] = new DropLedgerEntry(updated);
        context.MutationWriter.Upsert(updated);
        return true;
    }

    private static void CreateDrop(
        RoomProcessorContext context,
        RoomObjectSnapshot creep,
        string resourceType,
        int amount,
        Dictionary<DropLedgerKey, DropLedgerEntry> dropLedger)
    {
        if (amount <= 0)
            return;

        var drop = new RoomObjectSnapshot(
            Guid.NewGuid().ToString("N"),
            RoomObjectTypes.Resource,
            creep.RoomName,
            creep.Shard,
            null,
            creep.X,
            creep.Y,
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
            Store: new Dictionary<string, int>(0, StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(0, StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(0, StringComparer.Ordinal),
            Spawning: null,
            Body: Array.Empty<CreepBodyPartSnapshot>(),
            ResourceType: resourceType,
            ResourceAmount: amount);

        var key = new DropLedgerKey(
            creep.RoomName,
            creep.Shard,
            creep.X,
            creep.Y,
            resourceType);

        dropLedger[key] = new DropLedgerEntry(drop);
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

    private sealed class ContainerLedgerEntry(RoomObjectSnapshot container)
    {
        public RoomObjectSnapshot Container { get; } = container;
        public Dictionary<string, int> Store { get; } = new(container.Store, StringComparer.Ordinal);
    }

    private sealed class DropLedgerEntry(RoomObjectSnapshot snapshot)
    {
        public RoomObjectSnapshot Snapshot { get; set; } = snapshot;
    }

    private sealed record DropLedgerKey(string RoomName, string? Shard, int X, int Y, string ResourceType);

    private static IReadOnlyList<CreepBodyPartSnapshot> ClearBoosts(IReadOnlyList<CreepBodyPartSnapshot> body)
    {
        if (body.Count == 0)
            return Array.Empty<CreepBodyPartSnapshot>();

        var result = new CreepBodyPartSnapshot[body.Count];
        for (var i = 0; i < body.Count; i++)
        {
            var part = body[i];
            result[i] = string.IsNullOrWhiteSpace(part.Boost)
                ? part
                : part with { Boost = null };
        }

        return result;
    }

    private static void RemovePendingCreep(RoomProcessorContext context, SpawnRuntimeState runtime)
    {
        var pending = runtime.PendingCreep;
        if (pending is null)
            return;

        if (pending.IsSpawning != true)
            return;

        context.MutationWriter.Remove(pending.Id);
    }

    private static int CalculateRenewEffect(int bodyLength)
    {
        if (bodyLength <= 0)
            return 0;

        var effect = (int)Math.Floor(
            ScreepsGameConstants.SpawnRenewRatio *
            ScreepsGameConstants.CreepLifeTime /
            ScreepsGameConstants.CreepSpawnTime /
            bodyLength);

        return Math.Max(effect, 0);
    }

    private static int CalculateRenewEnergyCost(IReadOnlyList<CreepBodyPartSnapshot> body)
    {
        if (body.Count == 0)
            return 0;

        var totalCost = 0;
        for (var i = 0; i < body.Count; i++)
        {
            if (!ScreepsGameConstants.TryGetBodyPartEnergyCost(body[i].Type, out var cost))
                return 0;

            totalCost += cost;
        }

        var value = ScreepsGameConstants.SpawnRenewRatio * totalCost /
                    ScreepsGameConstants.CreepSpawnTime /
                    body.Count;

        var costResult = (int)Math.Ceiling(value);
        return Math.Max(costResult, 0);
    }
}
