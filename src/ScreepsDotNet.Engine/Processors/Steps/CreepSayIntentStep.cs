namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Handles creep "say" intents by setting the actionLog.say property with truncated message.
/// </summary>
internal sealed class CreepSayIntentStep : IRoomProcessorStep
{
    private const int MaxMessageLength = 10;

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        foreach (var envelope in intents.Users.Values) {
            if (envelope?.ObjectIntents is null)
                continue;

            foreach (var (objectId, intentRecords) in envelope.ObjectIntents) {
                if (!context.State.Objects.TryGetValue(objectId, out var obj))
                    continue;

                foreach (var record in intentRecords) {
                    if (!string.Equals(record.Name, IntentKeys.Say, StringComparison.Ordinal))
                        continue;

                    ProcessSayIntent(context, obj, record);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static void ProcessSayIntent(RoomProcessorContext context, RoomObjectSnapshot obj, IntentRecord record)
    {
        // Validate object type (both Creep and PowerCreep can say)
        var isCreep = string.Equals(obj.Type, RoomObjectTypes.Creep, StringComparison.Ordinal);
        var isPowerCreep = string.Equals(obj.Type, RoomObjectTypes.PowerCreep, StringComparison.Ordinal);
        if (!isCreep && !isPowerCreep)
            return;

        // Check if spawning (creeps only, PowerCreeps don't have spawning state)
        if (isCreep && (obj.IsSpawning == true || obj.Spawning is not null))
            return;

        // Extract message and isPublic from intent arguments
        if (record.Arguments.Count == 0)
            return;

        var fields = record.Arguments[0].Fields;

        var message = string.Empty;
        if (fields.TryGetValue("message", out var messageValue) && messageValue.TextValue is not null)
            message = messageValue.TextValue;

        var isPublic = false;
        if (fields.TryGetValue("isPublic", out var isPublicValue) && isPublicValue.BooleanValue.HasValue)
            isPublic = isPublicValue.BooleanValue.Value;

        // Truncate message to max length
        var truncatedMessage = message.Length > MaxMessageLength
            ? message[..MaxMessageLength]
            : message;

        // Patch action log
        var actionLogPatch = new RoomObjectActionLogPatch(Say: new RoomObjectActionLogSay(truncatedMessage, isPublic));
        var patch = new RoomObjectPatchPayload { ActionLog = actionLogPatch };

        context.MutationWriter.Patch(obj.Id, patch);
    }
}
