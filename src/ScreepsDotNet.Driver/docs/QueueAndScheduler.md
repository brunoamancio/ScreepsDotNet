# Queue & Scheduler Plan
_Last updated: January 11, 2026_

D4 covers the driver’s queue system (rooms/users work queues) and the scheduling primitives used by the engine loops.

## Requirements Recap
- **API parity:** Must expose the same surface as `@screeps/driver/lib/queue.js`: `create(name)`, `fetch()`, `markDone(id)`, `add(id)`, `addMulti(ids)`, `whenAllDone()`, `reset()`, plus `resetAll()` and `createDoneListener(name, fn)`.
- **Durable semantics:** Items survive process restarts; unfinished items return to the queue if not marked done (legacy behavior via `queue.reset` in watchdog).
- **Low latency:** `fetch()` should block briefly (or poll efficiently) to keep the runner saturated without hot loops.
- **Graceful shutdown:** On SIGTERM equivalent, stop handing out new work and allow in-flight items to finish.
- **Scheduler helper:** Provide a managed alternative to `driver.startLoop(name, fn)` that can run multiple concurrent workers per queue.

## Architecture
```
QueueService
├── IWorkQueueProvider
│   ├── WorkQueue (rooms)
│   └── WorkQueue (users)
└── QueueMonitor (optional watchdog)

SchedulerService
└── RunnerLoop (configurable worker pool powered by Task-based concurrency)
```

### Storage Layer
Use Redis lists and hashes to track pending/processing state per queue:
- Keys per queue: `queue:<name>:pending`, `queue:<name>:processing`.
- `Enqueue`: `LPUSH pending` (or `RPUSH` to maintain order).
- `Fetch`: `BRPOPLPUSH pending processing timeout` → returns an item while also tracking it in `processing`.
- `MarkDone`: `LREM processing item`.
- `WhenAllDone`: poll `pending` and `processing` lengths until both zero.
- `Reset`: move all items from `processing` back to `pending` (`RPOPLPUSH` in a loop). `resetAll` iterates known queues.

### Interfaces
```csharp
public interface IWorkQueue
{
    string Name { get; }
    Task EnqueueAsync(string id, CancellationToken token = default);
    Task EnqueueManyAsync(IEnumerable<string> ids, CancellationToken token = default);
    Task<string?> FetchAsync(TimeSpan? wait = null, CancellationToken token = default);
    Task MarkDoneAsync(string id, CancellationToken token = default);
    Task ResetAsync(CancellationToken token = default);
    Task WaitUntilDrainedAsync(CancellationToken token = default);
}

public interface IQueueService
{
    IWorkQueue GetQueue(string name, QueueMode mode = QueueMode.ReadWrite);
    Task ResetAllAsync(CancellationToken token = default);
    Task<IDisposable> SubscribeDoneAsync(string name, Func<string,Task> handler);
}
```
- `QueueMode` kept for compatibility (legacy call sites pass `"read"/"write"`).

### Scheduler
Replace `driver.startLoop` with a managed helper:
```csharp
public sealed class WorkerScheduler
{
    public WorkerScheduler(string name, int concurrency);
    public Task RunAsync(Func<CancellationToken, Task> worker, CancellationToken token);
}
```
- Uses `Task.Run` with a semaphore to cap concurrency.
- Accepts `RUNNER_THREADS` equivalent from config.
- Provides cancellation support so the driver can stop the runner cleanly.

### Shutdown Semantics
- `QueueService` exposes `Stop()` that sets a flag to prevent new `Fetch` calls from blocking indefinitely when the process is shutting down.
- In-flight workers still call `MarkDone`, ensuring items don’t get lost.

### Telemetry
- Queue depth metrics (pending counts for both rooms/users) now flow through `IDriverLoopHooks.PublishRuntimeTelemetryAsync` with stage tags (`enqueue*`, `drain*`, `idle`, `dequeue`, etc.), so backlog charts can be driven from the unified telemetry sink.
- Scheduler crashes emit `Stage=scheduler` telemetry via `WorkerScheduler`, replacing the old TODO about logging those errors.

## Implementation Steps
1. Introduce `IWorkQueue`/`IQueueService` interfaces under `ScreepsDotNet.Driver.Queues`.
2. Implement `RedisWorkQueue` using StackExchange.Redis (leveraging the storage adapters defined in D2).
3. Build `WorkerScheduler` + unit tests ensuring concurrency caps and cancellation work as expected.
4. Provide adapter methods that mimic the dynamic object returned by the Node driver so the legacy engine can call into the .NET queues during the transition phase.

With this plan we can proceed to coding the queue service and mark D4 complete once the interfaces + initial implementations exist.
