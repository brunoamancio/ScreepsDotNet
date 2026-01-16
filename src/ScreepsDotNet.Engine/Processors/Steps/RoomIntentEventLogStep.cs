namespace ScreepsDotNet.Engine.Processors.Steps;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Summarizes manual intents into an event-log payload and stages it through the mutation writer.
/// </summary>
internal sealed class RoomIntentEventLogStep : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var events = new List<RoomIntentEvent>();

        foreach (var (userId, envelope) in intents.Users)
        {
            AppendSpawnEvents(userId, envelope.SpawnIntents, events);
            AppendCreepEvents(userId, envelope.CreepIntents, events);
        }

        if (events.Count == 0)
            return Task.CompletedTask;

        var eventLog = new RoomIntentEventLog(context.State.RoomName, context.State.GameTime, events);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var mapView = new RoomIntentMapView(context.State.RoomName, timestamp, events);

        context.MutationWriter.SetEventLog(eventLog);
        context.MutationWriter.SetMapView(mapView);
        return Task.CompletedTask;
    }

    private static void AppendSpawnEvents(
        string userId,
        IReadOnlyDictionary<string, SpawnIntentEnvelope>? intents,
        List<RoomIntentEvent> events)
    {
        if (intents is null || intents.Count == 0)
            return;

        foreach (var (objectId, intent) in intents)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                continue;

            events.Add(new RoomIntentEvent(
                userId,
                objectId,
                RoomIntentEventKind.Spawn,
                intent,
                null));
        }
    }

    private static void AppendCreepEvents(
        string userId,
        IReadOnlyDictionary<string, CreepIntentEnvelope>? intents,
        List<RoomIntentEvent> events)
    {
        if (intents is null || intents.Count == 0)
            return;

        foreach (var (objectId, intent) in intents)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                continue;

            events.Add(new RoomIntentEvent(
                userId,
                objectId,
                RoomIntentEventKind.Creep,
                null,
                intent));
        }
    }
}
