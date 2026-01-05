using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

namespace ScreepsDotNet.Backend.Http.Health;

internal static class HealthCheckOptionsFactory
{
    public const string HealthEndpoint = "/health";

    private const string JsonContentType = "application/json";

    public static HealthCheckOptions Create()
    {
        return new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = JsonContentType;

                var payload = new
                {
                    status = report.Status.ToString(),
                    timestamp = DateTimeOffset.UtcNow,
                    results = report.Entries.ToDictionary(
                        entry => entry.Key,
                        entry => new
                        {
                            status = entry.Value.Status.ToString(),
                            description = entry.Value.Description,
                            data = entry.Value.Data
                        })
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
        };
    }
}
