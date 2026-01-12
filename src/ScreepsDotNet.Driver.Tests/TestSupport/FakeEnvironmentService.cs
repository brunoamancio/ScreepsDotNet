using ScreepsDotNet.Driver.Abstractions.Environment;

namespace ScreepsDotNet.Driver.Tests.TestSupport;

internal sealed class FakeEnvironmentService : IEnvironmentService
{
    private int _gameTime;
    private readonly Dictionary<string, int> _ints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _bools = new(StringComparer.Ordinal);

    public Task<int> GetGameTimeAsync(CancellationToken token = default)
        => Task.FromResult(_gameTime);

    public Task<int> IncrementGameTimeAsync(CancellationToken token = default)
        => Task.FromResult(++_gameTime);

    public Task NotifyTickStartedAsync(CancellationToken token = default) => Task.CompletedTask;

    public Task CommitDatabaseBulkAsync(CancellationToken token = default) => Task.CompletedTask;

    public Task SaveIdleTimeAsync(string componentName, TimeSpan duration) => Task.CompletedTask;

    public Task PublishRoomsDoneAsync(int gameTime, CancellationToken token = default) => Task.CompletedTask;

    public Task<int?> GetMainLoopMinDurationAsync(CancellationToken token = default)
        => Task.FromResult(GetInt(nameof(GetMainLoopMinDurationAsync)));

    public Task SetMainLoopMinDurationAsync(int value, CancellationToken token = default)
        => SetInt(nameof(GetMainLoopMinDurationAsync), value);

    public Task<int?> GetMainLoopResetIntervalAsync(CancellationToken token = default)
        => Task.FromResult(GetInt(nameof(GetMainLoopResetIntervalAsync)));

    public Task SetMainLoopResetIntervalAsync(int value, CancellationToken token = default)
        => SetInt(nameof(GetMainLoopResetIntervalAsync), value);

    public Task<int?> GetCpuMaxPerTickAsync(CancellationToken token = default)
        => Task.FromResult(GetInt(nameof(GetCpuMaxPerTickAsync)));

    public Task SetCpuMaxPerTickAsync(int value, CancellationToken token = default)
        => SetInt(nameof(GetCpuMaxPerTickAsync), value);

    public Task<int?> GetCpuBucketSizeAsync(CancellationToken token = default)
        => Task.FromResult(GetInt(nameof(GetCpuBucketSizeAsync)));

    public Task SetCpuBucketSizeAsync(int value, CancellationToken token = default)
        => SetInt(nameof(GetCpuBucketSizeAsync), value);

    public Task<int?> GetHistoryChunkSizeAsync(CancellationToken token = default)
        => Task.FromResult(GetInt(nameof(GetHistoryChunkSizeAsync)));

    public Task SetHistoryChunkSizeAsync(int value, CancellationToken token = default)
        => SetInt(nameof(GetHistoryChunkSizeAsync), value);

    public Task<bool?> GetUseSigintTimeoutAsync(CancellationToken token = default)
        => Task.FromResult(GetBool(nameof(GetUseSigintTimeoutAsync)));

    public Task SetUseSigintTimeoutAsync(bool value, CancellationToken token = default)
        => SetBool(nameof(GetUseSigintTimeoutAsync), value);

    public Task<bool?> GetEnableInspectorAsync(CancellationToken token = default)
        => Task.FromResult(GetBool(nameof(GetEnableInspectorAsync)));

    public Task SetEnableInspectorAsync(bool value, CancellationToken token = default)
        => SetBool(nameof(GetEnableInspectorAsync), value);

    private int? GetInt(string key) => _ints.TryGetValue(key, out var value) ? value : null;
    private Task SetInt(string key, int value)
    {
        _ints[key] = value;
        return Task.CompletedTask;
    }

    private bool? GetBool(string key) => _bools.TryGetValue(key, out var value) ? value : null;
    private Task SetBool(string key, bool value)
    {
        _bools[key] = value;
        return Task.CompletedTask;
    }
}
