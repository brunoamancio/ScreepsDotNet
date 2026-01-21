namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Processes link transferEnergy intents.
/// Transfers energy between links with distance-based cooldowns and 3% loss ratio.
/// Uses ledger pattern to accumulate mutations before emitting patches.
/// </summary>
internal sealed class LinkIntentStep : IRoomProcessorStep
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    /// <summary>
    /// Processes all link intents for the current tick.
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
                    if (record.Name == IntentKeys.TransferEnergy) {
                        ProcessTransferEnergy(context, obj, record, storeLedger, cooldownLedger, actionLogLedger, modifiedObjects);
                    }
                }
            }
        }

        EmitPatches(context, storeLedger, cooldownLedger, actionLogLedger, modifiedObjects);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes a transferEnergy intent from source link to target link.
    /// Applies 3% energy loss and sets cooldown based on distance.
    /// </summary>
    private static void ProcessTransferEnergy(
        RoomProcessorContext context,
        RoomObjectSnapshot link,
        IntentRecord record,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        Dictionary<string, int> cooldownLedger,
        Dictionary<string, RoomObjectActionLogPatch> actionLogLedger,
        HashSet<string> modifiedObjects)
    {
        // Validate source is a link
        if (!string.Equals(link.Type, RoomObjectTypes.Link, StringComparison.Ordinal))
            return;

        // Check cooldown
        var gameTime = context.State.GameTime;
        if (!cooldownLedger.TryGetValue(link.Id, out var currentCooldown))
            currentCooldown = link.Cooldown ?? 0;

        if (currentCooldown > gameTime)
            return;

        // Get intent arguments
        if (!TryGetTargetId(record, out var targetId))
            return;

        if (!TryGetAmount(record, out var amount))
            return;

        // Validate target link
        if (!context.State.Objects.TryGetValue(targetId, out var target))
            return;

        if (!string.Equals(target.Type, RoomObjectTypes.Link, StringComparison.Ordinal))
            return;

        // Get current energy levels from ledger
        if (!storeLedger.TryGetValue(link.Id, out var sourceStore)) {
            sourceStore = new Dictionary<string, int>(link.Store, Comparer);
        }

        if (!storeLedger.TryGetValue(target.Id, out var targetStore)) {
            targetStore = new Dictionary<string, int>(target.Store, Comparer);
        }

        var sourceEnergy = sourceStore.GetValueOrDefault(ResourceTypes.Energy, 0);
        var targetEnergy = targetStore.GetValueOrDefault(ResourceTypes.Energy, 0);
        var targetCapacity = target.StoreCapacityResource?.GetValueOrDefault(ResourceTypes.Energy, ScreepsGameConstants.LinkCapacity) ?? ScreepsGameConstants.LinkCapacity;

        // Check if source has sufficient energy
        if (sourceEnergy < amount)
            return;

        // Check if target has space
        if (targetEnergy >= targetCapacity)
            return;

        // Calculate actual transfer amount (capped by target capacity)
        var availableSpace = targetCapacity - targetEnergy;
        var actualAmount = Math.Min(amount, availableSpace);

        // Apply loss ratio to target (3% loss)
        var lossDouble = actualAmount * ScreepsGameConstants.LinkLossRatio;
        var loss = (int)Math.Ceiling(lossDouble);
        var transferredToTarget = actualAmount - loss;

        // Update stores
        sourceStore[ResourceTypes.Energy] = sourceEnergy - actualAmount;
        targetStore[ResourceTypes.Energy] = targetEnergy + transferredToTarget;

        storeLedger[link.Id] = sourceStore;
        storeLedger[target.Id] = targetStore;

        // Calculate cooldown based on distance
        var distance = Math.Max(Math.Abs(target.X - link.X), Math.Abs(target.Y - link.Y));
        var cooldownTicks = ScreepsGameConstants.LinkCooldown * distance;
        cooldownLedger[link.Id] = gameTime + cooldownTicks;

        // Record action log (target position)
        actionLogLedger[link.Id] = new RoomObjectActionLogPatch(
            TransferEnergy: new RoomObjectActionLogTransferEnergy(target.X, target.Y)
        );

        modifiedObjects.Add(link.Id);
        modifiedObjects.Add(target.Id);
    }

    private static bool TryGetTargetId(IntentRecord record, out string targetId)
    {
        targetId = string.Empty;

        if (record.Arguments.Count == 0)
            return false;

        if (!record.Arguments[0].Fields.TryGetValue(IntentKeys.TargetId, out var idField))
            return false;

        if (idField.Kind != IntentFieldValueKind.Text || string.IsNullOrWhiteSpace(idField.TextValue))
            return false;

        targetId = idField.TextValue;
        return true;
    }

    private static bool TryGetAmount(IntentRecord record, out int amount)
    {
        amount = 0;

        if (record.Arguments.Count == 0)
            return false;

        if (!record.Arguments[0].Fields.TryGetValue(IntentKeys.Amount, out var amountField))
            return false;

        if (amountField.Kind != IntentFieldValueKind.Number)
            return false;

        if (!amountField.NumberValue.HasValue)
            return false;

        amount = amountField.NumberValue.Value;
        return amount > 0;
    }

    private static void EmitPatches(
        RoomProcessorContext context,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        Dictionary<string, int> cooldownLedger,
        Dictionary<string, RoomObjectActionLogPatch> actionLogLedger,
        HashSet<string> modifiedObjects)
    {
        foreach (var objectId in modifiedObjects) {
            var patch = new RoomObjectPatchPayload();

            if (storeLedger.TryGetValue(objectId, out var store))
                patch = patch with { Store = new Dictionary<string, int>(store, Comparer) };

            if (cooldownLedger.TryGetValue(objectId, out var cooldown))
                patch = patch with { Cooldown = cooldown };

            if (actionLogLedger.TryGetValue(objectId, out var actionLog))
                patch = patch with { ActionLog = actionLog };

            if (patch.HasChanges)
                context.MutationWriter.Patch(objectId, patch);
        }
    }
}
