namespace ScreepsDotNet.Driver.Abstractions.Runtime;

public interface IRuntimeCoordinator
{
    Task ExecuteAsync(string userId, CancellationToken token = default);
}
