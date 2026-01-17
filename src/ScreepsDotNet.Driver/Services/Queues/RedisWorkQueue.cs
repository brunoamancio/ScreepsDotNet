using System.Diagnostics;
using ScreepsDotNet.Driver.Abstractions.Queues;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using StackExchange.Redis;

namespace ScreepsDotNet.Driver.Services.Queues;

internal sealed class RedisWorkQueue(IRedisConnectionProvider redisProvider, string queueName, QueueMode mode) : IWorkQueueChannel
{
    private IDatabase Database => redisProvider.GetConnection().GetDatabase();

    private string PendingKey => $"queue:{queueName}:pending";
    private string ProcessingKey => $"queue:{queueName}:processing";

    public string Name => queueName;

    public Task EnqueueAsync(string id, CancellationToken token = default)
    {
        EnsureWriteAccess();
        return Database.ListLeftPushAsync(PendingKey, id);
    }

    public async Task EnqueueManyAsync(IEnumerable<string> ids, CancellationToken token = default)
    {
        EnsureWriteAccess();
        var values = ids as string[] ?? ids.ToArray();
        if (values.Length == 0)
            return;

        var payload = Array.ConvertAll(values, value => (RedisValue)value);
        await Database.ListLeftPushAsync(PendingKey, payload).ConfigureAwait(false);
    }

    public async Task<string?> FetchAsync(TimeSpan? waitTimeout = null, CancellationToken token = default)
    {
        EnsureReadAccess();
        if (waitTimeout == TimeSpan.Zero)
            return await PopAsync().ConfigureAwait(false);

        var stopwatch = waitTimeout.HasValue ? Stopwatch.StartNew() : null;
        while (true) {
            token.ThrowIfCancellationRequested();
            var value = await PopAsync().ConfigureAwait(false);
            if (value is not null)
                return value;

            if (waitTimeout.HasValue && stopwatch!.Elapsed >= waitTimeout.Value)
                return null;

            var delay = waitTimeout.HasValue ? waitTimeout.Value - stopwatch!.Elapsed : TimeSpan.FromMilliseconds(100);
            if (delay <= TimeSpan.Zero)
                return null;

            var cappedDelay = delay > TimeSpan.FromMilliseconds(100) ? TimeSpan.FromMilliseconds(100) : delay;
            await Task.Delay(cappedDelay, token).ConfigureAwait(false);
        }
    }

    public Task MarkDoneAsync(string id, CancellationToken token = default)
    {
        EnsureReadAccess();
        return Database.ListRemoveAsync(ProcessingKey, id);
    }

    public async Task ResetAsync(CancellationToken token = default)
    {
        while (await Database.ListLengthAsync(ProcessingKey).ConfigureAwait(false) > 0) {
            token.ThrowIfCancellationRequested();
            var moved = await Database.ListRightPopLeftPushAsync(ProcessingKey, PendingKey).ConfigureAwait(false);
            if (moved.IsNull)
                break;
        }
    }

    public async Task WaitUntilDrainedAsync(CancellationToken token = default)
    {
        EnsureReadAccess();
        while (true) {
            token.ThrowIfCancellationRequested();
            var pending = await Database.ListLengthAsync(PendingKey).ConfigureAwait(false);
            var processing = await Database.ListLengthAsync(ProcessingKey).ConfigureAwait(false);
            if (pending == 0 && processing == 0)
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(100), token).ConfigureAwait(false);
        }
    }

    public async Task<int> GetPendingCountAsync(CancellationToken token = default)
    {
        var length = await Database.ListLengthAsync(PendingKey).ConfigureAwait(false);
        return length > int.MaxValue ? int.MaxValue : (int)length;
    }

    private Task<RedisValue> MovePendingToProcessingAsync()
        => Database.ListRightPopLeftPushAsync(PendingKey, ProcessingKey);

    private async Task<string?> PopAsync()
    {
        var value = await MovePendingToProcessingAsync().ConfigureAwait(false);
        return value.IsNull ? null : value.ToString();
    }

    private void EnsureWriteAccess()
    {
        if (mode == QueueMode.Read)
            throw new InvalidOperationException($"Queue '{Name}' is read-only in this context.");
    }

    private void EnsureReadAccess()
    {
        if (mode == QueueMode.Write)
            throw new InvalidOperationException($"Queue '{Name}' is write-only in this context.");
    }
}
