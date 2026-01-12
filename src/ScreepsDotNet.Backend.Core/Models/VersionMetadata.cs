namespace ScreepsDotNet.Backend.Core.Models;

/// <summary>
/// Represents the protocol/auth/package metadata exposed by /api/version.
/// </summary>
public sealed record VersionMetadata(int ProtocolVersion, bool UseNativeAuth, string? PackageVersion);
