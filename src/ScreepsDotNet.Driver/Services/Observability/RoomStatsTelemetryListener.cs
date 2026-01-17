namespace ScreepsDotNet.Driver.Services.Observability;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Driver.Abstractions.Observability;
using ScreepsDotNet.Driver.Contracts;

internal sealed class RoomStatsTelemetryListener(
    IObservabilityExporter exporter,
    IOptions<ObservabilityOptions> options,
    ILogger<RoomStatsTelemetryListener>? logger = null) : IRoomStatsListener
{
    private readonly ObservabilityOptions _options = options.Value;
    private readonly IObservabilityExporter _exporter = exporter;
    private readonly ILogger<RoomStatsTelemetryListener>? _logger = logger;

    public Task OnRoomStatsAsync(RoomStatsUpdate update, CancellationToken token = default)
    {
        if (!_options.EnableExporter)
            return Task.CompletedTask;

        try {
            return _exporter.ExportRoomStatsAsync(update, token);
        }
        catch (Exception ex) {
            _logger?.LogError(ex, "Observability exporter failed to handle room stats for room {Room} tick {Tick}.", update.Room, update.GameTime);
            return Task.CompletedTask;
        }
    }
}
