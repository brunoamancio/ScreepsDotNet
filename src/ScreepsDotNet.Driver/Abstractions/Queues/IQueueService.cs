namespace ScreepsDotNet.Driver.Abstractions.Queues;

public interface IQueueService
{
    IWorkQueueChannel GetQueue(string name, QueueMode mode = QueueMode.ReadWrite);
    Task ResetAllAsync(CancellationToken token = default);
}

public enum QueueMode
{
    Read,
    Write,
    ReadWrite
}
