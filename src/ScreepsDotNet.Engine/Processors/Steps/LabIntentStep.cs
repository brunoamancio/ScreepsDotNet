namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Processes lab-related intents: runReaction and boostCreep.
/// Uses ledger pattern to accumulate mutations before emitting patches.
/// </summary>
internal sealed class LabIntentStep : IRoomProcessorStep
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    /// <summary>
    /// Processes all lab intents for the current tick.
    /// </summary>
    /// <param name="context">The room processor context containing state, intents, and mutation writer.</param>
    /// <param name="token">Cancellation token for async operations.</param>
    /// <returns>A completed task.</returns>
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var storeLedger = new Dictionary<string, Dictionary<string, int>>(Comparer);
        var cooldownLedger = new Dictionary<string, int>(Comparer);
        var bodyLedger = new Dictionary<string, IReadOnlyList<CreepBodyPartSnapshot>>(Comparer);
        var storeCapacityLedger = new Dictionary<string, int>(Comparer);
        var storeCapacityResourceLedger = new Dictionary<string, Dictionary<string, int>>(Comparer);
        var actionLogLedger = new Dictionary<string, RoomObjectActionLogPatch>(Comparer);
        var modifiedObjects = new HashSet<string>(Comparer);

        foreach (var envelope in intents.Users.Values) {
            if (envelope?.ObjectIntents is null || envelope.ObjectIntents.Count == 0)
                continue;

            foreach (var (objectId, records) in envelope.ObjectIntents) {
                if (string.IsNullOrWhiteSpace(objectId) || records.Count == 0)
                    continue;

                if (!context.State.Objects.TryGetValue(objectId, out var obj))
                    continue;

                foreach (var record in records) {
                    switch (record.Name) {
                        case IntentKeys.RunReaction:
                            ProcessRunReaction(context, obj, record, storeLedger, cooldownLedger, storeCapacityResourceLedger, actionLogLedger, modifiedObjects);
                            break;
                        case IntentKeys.BoostCreep:
                            ProcessBoostCreep(context, obj, record, storeLedger, bodyLedger, storeCapacityLedger, storeCapacityResourceLedger, modifiedObjects);
                            break;
                        case IntentKeys.UnboostCreep:
                            ProcessUnboostCreep(context, obj, record, storeLedger, bodyLedger, storeCapacityLedger, modifiedObjects);
                            break;
                    }
                }
            }
        }

        EmitPatches(context, storeLedger, cooldownLedger, bodyLedger, storeCapacityLedger, storeCapacityResourceLedger, actionLogLedger, modifiedObjects);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes a runReaction intent.
    /// Combines two reagent labs' minerals to produce a compound in the target lab.
    /// Records reagent lab positions in action log for debugging visualization.
    /// </summary>
    private static void ProcessRunReaction(
        RoomProcessorContext context,
        RoomObjectSnapshot lab,
        IntentRecord record,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        Dictionary<string, int> cooldownLedger,
        Dictionary<string, Dictionary<string, int>> storeCapacityResourceLedger,
        Dictionary<string, RoomObjectActionLogPatch> actionLogLedger,
        HashSet<string> modifiedObjects)
    {
        if (!string.Equals(lab.Type, RoomObjectTypes.Lab, StringComparison.Ordinal))
            return;

        var gameTime = context.State.GameTime;
        var currentCooldown = cooldownLedger.GetValueOrDefault(lab.Id, lab.CooldownTime ?? 0);
        if (currentCooldown > gameTime)
            return;

        if (!TryGetLab1Id(record, out var lab1Id))
            return;

        if (!TryGetLab2Id(record, out var lab2Id))
            return;

        if (!context.State.Objects.TryGetValue(lab1Id, out var lab1))
            return;

        if (!string.Equals(lab1.Type, RoomObjectTypes.Lab, StringComparison.Ordinal))
            return;

        if (!IsInRange(lab, lab1, 2))
            return;

        var lab1Store = GetMutableStore(lab1, storeLedger);
        var lab1MineralType = GetLabMineralType(lab1, lab1Store);
        if (string.IsNullOrWhiteSpace(lab1MineralType))
            return;

        var lab1Available = lab1Store.GetValueOrDefault(lab1MineralType, 0);
        if (lab1Available < ScreepsGameConstants.LabReactionAmount)
            return;

        if (!context.State.Objects.TryGetValue(lab2Id, out var lab2))
            return;

        if (!string.Equals(lab2.Type, RoomObjectTypes.Lab, StringComparison.Ordinal))
            return;

        if (!IsInRange(lab, lab2, 2))
            return;

        var lab2Store = GetMutableStore(lab2, storeLedger);
        var lab2MineralType = GetLabMineralType(lab2, lab2Store);
        if (string.IsNullOrWhiteSpace(lab2MineralType))
            return;

        var lab2Available = lab2Store.GetValueOrDefault(lab2MineralType, 0);
        if (lab2Available < ScreepsGameConstants.LabReactionAmount)
            return;

        if (!LabReactions.TryGetProduct(lab1MineralType, lab2MineralType, out var product))
            return;

        var targetStore = GetMutableStore(lab, storeLedger);
        var targetMineralType = GetLabMineralType(lab, targetStore);
        var targetCurrent = targetStore.GetValueOrDefault(product, 0);

        if (targetCurrent + ScreepsGameConstants.LabReactionAmount > ScreepsGameConstants.LabMineralCapacity)
            return;

        if (!string.IsNullOrWhiteSpace(targetMineralType) && !string.Equals(targetMineralType, product, StringComparison.Ordinal))
            return;

        var reactionAmount = ScreepsGameConstants.LabReactionAmount;

        // Check for PWR_OPERATE_LAB effect to boost reaction amount
        if (lab.Effects.TryGetValue(PowerTypes.OperateLab, out var operateLabEffect)) {
            if (operateLabEffect.EndTime > gameTime && PowerInfo.Abilities.TryGetValue(PowerTypes.OperateLab, out var powerInfo)) {
                if (powerInfo.Effect is not null) {
                    var effectBonus = powerInfo.Effect[operateLabEffect.Level - 1];
                    reactionAmount = ScreepsGameConstants.LabReactionAmount + effectBonus;
                }
            }
        }

        targetStore[product] = targetCurrent + reactionAmount;
        lab1Store[lab1MineralType] = lab1Available - reactionAmount;
        lab2Store[lab2MineralType] = lab2Available - reactionAmount;

        if (!LabReactions.CooldownTimes.TryGetValue(product, out var cooldownTime))
            cooldownTime = 0;

        cooldownLedger[lab.Id] = gameTime + cooldownTime;

        if (targetCurrent == 0) {
            var targetCapacityResource = GetMutableStoreCapacityResource(lab, storeCapacityResourceLedger);
            targetCapacityResource[product] = ScreepsGameConstants.LabMineralCapacity;
        }

        if (lab1Available - reactionAmount == 0) {
            var lab1CapacityResource = GetMutableStoreCapacityResource(lab1, storeCapacityResourceLedger);
            lab1CapacityResource[lab1MineralType] = 0;
        }

        if (lab2Available - reactionAmount == 0) {
            var lab2CapacityResource = GetMutableStoreCapacityResource(lab2, storeCapacityResourceLedger);
            lab2CapacityResource[lab2MineralType] = 0;
        }

        actionLogLedger[lab.Id] = new RoomObjectActionLogPatch(
            RunReaction: new RoomObjectActionLogRunReaction(lab1.X, lab1.Y, lab2.X, lab2.Y)
        );

        modifiedObjects.Add(lab.Id);
        modifiedObjects.Add(lab1.Id);
        modifiedObjects.Add(lab2.Id);
    }

    /// <summary>
    /// Processes a boostCreep intent.
    /// Applies mineral boosts to compatible creep body parts.
    /// </summary>
    private static void ProcessBoostCreep(
        RoomProcessorContext context,
        RoomObjectSnapshot lab,
        IntentRecord record,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        Dictionary<string, IReadOnlyList<CreepBodyPartSnapshot>> bodyLedger,
        Dictionary<string, int> storeCapacityLedger,
        Dictionary<string, Dictionary<string, int>> storeCapacityResourceLedger,
        HashSet<string> modifiedObjects)
    {
        if (!string.Equals(lab.Type, RoomObjectTypes.Lab, StringComparison.Ordinal))
            return;

        if (!TryGetTargetId(record, out var creepId))
            return;

        if (!context.State.Objects.TryGetValue(creepId, out var creep))
            return;

        if (!creep.IsCreep(includePowerCreep: false))
            return;

        if (creep.IsSpawning == true)
            return;

        if (!IsInRange(lab, creep, 1))
            return;

        var labStore = GetMutableStore(lab, storeLedger);
        var mineralType = GetLabMineralType(lab, labStore);
        if (string.IsNullOrWhiteSpace(mineralType))
            return;

        var mineralAvailable = labStore.GetValueOrDefault(mineralType, 0);
        var energyAvailable = labStore.GetValueOrDefault(ResourceTypes.Energy, 0);

        if (mineralAvailable < ScreepsGameConstants.LabBoostMineral || energyAvailable < ScreepsGameConstants.LabBoostEnergy)
            return;

        var body = bodyLedger.TryGetValue(creep.Id, out var ledgerBody)
            ? ledgerBody.ToList()
            : creep.Body.ToList();

        var nonBoostedParts = new List<int>();
        for (var i = 0; i < body.Count; i++) {
            var part = body[i];
            if (string.IsNullOrWhiteSpace(part.Boost) && BoostConstants.CanBoost(part.Type, mineralType))
                nonBoostedParts.Add(i);
        }

        if (nonBoostedParts.Count == 0)
            return;

        var hasTough = body.Any(p => p.Type == BodyPartType.Tough && nonBoostedParts.Contains(body.IndexOf(p)));
        if (!hasTough)
            nonBoostedParts.Reverse();

        if (TryGetBodyPartsCount(record, out var bodyPartsCount)) {
            if (bodyPartsCount < nonBoostedParts.Count)
                nonBoostedParts = nonBoostedParts.Take(bodyPartsCount).ToList();
        }

        var boostedCount = 0;
        while (mineralAvailable >= ScreepsGameConstants.LabBoostMineral &&
               energyAvailable >= ScreepsGameConstants.LabBoostEnergy &&
               boostedCount < nonBoostedParts.Count) {
            var partIndex = nonBoostedParts[boostedCount];
            body[partIndex] = body[partIndex] with { Boost = mineralType };
            mineralAvailable -= ScreepsGameConstants.LabBoostMineral;
            energyAvailable -= ScreepsGameConstants.LabBoostEnergy;
            boostedCount++;
        }

        if (boostedCount == 0)
            return;

        labStore[mineralType] = mineralAvailable;
        labStore[ResourceTypes.Energy] = energyAvailable;

        bodyLedger[creep.Id] = body;

        var newStoreCapacity = RecalculateCreepStoreCapacity(body);
        storeCapacityLedger[creep.Id] = newStoreCapacity;

        if (mineralAvailable == 0) {
            var labCapacityResource = GetMutableStoreCapacityResource(lab, storeCapacityResourceLedger);
            labCapacityResource[mineralType] = 0;
        }

        modifiedObjects.Add(lab.Id);
        modifiedObjects.Add(creep.Id);
    }

    /// <summary>
    /// Processes an unboostCreep intent.
    /// Removes all boosts from creep body parts and returns minerals to the lab.
    /// </summary>
    private static void ProcessUnboostCreep(
        RoomProcessorContext context,
        RoomObjectSnapshot lab,
        IntentRecord record,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        Dictionary<string, IReadOnlyList<CreepBodyPartSnapshot>> bodyLedger,
        Dictionary<string, int> storeCapacityLedger,
        HashSet<string> modifiedObjects)
    {
        if (!string.Equals(lab.Type, RoomObjectTypes.Lab, StringComparison.Ordinal))
            return;

        if (!TryGetTargetId(record, out var creepId))
            return;

        if (!context.State.Objects.TryGetValue(creepId, out var creep))
            return;

        if (!creep.IsCreep(includePowerCreep: false))
            return;

        if (creep.IsSpawning == true)
            return;

        if (!IsInRange(lab, creep, 1))
            return;

        var body = bodyLedger.TryGetValue(creep.Id, out var ledgerBody)
            ? ledgerBody.ToList()
            : creep.Body.ToList();

        var boostedPartIndices = new List<int>();
        for (var i = 0; i < body.Count; i++) {
            var part = body[i];
            if (!string.IsNullOrWhiteSpace(part.Boost))
                boostedPartIndices.Add(i);
        }

        if (boostedPartIndices.Count == 0)
            return;

        var labStore = GetMutableStore(lab, storeLedger);
        var mineralReturned = new Dictionary<string, int>(Comparer);

        foreach (var partIndex in boostedPartIndices) {
            var part = body[partIndex];
            var mineral = part.Boost!;

            body[partIndex] = part with { Boost = null };

            var currentCount = mineralReturned.GetValueOrDefault(mineral, 0);
            mineralReturned[mineral] = currentCount + 1;
        }

        foreach (var (mineral, count) in mineralReturned) {
            var returnAmount = count * ScreepsGameConstants.LabUnboostMineral;
            var currentAmount = labStore.GetValueOrDefault(mineral, 0);
            labStore[mineral] = currentAmount + returnAmount;
        }

        bodyLedger[creep.Id] = body;

        var newStoreCapacity = RecalculateCreepStoreCapacity(body);
        storeCapacityLedger[creep.Id] = newStoreCapacity;

        modifiedObjects.Add(lab.Id);
        modifiedObjects.Add(creep.Id);
    }

    private static Dictionary<string, int> GetMutableStore(RoomObjectSnapshot obj, Dictionary<string, Dictionary<string, int>> ledger)
    {
        if (ledger.TryGetValue(obj.Id, out var store))
            return store;

        store = obj.Store.Count == 0
            ? new Dictionary<string, int>(Comparer)
            : new Dictionary<string, int>(obj.Store, Comparer);

        ledger[obj.Id] = store;
        return store;
    }

    private static Dictionary<string, int> GetMutableStoreCapacityResource(RoomObjectSnapshot obj, Dictionary<string, Dictionary<string, int>> ledger)
    {
        if (ledger.TryGetValue(obj.Id, out var capacityResource))
            return capacityResource;

        capacityResource = obj.StoreCapacityResource.Count == 0
            ? new Dictionary<string, int>(Comparer)
            : new Dictionary<string, int>(obj.StoreCapacityResource, Comparer);

        ledger[obj.Id] = capacityResource;
        return capacityResource;
    }

    private static string? GetLabMineralType(RoomObjectSnapshot lab, Dictionary<string, int> store)
    {
        foreach (var (resourceType, amount) in store) {
            if (!string.Equals(resourceType, ResourceTypes.Energy, StringComparison.Ordinal) && amount > 0)
                return resourceType;
        }

        return null;
    }

    private static bool IsInRange(RoomObjectSnapshot source, RoomObjectSnapshot target, int maxRange)
    {
        if (!string.Equals(source.RoomName, target.RoomName, StringComparison.Ordinal))
            return false;

        if (!string.Equals(source.Shard, target.Shard, StringComparison.Ordinal))
            return false;

        var dx = Math.Abs(source.X - target.X);
        var dy = Math.Abs(source.Y - target.Y);
        var result = dx <= maxRange && dy <= maxRange;
        return result;
    }

    private static int RecalculateCreepStoreCapacity(IReadOnlyList<CreepBodyPartSnapshot> body)
    {
        var capacity = 0;

        foreach (var part in body) {
            if (part.Type != BodyPartType.Carry)
                continue;

            if (part.Hits <= 0)
                continue;

            var baseCapacity = ScreepsGameConstants.CarryCapacity;

            if (!string.IsNullOrWhiteSpace(part.Boost) &&
                BoostConstants.TryGetMultiplier(BodyPartType.Carry, part.Boost, BoostActionTypes.Capacity, out var multiplier)) {
                capacity += (int)(baseCapacity * multiplier);
            }
            else {
                capacity += baseCapacity;
            }
        }

        return capacity;
    }

    private static void EmitPatches(
        RoomProcessorContext context,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        Dictionary<string, int> cooldownLedger,
        Dictionary<string, IReadOnlyList<CreepBodyPartSnapshot>> bodyLedger,
        Dictionary<string, int> storeCapacityLedger,
        Dictionary<string, Dictionary<string, int>> storeCapacityResourceLedger,
        Dictionary<string, RoomObjectActionLogPatch> actionLogLedger,
        HashSet<string> modifiedObjects)
    {
        foreach (var objectId in modifiedObjects) {
            var patch = new RoomObjectPatchPayload();

            if (storeLedger.TryGetValue(objectId, out var store))
                patch = patch with { Store = new Dictionary<string, int>(store, Comparer) };

            if (cooldownLedger.TryGetValue(objectId, out var cooldownTime))
                patch = patch with { CooldownTime = cooldownTime };

            if (bodyLedger.TryGetValue(objectId, out var body))
                patch = patch with { Body = body };

            if (storeCapacityLedger.TryGetValue(objectId, out var storeCapacity))
                patch = patch with { StoreCapacity = storeCapacity };

            if (storeCapacityResourceLedger.TryGetValue(objectId, out var storeCapacityResource)) {
                var cleanedCapacityResource = storeCapacityResource
                    .Where(kvp => kvp.Value > 0)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, Comparer);

                if (cleanedCapacityResource.Count > 0 || storeCapacityResource.Any(kvp => kvp.Value == 0))
                    patch = patch with { StoreCapacityResource = cleanedCapacityResource };
            }

            if (actionLogLedger.TryGetValue(objectId, out var actionLog))
                patch = patch with { ActionLog = actionLog };

            if (patch.HasChanges)
                context.MutationWriter.Patch(objectId, patch);
        }
    }

    private static bool TryGetTargetId(IntentRecord record, out string targetId)
    {
        targetId = string.Empty;
        if (record.Arguments.Count == 0)
            return false;

        var fields = record.Arguments[0].Fields;
        if (!fields.TryGetValue(IntentKeys.TargetId, out var value))
            return false;

        targetId = value.Kind switch
        {
            IntentFieldValueKind.Text => value.TextValue ?? string.Empty,
            IntentFieldValueKind.Number => value.NumberValue?.ToString() ?? string.Empty,
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(targetId);
    }

    private static bool TryGetLab1Id(IntentRecord record, out string lab1Id)
    {
        lab1Id = string.Empty;
        if (record.Arguments.Count == 0)
            return false;

        var fields = record.Arguments[0].Fields;
        if (!fields.TryGetValue(IntentKeys.Lab1, out var value))
            return false;

        lab1Id = value.TextValue ?? string.Empty;
        return !string.IsNullOrWhiteSpace(lab1Id);
    }

    private static bool TryGetLab2Id(IntentRecord record, out string lab2Id)
    {
        lab2Id = string.Empty;
        if (record.Arguments.Count == 0)
            return false;

        var fields = record.Arguments[0].Fields;
        if (!fields.TryGetValue(IntentKeys.Lab2, out var value))
            return false;

        lab2Id = value.TextValue ?? string.Empty;
        return !string.IsNullOrWhiteSpace(lab2Id);
    }

    private static bool TryGetBodyPartsCount(IntentRecord record, out int bodyPartsCount)
    {
        bodyPartsCount = 0;
        if (record.Arguments.Count == 0)
            return false;

        var fields = record.Arguments[0].Fields;
        if (!fields.TryGetValue(IntentKeys.BodyPartsCount, out var value))
            return false;

        bodyPartsCount = value.NumberValue ?? 0;
        return bodyPartsCount > 0;
    }
}
