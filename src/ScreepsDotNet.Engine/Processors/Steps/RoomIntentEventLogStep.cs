namespace ScreepsDotNet.Engine.Processors.Steps;

using System.Collections.Generic;
using System.Text.Json;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Summarizes manual intents into an event-log payload and stages it through the mutation writer.
/// </summary>
internal sealed class RoomIntentEventLogStep : IRoomProcessorStep
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var events = new List<object>();

        foreach (var (userId, envelope) in intents.Users)
        {
            if (envelope?.ObjectsManualJson is null || envelope.ObjectsManualJson.Count == 0)
                continue;

            foreach (var (objectId, payloadJson) in envelope.ObjectsManualJson)
            {
                if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(payloadJson))
                    continue;

                events.Add(new
                {
                    userId,
                    objectId,
                    payload = payloadJson
                });
            }
        }

        if (events.Count == 0)
            return Task.CompletedTask;

        var eventLog = JsonSerializer.Serialize(new
        {
            room = context.State.RoomName,
            tick = context.State.GameTime,
            events
        }, _jsonOptions);

        context.MutationWriter.SetEventLog(eventLog);
        context.MutationWriter.SetMapView(eventLog);
        return Task.CompletedTask;
    }
}
