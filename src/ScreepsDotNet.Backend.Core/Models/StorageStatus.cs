namespace ScreepsDotNet.Backend.Core.Models;

/// <summary>
/// Represents health metadata for an underlying storage provider.
/// </summary>
public sealed record StorageStatus(bool IsConnected, DateTimeOffset? LastSynchronizationUtc, string? Details);
