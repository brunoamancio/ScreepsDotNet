using Microsoft.Extensions.Diagnostics.HealthChecks;
using ScreepsDotNet.Backend.Core.Storage;

namespace ScreepsDotNet.Backend.Http.Health;

public sealed class StorageHealthCheck(IStorageAdapter storageAdapter) : IHealthCheck
{
    public const string HealthCheckName = "storage";

    private const string HealthyMessage = "Storage responded successfully";
    private const string UnhealthyFallbackMessage = "Storage is unavailable";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var status = await storageAdapter.GetStatusAsync(cancellationToken);

        return status.IsConnected ? HealthCheckResult.Healthy(HealthyMessage)
                                  : HealthCheckResult.Unhealthy(status.Details ?? UnhealthyFallbackMessage);
    }
}
