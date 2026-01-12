using System.Text.Json;

namespace ScreepsDotNet.Backend.Core.Repositories;

public interface IUserMemoryRepository
{
    Task<IDictionary<string, object?>> GetMemoryAsync(string userId, CancellationToken cancellationToken = default);

    Task UpdateMemoryAsync(string userId, string? path, JsonElement value, CancellationToken cancellationToken = default);

    Task<string?> GetMemorySegmentAsync(string userId, int segment, CancellationToken cancellationToken = default);

    Task SetMemorySegmentAsync(string userId, int segment, string? data, CancellationToken cancellationToken = default);
}
