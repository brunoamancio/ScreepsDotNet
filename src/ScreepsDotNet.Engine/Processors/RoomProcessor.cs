namespace ScreepsDotNet.Engine.Processors;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.GlobalMutations;
using ScreepsDotNet.Engine.Data.GlobalState;
using ScreepsDotNet.Engine.Data.Memory;
using ScreepsDotNet.Engine.Data.Rooms;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Telemetry;
using ScreepsDotNet.Engine.Validation;

internal sealed class RoomProcessor(
    IRoomStateProvider roomStateProvider,
    IGlobalStateProvider globalStateProvider,
    IRoomMutationWriterFactory mutationWriterFactory,
    IGlobalMutationWriterFactory globalMutationWriterFactory,
    IUserMemorySink memorySink,
    IHistoryService historyService,
    IEnumerable<IRoomProcessorStep> steps,
    IEngineTelemetrySink telemetrySink,
    IValidationStatisticsSink? validationStatsSink = null,
    ILogger<RoomProcessor>? logger = null) : IRoomProcessor
{
    public async Task ProcessAsync(string roomName, int gameTime, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        var stopwatch = Stopwatch.StartNew();

        var state = await roomStateProvider.GetRoomStateAsync(roomName, gameTime, token).ConfigureAwait(false);
        var globalState = await globalStateProvider.GetGlobalStateAsync(state.GameTime, token).ConfigureAwait(false);
        var writer = mutationWriterFactory.Create(roomName);
        var globalWriter = globalMutationWriterFactory.Create();
        var statsSink = new RoomStatsSink(historyService.CreateRoomStatsUpdater(roomName));
        globalState.ExitTopology.TryGetValue(roomName, out var exitTopology);
        var context = new RoomProcessorContext(state, writer, statsSink, globalWriter, exitTopology);

        try {
            foreach (var step in steps)
                await step.ExecuteAsync(context, token).ConfigureAwait(false);

            // Placeholder logging until real simulation steps populate mutations/memory.
            if (!steps.Any())
                logger?.LogDebug("RoomProcessor tick {Tick} room {Room} has {ObjectCount} objects.", state.GameTime, state.RoomName, state.Objects.Count);

            await writer.FlushAsync(token).ConfigureAwait(false);
            await globalWriter.FlushAsync(token).ConfigureAwait(false);

            await context.Stats.FlushAsync(state.GameTime, token).ConfigureAwait(false);

            await context.FlushMemoryAsync(memorySink, token).ConfigureAwait(false);

            stopwatch.Stop();

            // Emit telemetry
            var validationStats = validationStatsSink?.GetStatistics();

            // Calculate total intent count from all users
            var totalIntentCount = state.Intents?.Users.Values
                .Sum(envelope => envelope.ObjectIntents.Values.Sum(list => list.Count) +
                                 envelope.SpawnIntents.Count +
                                 envelope.CreepIntents.Count) ?? 0;

            var payload = new EngineTelemetryPayload(
                RoomName: roomName,
                GameTime: gameTime,
                ProcessingTimeMs: stopwatch.ElapsedMilliseconds,
                ObjectCount: state.Objects.Count,
                IntentCount: totalIntentCount,
                ValidatedIntentCount: validationStats?.ValidIntentsCount ?? 0,
                RejectedIntentCount: validationStats?.RejectedIntentsCount ?? 0,
                MutationCount: writer.GetMutationCount(),
                RejectionsByErrorCode: validationStats?.RejectionsByErrorCode is not null
                    ? new Dictionary<string, int>(validationStats.RejectionsByErrorCode.Select(kvp => new KeyValuePair<string, int>(kvp.Key.ToString(), kvp.Value)))
                    : null,
                RejectionsByIntentType: validationStats?.RejectionsByIntentType is not null
                    ? new Dictionary<string, int>(validationStats.RejectionsByIntentType)
                    : null,
                StepTimingsMs: null  // Optional: collect per-step timings
            );

            await telemetrySink.PublishRoomTelemetryAsync(payload, token).ConfigureAwait(false);

            // Reset validation stats after export (per-tick reset)
            validationStatsSink?.Reset();
        }
        finally {
            writer.Reset();
            globalWriter.Reset();
            context.ClearPendingMemory();
        }
    }
}
