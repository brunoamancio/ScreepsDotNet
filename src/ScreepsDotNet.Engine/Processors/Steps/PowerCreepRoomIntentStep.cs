using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Engine.Processors.Steps;

/// <summary>
/// Processes PowerCreep room-level intents (enableRoom, renew).
/// Note: usePower is handled by PowerAbilityStep due to its complexity.
/// </summary>
internal sealed class PowerCreepRoomIntentStep : IRoomProcessorStep
{
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

                if (!context.State.Objects.TryGetValue(objectId, out var powerCreep))
                    continue;

                if (!string.Equals(powerCreep.Type, RoomObjectTypes.PowerCreep, StringComparison.Ordinal))
                    continue;

                if (!string.Equals(powerCreep.UserId, envelope.UserId, StringComparison.Ordinal))
                    continue;

                foreach (var record in records) {
                    switch (record.Name) {
                        case IntentKeys.EnableRoom:
                            ProcessEnableRoom(context, powerCreep, record);
                            break;
                        case IntentKeys.Renew:
                            ProcessRenew(context, powerCreep, record);
                            break;
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    private static void ProcessEnableRoom(RoomProcessorContext context, RoomObjectSnapshot powerCreep, IntentRecord record)
    {
        if (!TryGetTargetId(record, out var targetId))
            return;

        if (!context.State.Objects.TryGetValue(targetId, out var controller))
            return;

        if (!string.Equals(controller.Type, RoomObjectTypes.Controller, StringComparison.Ordinal))
            return;

        // Validate target user matches PowerCreep user OR controller is not in safe mode
        if (!string.Equals(controller.UserId, powerCreep.UserId, StringComparison.Ordinal)) {
            var safeMode = controller.SafeMode ?? 0;
            if (safeMode > context.State.GameTime)
                return;
        }

        // Validate distance <= 1
        if (!IsAdjacent(powerCreep, controller))
            return;

        // Update controller with isPowerEnabled: true
        context.MutationWriter.Patch(controller.Id, new RoomObjectPatchPayload
        {
            IsPowerEnabled = true
        });

        // Set actionLog.attack to controller position
        context.MutationWriter.Patch(powerCreep.Id, new RoomObjectPatchPayload
        {
            ActionLog = new RoomObjectActionLogPatch(
                Attack: new RoomObjectActionLogAttack(controller.X, controller.Y)
            )
        });
    }

    private static void ProcessRenew(RoomProcessorContext context, RoomObjectSnapshot powerCreep, IntentRecord record)
    {
        if (!TryGetTargetId(record, out var targetId))
            return;

        if (!context.State.Objects.TryGetValue(targetId, out var target))
            return;

        // Validate target is powerBank or powerSpawn
        var isPowerBank = string.Equals(target.Type, RoomObjectTypes.PowerBank, StringComparison.Ordinal);
        var isPowerSpawn = string.Equals(target.Type, RoomObjectTypes.PowerSpawn, StringComparison.Ordinal);
        if (!isPowerBank && !isPowerSpawn)
            return;

        // Validate distance <= 1
        if (!IsAdjacent(powerCreep, target))
            return;

        // If powerSpawn, validate it's owned by the same user as the controller
        if (isPowerSpawn) {
            var controller = FindController(context);
            if (controller is null || !string.Equals(target.UserId, controller.UserId, StringComparison.Ordinal))
                return;
        }

        // Update PowerCreep ageTime
        var newAgeTime = context.State.GameTime + ScreepsGameConstants.PowerCreepLifeTime;
        context.MutationWriter.Patch(powerCreep.Id, new RoomObjectPatchPayload
        {
            AgeTime = newAgeTime,
            ActionLog = new RoomObjectActionLogPatch(
                Healed: new RoomObjectActionLogHealed(powerCreep.X, powerCreep.Y)
            )
        });
    }

    private static bool IsAdjacent(RoomObjectSnapshot a, RoomObjectSnapshot b)
    {
        if (!string.Equals(a.RoomName, b.RoomName, StringComparison.Ordinal))
            return false;

        var dx = Math.Abs(a.X - b.X);
        var dy = Math.Abs(a.Y - b.Y);
        return dx <= 1 && dy <= 1;
    }

    private static RoomObjectSnapshot? FindController(RoomProcessorContext context)
    {
        foreach (var obj in context.State.Objects.Values) {
            if (string.Equals(obj.Type, RoomObjectTypes.Controller, StringComparison.Ordinal))
                return obj;
        }
        return null;
    }

    private static bool TryGetTargetId(IntentRecord record, out string targetId)
    {
        if (record.Arguments.Count == 0) {
            targetId = string.Empty;
            return false;
        }

        var argument = record.Arguments[0];
        if (!argument.Fields.TryGetValue(IntentKeys.TargetId, out var field) ||
            field.Kind != IntentFieldValueKind.Text ||
            string.IsNullOrWhiteSpace(field.TextValue)) {
            targetId = string.Empty;
            return false;
        }

        targetId = field.TextValue;
        return true;
    }
}
