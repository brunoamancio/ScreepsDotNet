using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Engine.Processors.Steps;

/// <summary>
/// Removes expired power effects from room objects each tick.
/// </summary>
internal sealed class PowerEffectDecayStep : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var gameTime = context.State.GameTime;

        foreach (var obj in context.State.Objects.Values)
        {
            if (obj.Effects.Count == 0)
                continue;

            var remainingEffects = new Dictionary<PowerTypes, PowerEffectSnapshot>();
            var hasExpiredEffects = false;

            foreach (var (power, effect) in obj.Effects)
            {
                if (effect.EndTime <= gameTime)
                {
                    hasExpiredEffects = true;
                }
                else
                {
                    remainingEffects[power] = effect;
                }
            }

            if (!hasExpiredEffects)
                continue;

            var patch = new RoomObjectPatchPayload
            {
                Effects = remainingEffects
            };

            context.MutationWriter.Patch(obj.Id, patch);
        }

        return Task.CompletedTask;
    }
}
