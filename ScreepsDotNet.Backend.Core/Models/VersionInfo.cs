namespace ScreepsDotNet.Backend.Core.Models;

public sealed record VersionInfo(int Protocol, bool UseNativeAuth, int Users, ServerData ServerData, string? PackageVersion);
