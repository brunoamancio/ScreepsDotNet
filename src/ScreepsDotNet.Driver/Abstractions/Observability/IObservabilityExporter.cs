using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Driver.Abstractions.Observability;

public interface IObservabilityExporter
{
    Task ExportTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default);
    Task ExportWatchdogAlertAsync(RuntimeWatchdogAlert alert, CancellationToken token = default);
    Task ExportRoomStatsAsync(RoomStatsUpdate update, CancellationToken token = default);
}
