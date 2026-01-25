namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Processes rampart setPublic intents.
/// Allows rampart owners to toggle public access for allied units.
/// </summary>
internal sealed class RampartIntentStep : IRoomProcessorStep
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    /// <summary>
    /// Processes all rampart intents for the current tick.
    /// </summary>
    /// <param name="context">The room processor context containing state, intents, and mutation writer.</param>
    /// <param name="token">Cancellation token for async operations.</param>
    /// <returns>A completed task.</returns>
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        foreach (var envelope in intents.Users.Values) {
            if (envelope?.ObjectIntents is null || envelope.ObjectIntents.Count == 0)
                continue;

            foreach (var (objectId, records) in envelope.ObjectIntents) {
                if (string.IsNullOrWhiteSpace(objectId) || records.Count == 0)
                    continue;

                if (!context.State.Objects.TryGetValue(objectId, out var obj))
                    continue;

                foreach (var record in records) {
                    if (record.Name == IntentKeys.SetPublic) {
                        ProcessSetPublic(context, obj, envelope.UserId, record);
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes a setPublic intent on a rampart.
    /// Only the owner can change the public access setting.
    /// </summary>
    private static void ProcessSetPublic(
        RoomProcessorContext context,
        RoomObjectSnapshot rampart,
        string userId,
        IntentRecord record)
    {
        // Validate object is a rampart
        if (!string.Equals(rampart.Type, RoomObjectTypes.Rampart, StringComparison.Ordinal))
            return;

        // Only the owner can set public/private access
        // Intent is keyed by userId in the envelope, so if the rampart's userId doesn't match,
        // the intent came from a different user trying to modify someone else's rampart
        if (!string.Equals(userId, rampart.UserId, StringComparison.Ordinal))
            return;

        // Get isPublic argument
        if (!TryGetIsPublic(record, out var isPublic))
            return;

        // Emit patch
        context.MutationWriter.Patch(rampart.Id, new RoomObjectPatchPayload
        {
            IsPublic = isPublic
        });
    }

    private static bool TryGetIsPublic(IntentRecord record, out bool isPublic)
    {
        isPublic = false;

        if (record.Arguments.Count == 0)
            return false;

        if (!record.Arguments[0].Fields.TryGetValue(IntentKeys.IsPublic, out var field))
            return false;

        if (field.Kind != IntentFieldValueKind.Number)
            return false;

        if (!field.NumberValue.HasValue)
            return false;

        isPublic = field.NumberValue.Value != 0;
        return true;
    }
}
