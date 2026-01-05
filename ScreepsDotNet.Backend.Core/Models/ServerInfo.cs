namespace ScreepsDotNet.Backend.Core.Models;

/// <summary>
/// Wire-safe representation of server metadata returned by the public API.
/// </summary>
public sealed record ServerInfo(string Name, string Build, bool CliEnabled);
