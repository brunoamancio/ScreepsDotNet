using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Observability;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Driver.Services.Observability;

namespace ScreepsDotNet.Driver.Tests.Observability;

public sealed class ObservabilityTelemetryListenerTests
{
    [Fact]
    public async Task OnTelemetryAsync_ExportsWhenEnabled()
    {
        var exporter = new RecordingExporter();
        var listener = new ObservabilityTelemetryListener(
            exporter,
            Options.Create(new ObservabilityOptions { EnableExporter = true }),
            NullLogger<ObservabilityTelemetryListener>.Instance);

        var payload = CreatePayload();
        await listener.OnTelemetryAsync(payload, TestContext.Current.CancellationToken);

        Assert.Single(exporter.Telemetry);
        Assert.Same(payload, exporter.Telemetry[0]);
    }

    [Fact]
    public async Task OnTelemetryAsync_SkipsWhenDisabled()
    {
        var exporter = new RecordingExporter();
        var listener = new ObservabilityTelemetryListener(
            exporter,
            Options.Create(new ObservabilityOptions { EnableExporter = false }),
            NullLogger<ObservabilityTelemetryListener>.Instance);

        await listener.OnTelemetryAsync(CreatePayload(), TestContext.Current.CancellationToken);

        Assert.Empty(exporter.Telemetry);
    }

    private static RuntimeTelemetryPayload CreatePayload() =>
        new(
            Loop: DriverProcessType.Runner,
            UserId: "user",
            GameTime: 123,
            CpuLimit: 50,
            CpuBucket: 1000,
            CpuUsed: 20,
            TimedOut: false,
            ScriptError: false,
            HeapUsedBytes: 0,
            HeapSizeLimitBytes: 0,
            ErrorMessage: null,
            QueueDepth: 10,
            ColdStartRequested: false,
            Stage: "test");

    private sealed class RecordingExporter : IObservabilityExporter
    {
        public List<RuntimeTelemetryPayload> Telemetry { get; } = [];
        public List<RuntimeWatchdogAlert> Alerts { get; } = [];
        public List<RoomStatsUpdate> RoomStats { get; } = [];

        public Task ExportTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default)
        {
            Telemetry.Add(payload);
            return Task.CompletedTask;
        }

        public Task ExportWatchdogAlertAsync(RuntimeWatchdogAlert alert, CancellationToken token = default)
        {
            Alerts.Add(alert);
            return Task.CompletedTask;
        }

        public Task ExportRoomStatsAsync(RoomStatsUpdate update, CancellationToken token = default)
        {
            RoomStats.Add(update);
            return Task.CompletedTask;
        }
    }
}
