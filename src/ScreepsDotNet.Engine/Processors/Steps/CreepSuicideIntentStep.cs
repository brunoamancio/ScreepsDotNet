namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;

/// <summary>
/// Handles creep "suicide" intents by removing the creep and creating a tombstone with no dropped resources.
/// </summary>
internal sealed class CreepSuicideIntentStep(ICreepDeathProcessor deathProcessor) : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var energyLedger = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var envelope in intents.Users.Values) {
            if (envelope?.ObjectIntents is null)
                continue;

            foreach (var (objectId, intentRecords) in envelope.ObjectIntents) {
                if (!context.State.Objects.TryGetValue(objectId, out var obj))
                    continue;

                foreach (var record in intentRecords) {
                    if (!string.Equals(record.Name, IntentKeys.Suicide, StringComparison.Ordinal))
                        continue;

                    ProcessSuicideIntent(context, obj, deathProcessor, energyLedger);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static void ProcessSuicideIntent(RoomProcessorContext context, RoomObjectSnapshot obj, ICreepDeathProcessor processor, IDictionary<string, int> energyLedger)
    {
        // Validate object type
        if (!string.Equals(obj.Type, RoomObjectTypes.Creep, StringComparison.Ordinal))
            return;

        // Check if spawning
        if (obj.IsSpawning == true || obj.Spawning is not null)
            return;

        // Process death - invader creeps don't drop resources, others use default corpse rate
        var dropRate = SystemUserIds.IsInvader(obj.UserId)
            ? 0
            : ScreepsGameConstants.CreepCorpseRate;
        var options = new CreepDeathOptions(DropRate: dropRate, ViolentDeath: false, Spawn: null);
        processor.Process(context, obj, options, energyLedger);
    }
}
