namespace ScreepsDotNet.Driver.Abstractions.Loops;

public interface IMainLoop
{
    Task RunAsync(CancellationToken token = default);
}

public interface IRunnerLoop
{
    Task RunAsync(CancellationToken token = default);
}

public interface IProcessorLoop
{
    Task RunAsync(CancellationToken token = default);
}

public interface IRunnerLoopWorker
{
    Task HandleUserAsync(string userId, int? queueDepth, CancellationToken token = default);
}

public interface IProcessorLoopWorker
{
    Task HandleRoomAsync(string roomName, int? queueDepth, CancellationToken token = default);
}

public interface IMainLoopGlobalProcessor
{
    Task ExecuteAsync(CancellationToken token = default);
}
