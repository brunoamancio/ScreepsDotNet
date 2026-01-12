namespace ScreepsDotNet.Backend.Core.Cli;

/// <summary>
/// Represents a connected CLI session against the backend.
/// </summary>
public sealed record CliSession(Guid Id, string Username, DateTimeOffset CreatedAtUtc, DateTimeOffset LastActivityUtc);
