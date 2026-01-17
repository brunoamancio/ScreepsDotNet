namespace ScreepsDotNet.Backend.Core.Services;

public interface IObjectNameService
{
    Task<string> GenerateSpawnNameAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> IsSpawnNameUniqueAsync(string userId, string name, CancellationToken cancellationToken = default);
}
