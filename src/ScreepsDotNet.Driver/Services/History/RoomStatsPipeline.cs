namespace ScreepsDotNet.Driver.Services.History;

using Microsoft.Extensions.Logging;
using System.Linq;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Eventing;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Driver.Contracts;

internal sealed class RoomStatsPipeline : IDisposable
{
    private readonly IDriverConfig _config;
    private readonly IRoomStatsRepository _repository;
    private readonly IReadOnlyList<IRoomStatsListener> _listeners;
    private readonly ILogger<RoomStatsPipeline>? _logger;
    private bool _disposed;

    public RoomStatsPipeline(
        IDriverConfig config,
        IRoomStatsRepository repository,
        IEnumerable<IRoomStatsListener> listeners,
        ILogger<RoomStatsPipeline>? logger = null)
    {
        _config = config;
        _repository = repository;
        _listeners = (listeners).ToArray();
        _logger = logger;
        _config.ProcessorLoopStage += HandleProcessorLoopStage;
    }

    private void HandleProcessorLoopStage(object? sender, LoopStageEventArgs args)
    {
        if (!string.Equals(args.Stage, LoopStageNames.Processor.RoomStatsUpdated, StringComparison.Ordinal))
            return;

        if (args.Payload is not RoomStatsUpdate update)
            return;

        _ = ProcessRoomStatsAsync(update);
    }

    private async Task ProcessRoomStatsAsync(RoomStatsUpdate update)
    {
        try
        {
            await _repository.AppendAsync(update, CancellationToken.None).ConfigureAwait(false);
            await NotifyListenersAsync(update).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Room stats pipeline failed for room {Room} tick {Tick}.", update.Room, update.GameTime);
        }
    }

    private async Task NotifyListenersAsync(RoomStatsUpdate update)
    {
        if (_listeners.Count == 0)
            return;

        foreach (var listener in _listeners)
        {
            try
            {
                await listener.OnRoomStatsAsync(update, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "Room stats listener {Listener} failed for room {Room} tick {Tick}.",
                    listener.GetType().Name,
                    update.Room,
                    update.GameTime);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _config.ProcessorLoopStage -= HandleProcessorLoopStage;
        _disposed = true;
    }
}
