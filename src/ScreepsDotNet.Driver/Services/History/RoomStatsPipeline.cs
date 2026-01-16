namespace ScreepsDotNet.Driver.Services.History;

using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Eventing;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Driver.Contracts;

internal sealed class RoomStatsPipeline : IDisposable
{
    private readonly IDriverConfig _config;
    private readonly IRoomStatsRepository _repository;
    private readonly ILogger<RoomStatsPipeline>? _logger;
    private bool _disposed;

    public RoomStatsPipeline(IDriverConfig config, IRoomStatsRepository repository, ILogger<RoomStatsPipeline>? logger = null)
    {
        _config = config;
        _repository = repository;
        _logger = logger;
        _config.ProcessorLoopStage += HandleProcessorLoopStage;
    }

    private async void HandleProcessorLoopStage(object? sender, LoopStageEventArgs args)
    {
        if (!string.Equals(args.Stage, LoopStageNames.Processor.RoomStatsUpdated, StringComparison.Ordinal))
            return;

        if (args.Payload is not RoomStatsUpdate update)
            return;

        try
        {
            await _repository.AppendAsync(update, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Room stats pipeline failed for room {Room} tick {Tick}.", update.Room, update.GameTime);
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
