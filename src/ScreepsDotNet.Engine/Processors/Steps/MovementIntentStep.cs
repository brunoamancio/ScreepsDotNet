namespace ScreepsDotNet.Engine.Processors.Steps;

using System;
using System.Collections.Generic;
using System.Text.Json;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Applies simple movement intents by reading `move` payloads and updating creep coordinates/fatigue.
/// </summary>
internal sealed class MovementIntentStep : IRoomProcessorStep
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        foreach (var envelope in intents.Users.Values)
        {
            if (envelope?.ObjectsManualJson is null)
                continue;

            foreach (var (objectId, payloadJson) in envelope.ObjectsManualJson)
            {
                if (!context.State.Objects.TryGetValue(objectId, out var obj))
                    continue;

                if (string.IsNullOrWhiteSpace(payloadJson))
                    continue;

                if (!TryGetMoveTarget(payloadJson, out var targetX, out var targetY))
                    continue;

                var patches = new Dictionary<string, object?>
                {
                    ["x"] = Clamp(targetX),
                    ["y"] = Clamp(targetY),
                    ["fatigue"] = Math.Max((obj.Fatigue ?? 0) - 2, 0)
                };

                var json = JsonSerializer.Serialize(patches, _jsonOptions);
                context.MutationWriter.PatchJson(obj.Id, json);
            }
        }

        return Task.CompletedTask;
    }

    private static bool TryGetMoveTarget(string payloadJson, out int x, out int y)
    {
        x = y = 0;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("move", out var move))
                return false;

            if (!move.TryGetProperty("x", out var xElement) || !move.TryGetProperty("y", out var yElement))
                return false;

            x = xElement.GetInt32();
            y = yElement.GetInt32();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int Clamp(int value)
        => Math.Clamp(value, 0, 49);
}
