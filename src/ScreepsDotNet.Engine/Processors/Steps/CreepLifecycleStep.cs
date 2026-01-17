namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;

/// <summary>
/// Applies a minimal creep lifecycle: decrement TTL, clear fatigue for creeps that cannot move,
/// and emit an actionLog entry when a creep is about to die.
/// </summary>
internal sealed class CreepLifecycleStep(ICreepDeathProcessor deathProcessor) : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var energyLedger = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var obj in context.State.Objects.Values) {
            if (!obj.IsCreep())
                continue;

            if (obj.Hits is <= 0) {
                deathProcessor.Process(context, obj, new CreepDeathOptions(ViolentDeath: true), energyLedger);
                continue;
            }

            if (ShouldDespawnUserSummoned(context, obj)) {
                deathProcessor.Process(context, obj, new CreepDeathOptions(), energyLedger);
                continue;
            }

            int? ticksToLivePatch = null;
            RoomObjectActionLogPatch? actionLogPatch = null;

            if (obj.TicksToLive is > 0) {
                var next = obj.TicksToLive.Value - 1;
                ticksToLivePatch = next;

                if (next == 0) {
                    actionLogPatch = new RoomObjectActionLogPatch(
                        new RoomObjectActionLogDie(context.State.GameTime));
                }
            }

            int? fatiguePatch = null;
            if (obj.Fatigue is > 0 && (obj.MoveBodyParts ?? 0) == 0)
                fatiguePatch = 0;

            if (ticksToLivePatch is null && actionLogPatch is null && fatiguePatch is null)
                continue;

            if (ShouldExpire(obj)) {
                deathProcessor.Process(
                    context,
                    obj,
                    new CreepDeathOptions(),
                    energyLedger);
                continue;
            }

            var patch = new RoomObjectPatchPayload
            {
                TicksToLive = ticksToLivePatch,
                ActionLog = actionLogPatch,
                Fatigue = fatiguePatch
            };

            context.MutationWriter.Patch(obj.Id, patch);
        }

        return Task.CompletedTask;
    }

    private static bool ShouldExpire(RoomObjectSnapshot obj)
    {
        if (obj.Spawning is not null || obj.IsSpawning == true)
            return false;

        return obj.TicksToLive is <= 1;
    }

    private static bool ShouldDespawnUserSummoned(RoomProcessorContext context, RoomObjectSnapshot creep)
    {
        if (creep.UserSummoned != true)
            return false;

        var controllerOwner = FindControllerOwner(context.State.Objects);

        foreach (var other in context.State.Objects.Values) {
            if (ReferenceEquals(other, creep))
                continue;

            if (!other.IsCreep())
                continue;

            var otherUser = other.UserId;
            if (string.IsNullOrWhiteSpace(otherUser) || SystemUserIds.IsNpcUser(otherUser))
                continue;

            if (!string.IsNullOrWhiteSpace(controllerOwner) &&
                string.Equals(otherUser, controllerOwner, StringComparison.Ordinal)) {
                continue;
            }

            return true;
        }

        return false;
    }

    private static string? FindControllerOwner(IReadOnlyDictionary<string, RoomObjectSnapshot> objects)
    {
        foreach (var obj in objects.Values) {
            if (string.Equals(obj.Type, RoomObjectTypes.Controller, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(obj.UserId)) {
                return obj.UserId;
            }
        }

        return null;
    }

}
