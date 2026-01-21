using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Validation;

namespace ScreepsDotNet.Engine.Processors.Steps;

/// <summary>
/// Validates all intents before processing and filters out invalid ones.
/// This step MUST run first before all other intent processing steps.
/// Uses the IntentValidationPipeline to orchestrate all registered validators.
/// </summary>
internal sealed class IntentValidationStep(IIntentPipeline pipeline) : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        // Convert RoomState to RoomSnapshot for validators
        var roomSnapshot = CreateRoomSnapshot(context.State);

        // Collect all intents from all users/objects
        var allIntents = CollectAllIntents(intents);

        // Validate intents through pipeline
        var validIntents = pipeline.Validate(allIntents, roomSnapshot);

        // If all intents were valid, no need to rebuild
        if (validIntents.Count == allIntents.Count)
            return Task.CompletedTask;

        // Rebuild intent structure with only valid intents
        var filteredIntents = RebuildIntentSnapshot(intents, validIntents);

        // Create new room state with filtered intents
        var newState = context.State with { Intents = filteredIntents };

        // Replace state in context
        context.ReplaceState(newState);

        return Task.CompletedTask;
    }

    private static RoomSnapshot CreateRoomSnapshot(RoomState state)
    {
        var snapshot = new RoomSnapshot(
            state.RoomName,
            state.GameTime,
            state.Info,
            state.Objects,
            state.Users,
            state.Intents,
            state.Terrain,
            state.Flags);
        return snapshot;
    }

    private static List<IntentRecord> CollectAllIntents(RoomIntentSnapshot intents)
    {
        var allIntents = new List<IntentRecord>();

        foreach (var envelope in intents.Users.Values) {
            if (envelope?.ObjectIntents is null)
                continue;

            foreach (var intentRecords in envelope.ObjectIntents.Values) {
                allIntents.AddRange(intentRecords);
            }
        }

        return allIntents;
    }

    private static RoomIntentSnapshot RebuildIntentSnapshot(RoomIntentSnapshot originalIntents, IReadOnlyList<IntentRecord> validIntents)
    {
        // Create a set for fast lookup
        var validIntentsSet = new HashSet<IntentRecord>(validIntents);

        var filteredUsers = new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal);

        foreach (var (userId, envelope) in originalIntents.Users) {
            if (envelope?.ObjectIntents is null)
                continue;

            var filteredObjectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal);

            foreach (var (objectId, intentRecords) in envelope.ObjectIntents) {
                if (intentRecords.Count == 0)
                    continue;

                // Filter intents for this object
                var filteredIntentsForObject = intentRecords.Where(intent => validIntentsSet.Contains(intent)).ToList();

                if (filteredIntentsForObject.Count > 0)
                    filteredObjectIntents[objectId] = filteredIntentsForObject;
            }

            // Only keep this user envelope if they have any valid intents left
            if (filteredObjectIntents.Count > 0) {
                // Preserve SpawnIntents and CreepIntents from original envelope
                var filteredEnvelope = envelope with { ObjectIntents = filteredObjectIntents };
                filteredUsers[userId] = filteredEnvelope;
            }
        }

        var filteredSnapshot = originalIntents with { Users = filteredUsers };
        return filteredSnapshot;
    }
}
