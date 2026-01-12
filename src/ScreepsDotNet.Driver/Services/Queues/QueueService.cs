using ScreepsDotNet.Driver.Abstractions.Queues;
using ScreepsDotNet.Storage.MongoRedis.Providers;

namespace ScreepsDotNet.Driver.Services.Queues;

internal sealed class QueueService(IRedisConnectionProvider redisProvider) : IQueueService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, RedisWorkQueue> _queues = new(StringComparer.OrdinalIgnoreCase);

    public IWorkQueueChannel GetQueue(string name, QueueMode mode = QueueMode.ReadWrite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_lock)
        {
            if (_queues.TryGetValue(name, out var queue))
                return queue;

            queue = new RedisWorkQueue(redisProvider, MapLegacyQueueName(name), mode);
            _queues[name] = queue;
            return queue;
        }
    }

    public async Task ResetAllAsync(CancellationToken token = default)
    {
        IWorkQueueChannel[] snapshot;
        lock (_lock)
        {
            snapshot = new IWorkQueueChannel[_queues.Count];
            var index = 0;
            foreach (var queue in _queues.Values)
                snapshot[index++] = queue;
        }

        foreach (var queue in snapshot)
            await queue.ResetAsync(token).ConfigureAwait(false);
    }

    private static string MapLegacyQueueName(string requestedName)
    {
        return requestedName switch
        {
            "rooms" => QueueNames.Rooms,
            "users" => QueueNames.Users,
            _ => requestedName
        };
    }
}
