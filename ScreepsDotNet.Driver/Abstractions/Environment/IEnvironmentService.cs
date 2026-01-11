namespace ScreepsDotNet.Driver.Abstractions.Environment;

public interface IEnvironmentService
{
    Task<int> GetGameTimeAsync(CancellationToken token = default);
    Task<int> IncrementGameTimeAsync(CancellationToken token = default);

    Task NotifyTickStartedAsync(CancellationToken token = default);
    Task CommitDatabaseBulkAsync(CancellationToken token = default);
    Task SaveIdleTimeAsync(string componentName, TimeSpan duration);

    Task PublishRoomsDoneAsync(int gameTime, CancellationToken token = default);
}
