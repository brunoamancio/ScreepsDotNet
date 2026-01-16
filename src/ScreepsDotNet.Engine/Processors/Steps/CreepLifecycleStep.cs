using ScreepsDotNet.Common.Constants;

namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Applies a minimal creep lifecycle: decrement TTL, clear fatigue for creeps that cannot move,
/// and emit an actionLog entry when a creep is about to die.
/// </summary>
internal sealed class CreepLifecycleStep : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        foreach (var obj in context.State.Objects.Values)
        {
            if (obj.Type is not (RoomObjectTypes.Creep or RoomObjectTypes.PowerCreep))
                continue;

            int? ticksToLivePatch = null;
            RoomObjectActionLogPatch? actionLogPatch = null;
            if (obj.TicksToLive is > 0)
            {
                var next = obj.TicksToLive.Value - 1;
                ticksToLivePatch = next;

                if (next == 0)
                {
                    actionLogPatch = new RoomObjectActionLogPatch(
                        new RoomObjectActionLogDie(context.State.GameTime));
                }
            }

            int? fatiguePatch = null;
            if (obj.Fatigue is > 0 && (obj.MoveBodyParts ?? 0) == 0)
                fatiguePatch = 0;

            if (ticksToLivePatch is null && actionLogPatch is null && fatiguePatch is null)
                continue;

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
}
