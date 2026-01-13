namespace ScreepsDotNet.Driver.Services.Observability;

public sealed class ObservabilityOptions
{
    public bool EnableExporter { get; init; }
    public string? Provider { get; init; }
}
