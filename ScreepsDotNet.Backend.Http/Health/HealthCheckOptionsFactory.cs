using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

namespace ScreepsDotNet.Backend.Http.Health;

public static class HealthCheckOptionsFactory
{
    public const string HealthEndpoint = "/health";

    private const string JsonContentType = "application/json";

    public static HealthCheckOptions Create()
        => new()
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = JsonContentType;

                var results = report.Entries.ToDictionary(
                    entry => entry.Key,
                    object (entry) => new Dictionary<string, object?>
                    {
                        [HealthResponseFields.Status] = entry.Value.Status.ToString(),
                        [HealthResponseFields.Description] = entry.Value.Description,
                        [HealthResponseFields.Data] = entry.Value.Data
                    });

                var payload = new Dictionary<string, object?>
                {
                    [HealthResponseFields.Status] = report.Status.ToString(),
                    [HealthResponseFields.Timestamp] = DateTimeOffset.UtcNow,
                    [HealthResponseFields.Results] = results
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
        };
}
