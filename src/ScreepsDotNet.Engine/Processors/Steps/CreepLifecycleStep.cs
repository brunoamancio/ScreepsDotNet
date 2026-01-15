using ScreepsDotNet.Common.Constants;

namespace ScreepsDotNet.Engine.Processors.Steps;

using System.Collections.Generic;
using System.Text.Json;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Applies a minimal creep lifecycle: decrement TTL, clear fatigue for creeps that cannot move,
/// and emit an actionLog entry when a creep is about to die.
/// </summary>
internal sealed class CreepLifecycleStep : IRoomProcessorStep
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        foreach (var obj in context.State.Objects.Values)
        {
            if (obj.Type is not (RoomObjectTypes.Creep or RoomObjectTypes.PowerCreep))
                continue;

            var patches = new Dictionary<string, object?>(StringComparer.Ordinal);

            if (obj.TicksToLive is > 0)
            {
                var next = obj.TicksToLive.Value - 1;
                patches["ticksToLive"] = next;

                if (next == 0)
                {
                    patches["_actionLog"] = new
                    {
                        die = new { time = context.State.GameTime }
                    };
                }
            }

            if (obj.Fatigue is > 0 && obj.Store.TryGetValue("move", out var moveParts) && moveParts == 0)
                patches["fatigue"] = 0;

            if (patches.Count == 0)
                continue;

            var json = JsonSerializer.Serialize(patches, _jsonOptions);
            context.MutationWriter.PatchJson(obj.Id, json);
        }

        return Task.CompletedTask;
    }
}
