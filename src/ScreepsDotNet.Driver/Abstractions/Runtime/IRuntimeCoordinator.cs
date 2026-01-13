namespace ScreepsDotNet.Driver.Abstractions.Runtime;

public interface IRuntimeCoordinator
{
    Task ExecuteAsync(string userId, int? queueDepth = null, CancellationToken token = default);
}
