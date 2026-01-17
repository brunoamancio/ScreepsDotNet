using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Observability;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Driver.Services.Observability;

namespace ScreepsDotNet.Driver.Tests.Observability;

public sealed class RoomStatsTelemetryListenerTests
{
    [Fact]
    public async Task OnRoomStatsAsync_WhenEnabled_Exports()
    {
        var exporter = new RecordingObservabilityExporter();
        var options = Options.Create(new ObservabilityOptions { EnableExporter = true });
        var listener = new RoomStatsTelemetryListener(exporter, options);

        var update = CreateUpdate();

        await listener.OnRoomStatsAsync(update, TestContext.Current.CancellationToken);

        Assert.Single(exporter.RoomStats);
        Assert.Same(update, exporter.RoomStats[0]);
    }

    [Fact]
    public async Task OnRoomStatsAsync_WhenDisabled_Skips()
    {
        var exporter = new RecordingObservabilityExporter();
        var options = Options.Create(new ObservabilityOptions { EnableExporter = false });
        var listener = new RoomStatsTelemetryListener(exporter, options);

        await listener.OnRoomStatsAsync(CreateUpdate(), TestContext.Current.CancellationToken);

        Assert.Empty(exporter.RoomStats);
    }

    private static RoomStatsUpdate CreateUpdate()
    {
        var metrics = new Dictionary<string, IReadOnlyDictionary<string, int>>
        {
            ["user1"] = new Dictionary<string, int> { ["spawnsCreate"] = 1 }
        };
        return new RoomStatsUpdate("W1N1", 1000, metrics);
    }

    private sealed class RecordingObservabilityExporter : IObservabilityExporter
    {
        public List<RoomStatsUpdate> RoomStats { get; } = [];

        public Task ExportTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default) => Task.CompletedTask;

        public Task ExportWatchdogAlertAsync(RuntimeWatchdogAlert alert, CancellationToken token = default) => Task.CompletedTask;

        public Task ExportRoomStatsAsync(RoomStatsUpdate update, CancellationToken token = default)
        {
            RoomStats.Add(update);
            return Task.CompletedTask;
        }
    }
}
