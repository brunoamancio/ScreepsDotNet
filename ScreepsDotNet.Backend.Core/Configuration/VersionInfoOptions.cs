namespace ScreepsDotNet.Backend.Core.Configuration;

public sealed class VersionInfoOptions
{
    public const string SectionName = "VersionInfo";

    public int ProtocolVersion { get; init; } = 14;

    public bool UseNativeAuth { get; init; }

    public string? PackageVersion { get; init; }
}
