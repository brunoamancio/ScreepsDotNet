using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Eventing;
using ScreepsDotNet.Driver.Abstractions.Rooms;

namespace ScreepsDotNet.Driver.Services.History;

internal sealed class RoomHistoryPipeline : IDisposable
{
    private readonly IDriverConfig _config;
    private readonly IRoomDataService _rooms;
    private readonly ILogger<RoomHistoryPipeline>? _logger;
    private bool _disposed;

    public RoomHistoryPipeline(IDriverConfig config, IRoomDataService rooms, ILogger<RoomHistoryPipeline>? logger = null)
    {
        _config = config;
        _rooms = rooms;
        _logger = logger;
        _config.RoomHistorySaved += HandleRoomHistorySaved;
    }

    private async void HandleRoomHistorySaved(object? sender, RoomHistorySavedEventArgs args)
    {
        try
        {
            await PublishMapViewSnapshotAsync(args).ConfigureAwait(false);
            await PublishEventLogAsync(args).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Room history pipeline failed for {Room} baseTick {BaseTick}.", args.RoomName, args.BaseGameTime);
        }
    }

    private Task PublishMapViewSnapshotAsync(RoomHistorySavedEventArgs args)
    {
        var payload = JsonSerializer.Serialize(new RoomHistoryMapViewSnapshot(
            args.RoomName,
            args.BaseGameTime,
            args.Chunk.Timestamp.UtcDateTime,
            args.Chunk.Ticks.Count));
        return _rooms.SaveMapViewAsync(args.RoomName, payload);
    }

    private Task PublishEventLogAsync(RoomHistorySavedEventArgs args)
    {
        var payload = JsonSerializer.Serialize(new RoomHistoryEventLog(
            args.RoomName,
            args.BaseGameTime,
            args.Chunk.Timestamp.UtcDateTime,
            args.Chunk.Ticks));
        return _rooms.SaveRoomEventLogAsync(args.RoomName, payload);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _config.RoomHistorySaved -= HandleRoomHistorySaved;
        _disposed = true;
    }

    private sealed record RoomHistoryMapViewSnapshot(string Room, int BaseTick, DateTime TimestampUtc, int TickCount);
    private sealed record RoomHistoryEventLog(string Room, int BaseTick, DateTime TimestampUtc, IReadOnlyDictionary<int, JsonNode?> Ticks);
}
