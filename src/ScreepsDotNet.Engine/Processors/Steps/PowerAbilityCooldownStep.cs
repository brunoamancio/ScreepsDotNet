using ScreepsDotNet.Common.Constants;

namespace ScreepsDotNet.Engine.Processors.Steps;

using System.Collections.Generic;
using System.Text.Json;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Applies a simple cooldown for power creep abilities and reduces spawn cooldown timers.
/// </summary>
internal sealed class PowerAbilityCooldownStep : IRoomProcessorStep
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        foreach (var obj in context.State.Objects.Values)
        {
            if (obj.Type != RoomObjectTypes.PowerCreep)
                continue;

            var patches = new Dictionary<string, object?>(StringComparer.Ordinal);

            if (obj.Structure is not null && obj.Structure.Hits is > 0)
            {
                var reduced = Math.Max((obj.Structure.Hits ?? 0) - 10, 0);
                if (reduced != obj.Structure.Hits)
                    patches["_structureHits"] = reduced;
            }

            if (obj.Store.TryGetValue("spawnCooldownTime", out var cooldown) && cooldown > 0)
                patches["spawnCooldownTime"] = Math.Max(cooldown - 1, 0);

            if (patches.Count == 0)
                continue;

            var json = JsonSerializer.Serialize(patches, _jsonOptions);
            context.MutationWriter.PatchJson(obj.Id, json);
        }

        return Task.CompletedTask;
    }
}
