namespace ScreepsDotNet.Driver.Services.Scheduling;

internal sealed class WorkerScheduler(string name, int concurrency)
{
    private readonly string _name = name;
    private readonly int _concurrency = concurrency > 0
        ? concurrency
        : throw new ArgumentOutOfRangeException(nameof(concurrency));

    public Task RunAsync(Func<CancellationToken, Task> worker, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var tasks = new Task[_concurrency];
        for (var i = 0; i < _concurrency; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await worker(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch
                    {
                        // TODO: connect to logging once available.
                    }
                }
            }, token);
        }

        return Task.WhenAll(tasks);
    }
}
