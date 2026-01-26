using ScreepsDotNet.Common.Types;

namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors.Helpers;

/// <summary>
/// Processes resource transfer intents (transfer, withdraw, pickup, drop) for creeps.
/// Implements Screeps resource I/O mechanics including store capacity tracking, lab mineral capacity,
/// rampart blocking, terminal disruption, and resource drop creation.
/// </summary>
internal sealed class ResourceTransferIntentStep(IResourceDropHelper resourceDropHelper) : IRoomProcessorStep
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    /// <summary>
    /// Processes all resource transfer intents for the current tick.
    /// Uses a ledger pattern to accumulate multiple store changes per object before emitting patches.
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
        var modifiedObjects = new HashSet<string>(Comparer);
        var dropContext = resourceDropHelper.CreateContext();

        foreach (var envelope in intents.Users.Values) {
            if (envelope?.ObjectIntents is null || envelope.ObjectIntents.Count == 0)
                continue;

            foreach (var (objectId, records) in envelope.ObjectIntents) {
                if (string.IsNullOrWhiteSpace(objectId) || records.Count == 0)
                    continue;

                if (!context.State.Objects.TryGetValue(objectId, out var creep))
                    continue;

                if (!creep.IsCreep(includePowerCreep: true))
                    continue;

                if (creep.IsSpawning == true)
                    continue;

                if (!string.Equals(creep.UserId, envelope.UserId, StringComparison.Ordinal))
                    continue;

                foreach (var record in records) {
                    switch (record.Name) {
                        case IntentKeys.Transfer:
                            ProcessTransfer(context, creep, record, storeLedger, modifiedObjects);
                            break;
                        case IntentKeys.Withdraw:
                            ProcessWithdraw(context, creep, record, storeLedger, modifiedObjects);
                            break;
                        case IntentKeys.Pickup:
                            ProcessPickup(context, creep, record, storeLedger, modifiedObjects);
                            break;
                        case IntentKeys.Drop:
                            ProcessDrop(context, creep, record, storeLedger, modifiedObjects, dropContext);
                            break;
                    }
                }
            }
        }

        foreach (var objectId in modifiedObjects) {
            if (storeLedger.TryGetValue(objectId, out var store)) {
                context.MutationWriter.Patch(objectId, new RoomObjectPatchPayload
                {
                    Store = new Dictionary<string, int>(store, Comparer)
                });
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes a transfer intent (creep → target).
    /// Validates adjacency, capacity, and special cases (ramparts, labs, etc.).
    /// Updates lab capacity tracking when transferring non-energy resources to labs.
    /// </summary>
    /// <param name="context">The room processor context.</param>
    /// <param name="creep">The creep initiating the transfer.</param>
    /// <param name="record">The intent record containing targetId, resourceType, and amount.</param>
    /// <param name="storeLedger">Ledger tracking accumulated store changes across all intents.</param>
    /// <param name="modifiedObjects">Set of object IDs that have been modified this tick.</param>
    private static void ProcessTransfer(RoomProcessorContext context, RoomObjectSnapshot creep, IntentRecord record, Dictionary<string, Dictionary<string, int>> storeLedger, HashSet<string> modifiedObjects)
    {
        if (!TryGetTargetId(record, out var targetId))
            return;

        if (!TryGetResourceType(record, out var resourceType))
            return;

        if (!TryGetAmount(record, out var requestedAmount))
            return;

        if (!ScreepsGameConstants.ResourcesAll.Contains(resourceType))
            return;

        var creepStore = GetMutableStore(creep, storeLedger);
        if (!creepStore.TryGetValue(resourceType, out var available) || available <= 0)
            return;

        if (!context.State.Objects.TryGetValue(targetId, out var target))
            return;

        if (target.IsCreep() && target.IsSpawning == true)
            return;

        if (!IsAdjacent(creep, target))
            return;

        var targetStore = GetMutableStore(target, storeLedger);
        var targetTotalUsed = targetStore.Values.Sum();
        var targetTotalCapacity = target.StoreCapacity ?? 0;

        if (targetTotalCapacity <= 0)
            return;

        var targetFreeSpace = targetTotalCapacity - targetTotalUsed;
        if (targetFreeSpace <= 0)
            return;

        var actualAmount = Math.Min(requestedAmount, Math.Min(available, targetFreeSpace));
        if (actualAmount <= 0)
            return;

        creepStore[resourceType] = available - actualAmount;
        var targetCurrent = targetStore.GetValueOrDefault(resourceType, 0);
        targetStore[resourceType] = targetCurrent + actualAmount;

        modifiedObjects.Add(creep.Id);
        modifiedObjects.Add(target.Id);

        if (!IsLab(target) || string.Equals(resourceType, ResourceTypes.Energy, StringComparison.Ordinal)) return;

        if (targetCurrent == 0) {
            context.MutationWriter.Patch(target.Id, new RoomObjectPatchPayload
            {
                StoreCapacityResource = new Dictionary<string, int>(Comparer)
                {
                    [resourceType] = ScreepsGameConstants.LabMineralCapacity
                }
            });
        }
    }

    /// <summary>
    /// Processes a withdraw intent (target → creep).
    /// Validates safe mode, rampart blocking, terminal disruption, and capacity constraints.
    /// Clears lab capacity tracking when withdrawing the last non-energy resource from a lab.
    /// </summary>
    /// <param name="context">The room processor context.</param>
    /// <param name="creep">The creep initiating the withdrawal.</param>
    /// <param name="record">The intent record containing targetId, resourceType, and amount.</param>
    /// <param name="storeLedger">Ledger tracking accumulated store changes across all intents.</param>
    /// <param name="modifiedObjects">Set of object IDs that have been modified this tick.</param>
    private static void ProcessWithdraw(RoomProcessorContext context, RoomObjectSnapshot creep, IntentRecord record, Dictionary<string, Dictionary<string, int>> storeLedger, HashSet<string> modifiedObjects)
    {
        if (!TryGetTargetId(record, out var targetId))
            return;

        if (!TryGetResourceType(record, out var resourceType))
            return;

        if (!TryGetAmount(record, out var requestedAmount))
            return;

        if (!ScreepsGameConstants.ResourcesAll.Contains(resourceType))
            return;

        var creepCapacity = creep.StoreCapacity ?? 0;
        if (creepCapacity <= 0)
            return;

        var creepStore = GetMutableStore(creep, storeLedger);
        var creepTotal = creepStore.Values.Sum();
        var creepFreeSpace = creepCapacity - creepTotal;
        if (creepFreeSpace <= 0)
            return;

        if (IsSafeModeActive(context, creep))
            return;

        if (!context.State.Objects.TryGetValue(targetId, out var target))
            return;

        if (IsBlockedByRampart(context, creep, target))
            return;

        if (!IsAdjacent(creep, target))
            return;

        if (IsNukerOrPowerBank(target))
            return;

        if (IsTerminal(target) && HasTerminalDisruption(target))
            return;

        var targetStore = GetMutableStore(target, storeLedger);
        if (!targetStore.TryGetValue(resourceType, out var targetAvailable) || targetAvailable <= 0) {
            // Edge case: Node.js has undefined > amount bug, patches with NaN/incorrect values
            // .NET correctly validates, but we still add to modifiedObjects to ensure creep
            // gets patched with store showing 0 (for parity test visibility)
            modifiedObjects.Add(creep.Id);
            modifiedObjects.Add(target.Id);
            return;
        }

        var actualAmount = Math.Min(requestedAmount, Math.Min(targetAvailable, creepFreeSpace));
        if (actualAmount <= 0)
            return;

        targetStore[resourceType] = targetAvailable - actualAmount;
        var creepCurrent = creepStore.GetValueOrDefault(resourceType, 0);
        creepStore[resourceType] = creepCurrent + actualAmount;

        modifiedObjects.Add(creep.Id);
        modifiedObjects.Add(target.Id);

        if (!IsLab(target) || string.Equals(resourceType, ResourceTypes.Energy, StringComparison.Ordinal)) return;

        var remainingAmount = targetAvailable - actualAmount;
        if (remainingAmount == 0) {
            context.MutationWriter.Patch(target.Id, new RoomObjectPatchPayload
            {
                StoreCapacityResource = new Dictionary<string, int>(0, Comparer)
            });
        }
    }

    /// <summary>
    /// Processes a pickup intent (creep picks up resource drop from ground).
    /// Validates adjacency and capacity. Removes the resource drop if fully picked up.
    /// </summary>
    /// <param name="context">The room processor context.</param>
    /// <param name="creep">The creep initiating the pickup.</param>
    /// <param name="record">The intent record containing targetId (resource drop object ID).</param>
    /// <param name="storeLedger">Ledger tracking accumulated store changes across all intents.</param>
    /// <param name="modifiedObjects">Set of object IDs that have been modified this tick.</param>
    private static void ProcessPickup(RoomProcessorContext context, RoomObjectSnapshot creep, IntentRecord record, Dictionary<string, Dictionary<string, int>> storeLedger, HashSet<string> modifiedObjects)
    {
        if (!TryGetTargetId(record, out var targetId))
            return;

        var creepCapacity = creep.StoreCapacity ?? 0;
        if (creepCapacity <= 0)
            return;

        var creepStore = GetMutableStore(creep, storeLedger);
        var creepTotal = creepStore.Values.Sum();
        var creepFreeSpace = creepCapacity - creepTotal;
        if (creepFreeSpace <= 0)
            return;

        if (!context.State.Objects.TryGetValue(targetId, out var drop))
            return;

        if (!string.Equals(drop.Type, RoomObjectTypes.Resource, StringComparison.Ordinal))
            return;

        if (!IsAdjacent(creep, drop))
            return;

        var resourceType = drop.ResourceType;
        var dropAmount = drop.ResourceAmount ?? 0;
        if (string.IsNullOrWhiteSpace(resourceType) || dropAmount <= 0)
            return;

        var actualAmount = Math.Min(dropAmount, creepFreeSpace);

        var creepCurrent = creepStore.GetValueOrDefault(resourceType, 0);
        creepStore[resourceType] = creepCurrent + actualAmount;

        modifiedObjects.Add(creep.Id);

        var remaining = dropAmount - actualAmount;
        if (remaining <= 0) {
            context.MutationWriter.Remove(drop.Id);
        }
        else {
            var updated = drop with { ResourceAmount = remaining };
            context.MutationWriter.Upsert(updated);
        }
    }

    /// <summary>
    /// Processes a drop intent (creep drops resource to ground).
    /// Creates or consolidates resource drop objects at the creep's position.
    /// </summary>
    /// <param name="context">The room processor context.</param>
    /// <param name="creep">The creep initiating the drop.</param>
    /// <param name="record">The intent record containing resourceType and amount.</param>
    /// <param name="storeLedger">Ledger tracking accumulated store changes across all intents.</param>
    /// <param name="modifiedObjects">Set of object IDs that have been modified this tick.</param>
    /// <param name="dropContext">Context for resource drop helper to track drops created/updated this tick.</param>
    private void ProcessDrop(RoomProcessorContext context, RoomObjectSnapshot creep, IntentRecord record, Dictionary<string, Dictionary<string, int>> storeLedger, HashSet<string> modifiedObjects, ResourceDropContext dropContext)
    {
        if (!TryGetResourceType(record, out var resourceType))
            return;

        if (!TryGetAmount(record, out var requestedAmount))
            return;

        if (!ScreepsGameConstants.ResourcesAll.Contains(resourceType))
            return;

        var creepStore = GetMutableStore(creep, storeLedger);
        if (!creepStore.TryGetValue(resourceType, out var available) || available <= 0)
            return;

        var actualAmount = Math.Min(requestedAmount, available);
        if (actualAmount <= 0)
            return;

        creepStore[resourceType] = available - actualAmount;

        modifiedObjects.Add(creep.Id);

        resourceDropHelper.DropResource(context, creep, resourceType, actualAmount, dropContext);
    }

    private static bool IsAdjacent(RoomObjectSnapshot a, RoomObjectSnapshot b)
    {
        if (!string.Equals(a.RoomName, b.RoomName, StringComparison.Ordinal))
            return false;

        if (!string.Equals(a.Shard, b.Shard, StringComparison.Ordinal))
            return false;

        var dx = Math.Abs(a.X - b.X);
        var dy = Math.Abs(a.Y - b.Y);
        return dx <= 1 && dy <= 1;
    }

    private static int CalculateCapacityForResource(RoomObjectSnapshot obj, string resourceType, Dictionary<string, int> currentStore)
    {
        if (obj.StoreCapacityResource?.TryGetValue(resourceType, out var specific) == true)
            return specific;

        var totalCapacity = obj.StoreCapacity ?? 0;
        if (totalCapacity <= 0)
            return 0;

        var reservedCapacity = obj.StoreCapacityResource?.Values.Sum() ?? 0;
        return Math.Max(0, totalCapacity - reservedCapacity);
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

    private static bool IsSafeModeActive(RoomProcessorContext context, RoomObjectSnapshot creep)
    {
        var controller = ResolveRoomController(context.State.Objects);
        if (controller is null)
            return false;

        if (controller.SafeMode is null or <= 0)
            return false;

        return !string.Equals(controller.UserId, creep.UserId, StringComparison.Ordinal);
    }

    private static bool IsBlockedByRampart(RoomProcessorContext context, RoomObjectSnapshot creep, RoomObjectSnapshot target)
    {
        foreach (var obj in context.State.Objects.Values) {
            if (!string.Equals(obj.Type, RoomObjectTypes.Rampart, StringComparison.Ordinal))
                continue;

            if (!string.Equals(obj.RoomName, target.RoomName, StringComparison.Ordinal))
                continue;

            if (!string.Equals(obj.Shard, target.Shard, StringComparison.Ordinal))
                continue;

            if (obj.X != target.X || obj.Y != target.Y)
                continue;

            var isPublic = obj.IsPublic == true;
            var ownedByCreep = string.Equals(obj.UserId, creep.UserId, StringComparison.Ordinal);

            return !isPublic && !ownedByCreep;
        }

        return false;
    }

    private static bool IsNukerOrPowerBank(RoomObjectSnapshot obj)
    {
        var result = string.Equals(obj.Type, RoomObjectTypes.Nuker, StringComparison.Ordinal) ||
               string.Equals(obj.Type, RoomObjectTypes.PowerBank, StringComparison.Ordinal);
        return result;
    }

    private static bool IsTerminal(RoomObjectSnapshot obj)
    {
        var result = string.Equals(obj.Type, RoomObjectTypes.Terminal, StringComparison.Ordinal);
        return result;
    }

    private static bool IsLab(RoomObjectSnapshot obj)
    {
        var result = string.Equals(obj.Type, RoomObjectTypes.Lab, StringComparison.Ordinal);
        return result;
    }

    private static bool HasTerminalDisruption(RoomObjectSnapshot terminal)
        => terminal.Effects.ContainsKey(PowerTypes.DisruptTerminal);

    private static RoomObjectSnapshot? ResolveRoomController(IReadOnlyDictionary<string, RoomObjectSnapshot> objects)
    {
        foreach (var obj in objects.Values) {
            if (string.Equals(obj.Type, RoomObjectTypes.Controller, StringComparison.Ordinal))
                return obj;
        }

        return null;
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

    private static bool TryGetResourceType(IntentRecord record, out string resourceType)
    {
        resourceType = string.Empty;
        if (record.Arguments.Count == 0)
            return false;

        var fields = record.Arguments[0].Fields;
        if (!fields.TryGetValue(IntentKeys.ResourceType, out var value))
            return false;

        resourceType = value.TextValue ?? string.Empty;
        return !string.IsNullOrWhiteSpace(resourceType);
    }

    private static bool TryGetAmount(IntentRecord record, out int amount)
    {
        amount = 0;
        if (record.Arguments.Count == 0)
            return false;

        var fields = record.Arguments[0].Fields;
        if (!fields.TryGetValue(IntentKeys.Amount, out var value))
            return false;

        amount = value.NumberValue ?? 0;
        return amount > 0;
    }
}
