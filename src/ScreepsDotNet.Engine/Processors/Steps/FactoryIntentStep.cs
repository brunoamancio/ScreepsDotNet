namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Processes factory produce intents.
/// Consumes components to produce commodities based on recipes.
/// Uses ledger pattern to accumulate mutations before emitting patches.
/// </summary>
internal sealed class FactoryIntentStep : IRoomProcessorStep
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    /// <summary>
    /// Processes all factory intents for the current tick.
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
                    if (record.Name == IntentKeys.Produce)
                    {
                        ProcessProduce(context, obj, record, storeLedger, cooldownLedger, actionLogLedger, modifiedObjects);
                    }
                }
            }
        }

        EmitPatches(context, storeLedger, cooldownLedger, actionLogLedger, modifiedObjects);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes a produce intent for a factory.
    /// Consumes components and produces commodities based on recipes.
    /// </summary>
    private static void ProcessProduce(
        RoomProcessorContext context,
        RoomObjectSnapshot factory,
        IntentRecord record,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        Dictionary<string, int> cooldownLedger,
        Dictionary<string, RoomObjectActionLogPatch> actionLogLedger,
        HashSet<string> modifiedObjects)
    {
        // Validate factory type
        if (!string.Equals(factory.Type, RoomObjectTypes.Factory, StringComparison.Ordinal))
            return;

        // Check cooldown from snapshot (previous tick only)
        // Cooldown from current tick's ledger should not block subsequent intents in same tick
        var gameTime = context.State.GameTime;
        var currentCooldown = factory.Cooldown ?? 0;

        if (currentCooldown > gameTime)
            return;

        // Get intent arguments
        if (!TryGetResourceType(record, out var resourceType))
            return;

        // Get recipe
        if (!CommodityRecipes.TryGetRecipe(resourceType, out var recipe))
            return;

        if (recipe is null)
            return;

        // Check factory level (defer PWR_OPERATE_FACTORY effect to E5)
        var factoryLevel = factory.Level ?? 0;

        // TODO (E5): Check for PWR_OPERATE_FACTORY effect to increase factory level
        // if (factory.Effects?.TryGetValue(PowerTypes.OperateFactory, out var effect) == true) {
        //     factoryLevel = Math.Max(factoryLevel, effect.Magnitude);
        // }

        if (recipe.Level.HasValue && factoryLevel < recipe.Level.Value)
            return;

        // Get current store from ledger
        if (!storeLedger.TryGetValue(factory.Id, out var store)) {
            store = new Dictionary<string, int>(factory.Store, Comparer);
        }

        // Check for sufficient components
        foreach (var (componentType, componentAmount) in recipe.Components) {
            var currentAmount = store.GetValueOrDefault(componentType, 0);
            if (currentAmount < componentAmount)
                return;
        }

        // Check for sufficient capacity
        var currentStoreTotal = store.Values.Sum();
        var producedAmount = recipe.Amount;
        var storeCapacity = factory.StoreCapacity ?? ScreepsGameConstants.FactoryCapacity;

        if (currentStoreTotal + producedAmount > storeCapacity)
            return;

        // Consume components
        foreach (var (componentType, componentAmount) in recipe.Components) {
            var currentAmount = store.GetValueOrDefault(componentType, 0);
            var newAmount = currentAmount - componentAmount;
            if (newAmount > 0)
                store[componentType] = newAmount;
            else
                store.Remove(componentType);
        }

        // Add produced commodity
        var currentProduced = store.GetValueOrDefault(resourceType, 0);
        store[resourceType] = currentProduced + producedAmount;

        // Set cooldown (cumulative for multiple productions in same tick)
        if (!cooldownLedger.TryGetValue(factory.Id, out var newCooldown))
            newCooldown = gameTime;

        newCooldown += recipe.Cooldown;
        cooldownLedger[factory.Id] = newCooldown;

        // Record action log
        actionLogLedger[factory.Id] = new RoomObjectActionLogPatch(
            Produce: new RoomObjectActionLogProduce(resourceType)
        );

        storeLedger[factory.Id] = store;
        modifiedObjects.Add(factory.Id);
    }

    private static bool TryGetResourceType(IntentRecord record, out string resourceType)
    {
        resourceType = string.Empty;

        if (record.Arguments.Count == 0)
            return false;

        if (!record.Arguments[0].Fields.TryGetValue(IntentKeys.ResourceType, out var typeField))
            return false;

        if (typeField.Kind != IntentFieldValueKind.Text || string.IsNullOrWhiteSpace(typeField.TextValue))
            return false;

        resourceType = typeField.TextValue;
        return true;
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
