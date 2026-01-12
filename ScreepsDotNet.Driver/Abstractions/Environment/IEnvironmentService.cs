namespace ScreepsDotNet.Driver.Abstractions.Environment;

public interface IEnvironmentService
{
    Task<int> GetGameTimeAsync(CancellationToken token = default);
    Task<int> IncrementGameTimeAsync(CancellationToken token = default);

    Task NotifyTickStartedAsync(CancellationToken token = default);
    Task CommitDatabaseBulkAsync(CancellationToken token = default);
    Task SaveIdleTimeAsync(string componentName, TimeSpan duration);

    Task PublishRoomsDoneAsync(int gameTime, CancellationToken token = default);

    Task<int?> GetMainLoopMinDurationAsync(CancellationToken token = default);
    Task SetMainLoopMinDurationAsync(int value, CancellationToken token = default);

    Task<int?> GetMainLoopResetIntervalAsync(CancellationToken token = default);
    Task SetMainLoopResetIntervalAsync(int value, CancellationToken token = default);

    Task<int?> GetCpuMaxPerTickAsync(CancellationToken token = default);
    Task SetCpuMaxPerTickAsync(int value, CancellationToken token = default);

    Task<int?> GetCpuBucketSizeAsync(CancellationToken token = default);
    Task SetCpuBucketSizeAsync(int value, CancellationToken token = default);

    Task<int?> GetHistoryChunkSizeAsync(CancellationToken token = default);
    Task SetHistoryChunkSizeAsync(int value, CancellationToken token = default);

    Task<bool?> GetUseSigintTimeoutAsync(CancellationToken token = default);
    Task SetUseSigintTimeoutAsync(bool value, CancellationToken token = default);

    Task<bool?> GetEnableInspectorAsync(CancellationToken token = default);
    Task SetEnableInspectorAsync(bool value, CancellationToken token = default);
}
