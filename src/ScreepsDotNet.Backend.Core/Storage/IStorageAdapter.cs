using ScreepsDotNet.Backend.Core.Models;

namespace ScreepsDotNet.Backend.Core.Storage;

/// <summary>
/// Defines the contract for interacting with the Screeps storage layer.
/// </summary>
public interface IStorageAdapter
{
    Task<StorageStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
