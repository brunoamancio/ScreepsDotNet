namespace ScreepsDotNet.Engine.Processors.Steps;

using System;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Applies simple movement intents by reading `move` payloads and updating creep coordinates/fatigue.
/// </summary>
internal sealed class MovementIntentStep : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        foreach (var envelope in intents.Users.Values)
        {
            if (envelope?.CreepIntents is null || envelope.CreepIntents.Count == 0)
                continue;

            foreach (var (objectId, creepIntent) in envelope.CreepIntents)
            {
                if (creepIntent?.Move is null)
                    continue;

                if (!context.State.Objects.TryGetValue(objectId, out var obj))
                    continue;

                var patch = new RoomObjectPatchPayload
                {
                    Position = new RoomObjectPositionPatch(
                        Clamp(creepIntent.Move.X),
                        Clamp(creepIntent.Move.Y)),
                    Fatigue = Math.Max((obj.Fatigue ?? 0) - 2, 0)
                };

                context.MutationWriter.Patch(obj.Id, patch);
            }
        }

        return Task.CompletedTask;
    }
    private static int Clamp(int value)
        => Math.Clamp(value, 0, 49);
}
