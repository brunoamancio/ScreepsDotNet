namespace ScreepsDotNet.Backend.Core.Configuration;

/// <summary>
/// Configurable metadata describing the running Screeps backend instance.
/// </summary>
public sealed class ServerInfoOptions
{
    public const string SectionName = "ServerInfo";

    public string Name { get; init; } = "ScreepsDotNet";

    public string Build { get; init; } = "0.0.1-dev";

    public bool CliEnabled { get; init; }
}
