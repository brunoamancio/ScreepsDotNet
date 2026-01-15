using ScreepsDotNet.Common.Constants;

namespace ScreepsDotNet.Engine.Processors.Steps;

using System;
using System.Collections.Generic;
using System.Text.Json;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Applies a lightweight controller downgrade timer so rooms without active users eventually lose ownership.
/// </summary>
internal sealed class ControllerDowngradeStep : IRoomProcessorStep
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        foreach (var controller in context.State.Objects.Values)
        {
            if (controller.Type != RoomObjectTypes.Controller)
                continue;

            var patches = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (!controller.Store.TryGetValue("downgradeTimer", out var timer))
                continue;

            var next = Math.Max(timer - 1, 0);
            patches["downgradeTimer"] = next;
            if (next == 0)
                patches["upgradeBlocked"] = true;

            var json = JsonSerializer.Serialize(patches, _jsonOptions);
            context.MutationWriter.PatchJson(controller.Id, json);
        }

        return Task.CompletedTask;
    }
}
