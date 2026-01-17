namespace ScreepsDotNet.Engine.Processors.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;

internal interface ICreepDeathProcessor
{
    void Process(RoomProcessorContext context, RoomObjectSnapshot creep, CreepDeathOptions options, IDictionary<string, int> energyLedger);
}

internal sealed record CreepDeathOptions(double DropRate = ScreepsGameConstants.CreepCorpseRate, bool ViolentDeath = false, RoomObjectSnapshot? Spawn = null);

internal sealed class CreepDeathProcessor : ICreepDeathProcessor
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    public void Process(RoomProcessorContext context, RoomObjectSnapshot creep, CreepDeathOptions options, IDictionary<string, int> energyLedger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(creep);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(energyLedger);

        context.MutationWriter.Remove(creep.Id);

        if (!string.IsNullOrWhiteSpace(creep.UserId) && creep.Body.Count > 0)
            context.Stats.IncrementCreepsLost(creep.UserId!, creep.Body.Count);

        var dropResources = CalculateBodyResources(creep, options.DropRate);
        MergeResourceDictionary(dropResources, creep.Store);

        var container = FindContainer(context.State.Objects, creep);
        if (container is not null)
            MoveResourcesToContainer(context, container, dropResources);

        var tombstone = CreateTombstoneSnapshot(context.State.GameTime, creep, dropResources, options.ViolentDeath);
        context.MutationWriter.Upsert(tombstone);
        if (!string.IsNullOrWhiteSpace(creep.UserId))
            context.Stats.IncrementTombstonesCreated(creep.UserId!);

        if (options.Spawn is not null) {
            var refund = CalculateRecycleRefund(creep);
            if (refund > 0)
                ApplyEnergyRefund(context, options.Spawn, refund, energyLedger);
        }
    }

    private static RoomObjectSnapshot CreateTombstoneSnapshot(
        int gameTime,
        RoomObjectSnapshot creep,
        IReadOnlyDictionary<string, int> store,
        bool violentDeath)
    {
        var decay = CalculateDecayTime(gameTime, creep);
        var tombstoneStore = store.Count == 0
            ? new Dictionary<string, int>(0, Comparer)
            : new Dictionary<string, int>(store, Comparer);

        return new RoomObjectSnapshot(
            Guid.NewGuid().ToString("N"),
            RoomObjectTypes.Tombstone,
            creep.RoomName,
            creep.Shard,
            creep.UserId,
            creep.X,
            creep.Y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: creep.Name,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: tombstoneStore,
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(0, Comparer),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(0, Comparer),
            Spawning: null,
            Body: creep.Body,
            UserSummoned: creep.UserSummoned,
            StrongholdId: creep.StrongholdId,
            DeathTime: gameTime,
            DecayTime: decay,
            CreepId: creep.Id,
            CreepName: creep.Name,
            CreepTicksToLive: creep.TicksToLive,
            CreepSaying: creep.CreepSaying,
            ResourceType: null,
            ResourceAmount: null);
    }

    private static int CalculateDecayTime(int gameTime, RoomObjectSnapshot creep)
    {
        if (string.Equals(creep.Type, RoomObjectTypes.PowerCreep, StringComparison.Ordinal))
            return gameTime + ScreepsGameConstants.TombstoneDecayPowerCreep;

        var parts = Math.Max(creep.Body.Count, 1);
        return gameTime + (parts * ScreepsGameConstants.TombstoneDecayPerPart);
    }

    private static Dictionary<string, int> CalculateBodyResources(RoomObjectSnapshot creep, double dropRate)
    {
        var result = new Dictionary<string, int>(Comparer);
        if (dropRate <= 0 || creep.Body.Count == 0)
            return result;

        var lifeTime = DetermineLifeTime(creep.Body);
        var ticksToLive = Math.Clamp(creep.TicksToLive ?? lifeTime, 0, lifeTime);
        if (ticksToLive <= 0)
            return result;

        var lifeRate = dropRate * ticksToLive / lifeTime;
        if (lifeRate <= 0)
            return result;

        double energy = 0;
        var boostResources = new Dictionary<string, double>(Comparer);

        foreach (var part in creep.Body) {
            if (ScreepsGameConstants.TryGetBodyPartEnergyCost(part.Type, out var cost))
                energy += Math.Min(ScreepsGameConstants.CreepPartMaxEnergy, cost * lifeRate);

            if (!string.IsNullOrWhiteSpace(part.Boost)) {
                var boost = part.Boost!;
                var mineral = ScreepsGameConstants.LabBoostMineral * lifeRate;
                boostResources[boost] = boostResources.TryGetValue(boost, out var existing)
                    ? existing + mineral
                    : mineral;

                energy += ScreepsGameConstants.LabBoostEnergy * lifeRate;
            }
        }

        if (energy > 0)
            result[RoomDocumentFields.RoomObject.Store.Energy] = (int)Math.Floor(energy);

        foreach (var (resource, amount) in boostResources) {
            var floored = (int)Math.Floor(amount);
            if (floored > 0)
                result[resource] = floored;
        }

        return result;
    }

    private static int DetermineLifeTime(IReadOnlyList<CreepBodyPartSnapshot> body)
        => body.Any(part => part.Type == BodyPartType.Claim)
            ? ScreepsGameConstants.CreepClaimLifeTime
            : ScreepsGameConstants.CreepLifeTime;

    private static void MergeResourceDictionary(
        IDictionary<string, int> target,
        IReadOnlyDictionary<string, int> source)
    {
        foreach (var (resource, amount) in source) {
            if (amount <= 0 || string.IsNullOrWhiteSpace(resource))
                continue;

            target[resource] = target.TryGetValue(resource, out var existing)
                ? existing + amount
                : amount;
        }
    }

    private static RoomObjectSnapshot? FindContainer(
        IReadOnlyDictionary<string, RoomObjectSnapshot> objects,
        RoomObjectSnapshot creep)
    {
        foreach (var obj in objects.Values) {
            if (!string.Equals(obj.RoomName, creep.RoomName, StringComparison.Ordinal))
                continue;

            if (obj.X != creep.X || obj.Y != creep.Y)
                continue;

            if (!IsContainer(obj))
                continue;

            return obj;
        }

        return null;
    }

    private static bool IsContainer(RoomObjectSnapshot obj)
        => string.Equals(obj.Type, RoomObjectTypes.Container, StringComparison.Ordinal)
           || string.Equals(obj.StructureType, RoomObjectTypes.Container, StringComparison.Ordinal);

    private static void MoveResourcesToContainer(
        RoomProcessorContext context,
        RoomObjectSnapshot container,
        IDictionary<string, int> dropResources)
    {
        if (dropResources.Count == 0)
            return;

        if ((container.Hits ?? 1) <= 0)
            return;

        var capacity = container.StoreCapacity ?? 0;
        if (capacity <= 0)
            return;

        var currentTotal = container.Store.Values.Sum();
        var remainingCapacity = capacity - currentTotal;
        if (remainingCapacity <= 0)
            return;

        var updatedStore = new Dictionary<string, int>(Comparer);

        foreach (var resource in dropResources.Keys.ToList()) {
            if (remainingCapacity <= 0)
                break;

            var available = dropResources[resource];
            if (available <= 0)
                continue;

            var transfer = Math.Min(available, remainingCapacity);
            if (transfer <= 0)
                continue;

            var newValue = container.Store.GetValueOrDefault(resource, 0) + transfer;
            updatedStore[resource] = newValue;
            dropResources[resource] = available - transfer;
            remainingCapacity -= transfer;
        }

        foreach (var key in dropResources.Where(kvp => kvp.Value <= 0).Select(kvp => kvp.Key).ToList())
            dropResources.Remove(key);

        if (updatedStore.Count == 0)
            return;

        context.MutationWriter.Patch(container.Id, new RoomObjectPatchPayload
        {
            Store = updatedStore
        });
    }

    private static int CalculateRecycleRefund(RoomObjectSnapshot creep)
    {
        if (creep.Body.Count == 0)
            return 0;

        var ttl = Math.Clamp(creep.TicksToLive ?? 0, 0, ScreepsGameConstants.CreepLifeTime);
        if (ttl <= 0)
            return 0;

        var totalCost = 0;
        foreach (var part in creep.Body) {
            if (ScreepsGameConstants.TryGetBodyPartEnergyCost(part.Type, out var cost))
                totalCost += cost;
        }

        return totalCost <= 0 ? 0 : (int)Math.Floor(totalCost * ttl / (double)ScreepsGameConstants.CreepLifeTime);
    }

    private static void ApplyEnergyRefund(
        RoomProcessorContext context,
        RoomObjectSnapshot spawn,
        int refund,
        IDictionary<string, int> energyLedger)
    {
        if (refund <= 0)
            return;

        var key = RoomDocumentFields.RoomObject.Store.Energy;
        var current = energyLedger.TryGetValue(spawn.Id, out var overrideValue)
            ? overrideValue
            : spawn.Store.GetValueOrDefault(key, 0);

        var capacity = spawn.StoreCapacity ?? ScreepsGameConstants.SpawnEnergyCapacity;
        var newValue = Math.Min(capacity, current + refund);
        energyLedger[spawn.Id] = newValue;

        context.MutationWriter.Patch(spawn.Id, new RoomObjectPatchPayload
        {
            Store = new Dictionary<string, int>(Comparer)
            {
                [key] = newValue
            }
        });

        if (!string.IsNullOrWhiteSpace(spawn.UserId))
            context.Stats.IncrementEnergyCreeps(spawn.UserId!, refund);
    }
}
