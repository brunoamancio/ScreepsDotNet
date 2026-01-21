namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Processes power spawn processPower intents.
/// Consumes 1 power + 50 energy per tick to generate ops for power creeps.
/// Uses ledger pattern to accumulate mutations before emitting patches.
/// </summary>
internal sealed class PowerSpawnIntentStep : IRoomProcessorStep
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    /// <summary>
    /// Processes all power spawn intents for the current tick.
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

        foreach (var envelope in intents.Users.Values) {
            if (envelope?.ObjectIntents is null || envelope.ObjectIntents.Count == 0)
                continue;

            foreach (var (objectId, records) in envelope.ObjectIntents) {
                if (string.IsNullOrWhiteSpace(objectId) || records.Count == 0)
                    continue;

                if (!context.State.Objects.TryGetValue(objectId, out var obj))
                    continue;

                foreach (var record in records) {
                    if (record.Name == IntentKeys.ProcessPower) {
                        ProcessPowerSpawn(context, obj, record, storeLedger, modifiedObjects);
                    }
                }
            }
        }

        EmitPatches(context, storeLedger, modifiedObjects);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes a processPower intent for a power spawn.
    /// Consumes 1 power and 50 energy per tick.
    /// </summary>
    private static void ProcessPowerSpawn(
        RoomProcessorContext context,
        RoomObjectSnapshot powerSpawn,
        IntentRecord record,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        HashSet<string> modifiedObjects)
    {
        var gameTime = context.State.GameTime;

        // Validate power spawn type
        if (!string.Equals(powerSpawn.Type, RoomObjectTypes.PowerSpawn, StringComparison.Ordinal))
            return;

        // Get current store from ledger
        if (!storeLedger.TryGetValue(powerSpawn.Id, out var store)) {
            store = new Dictionary<string, int>(powerSpawn.Store, Comparer);
        }

        var currentPower = store.GetValueOrDefault(ResourceTypes.Power, 0);
        var currentEnergy = store.GetValueOrDefault(ResourceTypes.Energy, 0);

        // Base amount is 1 power per tick
        var amount = 1;

        // Check for PWR_OPERATE_POWER effect to boost processing amount
        if (powerSpawn.Effects.TryGetValue(PowerTypes.OperatePower, out var operatePowerEffect)) {
            if (operatePowerEffect.EndTime > gameTime && PowerInfo.Abilities.TryGetValue(PowerTypes.OperatePower, out var powerInfo)) {
                if (powerInfo.Effect is not null) {
                    var effectBonus = powerInfo.Effect[operatePowerEffect.Level - 1];
                    amount = 1 + effectBonus;
                }
            }
        }

        var energyRequired = amount * ScreepsGameConstants.PowerSpawnEnergyRatio;

        // Check if power spawn has sufficient resources
        if (currentPower < amount)
            return;

        if (currentEnergy < energyRequired)
            return;

        // Consume resources
        store[ResourceTypes.Power] = currentPower - amount;
        store[ResourceTypes.Energy] = currentEnergy - energyRequired;

        storeLedger[powerSpawn.Id] = store;
        modifiedObjects.Add(powerSpawn.Id);

        // TODO (E5): Increment user power balance
        // context.GlobalMutationWriter.IncrementUserPower(powerSpawn.UserId, amount);

        // TODO (E5): Record stats
        // context.Stats.IncrementUserStat(powerSpawn.UserId, "powerProcessed", amount);
    }

    private static void EmitPatches(
        RoomProcessorContext context,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        HashSet<string> modifiedObjects)
    {
        foreach (var objectId in modifiedObjects) {
            var patch = new RoomObjectPatchPayload();

            if (storeLedger.TryGetValue(objectId, out var store))
                patch = patch with { Store = new Dictionary<string, int>(store, Comparer) };

            if (patch.HasChanges)
                context.MutationWriter.Patch(objectId, patch);
        }
    }
}
