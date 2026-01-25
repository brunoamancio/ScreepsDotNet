namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Common.Utilities;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;

/// <summary>
/// Processes observer observeRoom intents.
/// Sets temporary observeRoom property to grant vision to distant rooms.
/// Uses ledger pattern to accumulate mutations before emitting patches.
/// </summary>
internal sealed class ObserverIntentStep : IRoomProcessorStep
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    /// <summary>
    /// Processes all observer intents for the current tick.
    /// </summary>
    /// <param name="context">The room processor context containing state, intents, and mutation writer.</param>
    /// <param name="token">Cancellation token for async operations.</param>
    /// <returns>A completed task.</returns>
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var observeRoomLedger = new Dictionary<string, string>(Comparer);
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
                    if (record.Name == IntentKeys.ObserveRoom) {
                        ProcessObserveRoom(context, envelope.UserId, obj, record, observeRoomLedger, actionLogLedger, modifiedObjects);
                    }
                }
            }
        }

        EmitPatches(context, observeRoomLedger, actionLogLedger, modifiedObjects);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes an observeRoom intent from an observer.
    /// Validates ownership, room name, RCL, and range (with PWR_OPERATE_OBSERVER support).
    /// </summary>
    private static void ProcessObserveRoom(
        RoomProcessorContext context,
        string userId,
        RoomObjectSnapshot observer,
        IntentRecord record,
        Dictionary<string, string> observeRoomLedger,
        Dictionary<string, RoomObjectActionLogPatch> actionLogLedger,
        HashSet<string> modifiedObjects)
    {
        // Validate observer type
        if (!string.Equals(observer.Type, RoomObjectTypes.Observer, StringComparison.Ordinal))
            return;

        // Validate ownership
        if (!string.Equals(observer.UserId, userId, StringComparison.Ordinal))
            return;

        // Get target room name from intent
        if (!TryGetRoomName(record, out var targetRoomName))
            return;

        // Validate room name format
        if (!RoomCoordinateHelper.IsValidRoomName(targetRoomName))
            return;

        // Check structure activation (requires controller ownership and RCL limits)
        var controller = StructureActivationHelper.FindController(context.State.Objects);
        if (!StructureActivationHelper.IsStructureActive(observer, context.State.Objects, controller))
            return;

        // Calculate distance between observer room and target room
        var distance = RoomCoordinateHelper.CalculateDistance(observer.RoomName, targetRoomName);

        // Check if PWR_OPERATE_OBSERVER effect is active
        var hasOperateObserverEffect = false;
        if (observer.Effects is not null &&
            observer.Effects.TryGetValue(PowerTypes.OperateObserver, out var effect)) {
            hasOperateObserverEffect = effect.EndTime > context.State.GameTime;
        }

        // Validate range (unless PWR_OPERATE_OBSERVER is active)
        if (!hasOperateObserverEffect && distance > ScreepsGameConstants.ObserverRange)
            return;

        // Set observeRoom in ledger
        observeRoomLedger[observer.Id] = targetRoomName;

        // Record action log
        actionLogLedger[observer.Id] = new RoomObjectActionLogPatch(
            ObserveRoom: new RoomObjectActionLogObserveRoom(targetRoomName)
        );

        modifiedObjects.Add(observer.Id);
    }

    private static bool TryGetRoomName(IntentRecord record, out string roomName)
    {
        roomName = string.Empty;

        if (record.Arguments.Count == 0)
            return false;

        if (!record.Arguments[0].Fields.TryGetValue(IntentKeys.RoomName, out var roomNameField))
            return false;

        if (roomNameField.Kind != IntentFieldValueKind.Text || string.IsNullOrWhiteSpace(roomNameField.TextValue))
            return false;

        roomName = roomNameField.TextValue;
        return true;
    }

    private static void EmitPatches(
        RoomProcessorContext context,
        Dictionary<string, string> observeRoomLedger,
        Dictionary<string, RoomObjectActionLogPatch> actionLogLedger,
        HashSet<string> modifiedObjects)
    {
        foreach (var objectId in modifiedObjects) {
            // Observer.observeRoom is set via global mutation (not room mutation)
            if (observeRoomLedger.TryGetValue(objectId, out var observeRoom)) {
                var globalPatch = new GlobalRoomObjectPatch(ObserveRoom: observeRoom);
                context.GlobalMutationWriter.PatchRoomObject(objectId, globalPatch);
            }

            // ActionLog is set via room mutation
            if (actionLogLedger.TryGetValue(objectId, out var actionLog)) {
                var roomPatch = new RoomObjectPatchPayload { ActionLog = actionLog };
                if (roomPatch.HasChanges)
                    context.MutationWriter.Patch(objectId, roomPatch);
            }
        }
    }
}
