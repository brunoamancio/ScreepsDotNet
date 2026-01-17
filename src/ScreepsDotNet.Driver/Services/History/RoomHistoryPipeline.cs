using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Eventing;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Driver.Services.History;

internal sealed class RoomHistoryPipeline : IDisposable
{
    private readonly IDriverConfig _config;
    private readonly IRoomMutationDispatcher _mutationDispatcher;
    private readonly ILogger<RoomHistoryPipeline>? _logger;
    private bool _disposed;

    public RoomHistoryPipeline(IDriverConfig config, IRoomMutationDispatcher mutationDispatcher, ILogger<RoomHistoryPipeline>? logger = null)
    {
        _config = config;
        _mutationDispatcher = mutationDispatcher;
        _logger = logger;
        _config.RoomHistorySaved += HandleRoomHistorySaved;
    }

    private void HandleRoomHistorySaved(object? sender, RoomHistorySavedEventArgs args)
        => _ = PublishArtifactsSafeAsync(args);

    private async Task PublishArtifactsSafeAsync(RoomHistorySavedEventArgs args)
    {
        try
        {
            await PublishArtifactsAsync(args).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Room history pipeline failed for {Room} baseTick {BaseTick}.", args.RoomName, args.BaseGameTime);
        }
    }

    private Task PublishArtifactsAsync(RoomHistorySavedEventArgs args)
    {
        var mapViewPayload = new RoomHistoryMapViewSnapshot(
            args.RoomName,
            args.BaseGameTime,
            args.Chunk.Timestamp.UtcDateTime,
            args.Chunk.Ticks.Count);
        var eventLogPayload = new RoomHistoryEventLog(
            args.RoomName,
            args.BaseGameTime,
            args.Chunk.Timestamp.UtcDateTime,
            args.Chunk.Ticks);

        var batch = new RoomMutationBatch(
            args.RoomName,
            [],
            [],
            [],
            null,
            mapViewPayload,
            eventLogPayload);

        return _mutationDispatcher.ApplyAsync(batch);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _config.RoomHistorySaved -= HandleRoomHistorySaved;
        _disposed = true;
    }

    private sealed record RoomHistoryMapViewSnapshot(string Room, int BaseTick, DateTime TimestampUtc, int TickCount) : IRoomMapViewPayload;
    private sealed record RoomHistoryEventLog(string Room, int BaseTick, DateTime TimestampUtc, IReadOnlyDictionary<int, JsonNode?> Ticks) : IRoomEventLogPayload;
}
