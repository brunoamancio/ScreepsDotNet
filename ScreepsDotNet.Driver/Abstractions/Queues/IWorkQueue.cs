namespace ScreepsDotNet.Driver.Abstractions.Queues;

public interface IWorkQueueChannel
{
    string Name { get; }
    Task EnqueueAsync(string id, CancellationToken token = default);
    Task EnqueueManyAsync(IEnumerable<string> ids, CancellationToken token = default);
    Task<string?> FetchAsync(TimeSpan? waitTimeout = null, CancellationToken token = default);
    Task MarkDoneAsync(string id, CancellationToken token = default);
    Task ResetAsync(CancellationToken token = default);
    Task WaitUntilDrainedAsync(CancellationToken token = default);
}
