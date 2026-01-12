# Runtime Lifecycle Plan
_Last updated: January 11, 2026_

D8 covers the operations executed around each user tick: sandbox creation, console/memory/intents persistence, error reporting, telemetry, and watchdogs.

## Responsibilities
- `makeRuntime(userId)` equivalent: assemble runtime context, execute user code via the sandbox from D3, return outputs (console entries, intents, memory deltas, inter-shard data, errors).
- Persist outputs via storage adapters: `saveUserMemory`, `saveUserMemorySegments`, `saveUserMemoryInterShardSegment`, `saveUserIntents`.
- Publish console logs/errors to pub/sub channels.
- Track per-user CPU usage and enforce bucket/tick limits; integrate with config from D7.
- Provide watchdog telemetry (e.g., log when a runtime exceeds CPU or crashes, expose metrics for heap usage per user).

## Proposed Components
1. **`IRuntimeService`**
```csharp
public interface IRuntimeService
{
    Task<RuntimeExecutionResult> ExecuteAsync(RuntimeExecutionRequest request, CancellationToken token = default);
}
```
- `RuntimeExecutionRequest` includes user ID, code bundle metadata, CPU limits, memory snapshot, static terrain data, etc.
- `RuntimeExecutionResult` includes console entries, intents, memory updates, segments, inter-shard data, CPU used, errors, sandbox metrics.

2. **`RuntimeCoordinator`**
- Sits inside the driver’s runner loop.
- Steps per user:
  1. Load runtime assets (code modules, Memory, segments, static terrain) from storage adapters.
  2. Acquire/initialize sandbox (`V8UserSandbox`).
  3. Call `ExecuteAsync` with tick context (Game time, CPU bucket).
  4. Persist results using storage services.
  5. Publish console logs/errors via `NotificationService`.
  6. Update CPU bucket, notify watchers if limits exceeded.

3. **Error Handling**
- Distinguish between script errors (returned to user console) and system errors (bubble up to driver logs, mark runtime unhealthy).
- When sandbox crashes (`isolate disposed` equivalent), recycle it and report “Script execution terminated: isolate disposed unexpectedly” (matching legacy behavior).

4. **Telemetry & Watchdogs**
- Record per-user metrics (heap usage, last tick time, code timestamp) so `runtime metrics` endpoints can introspect current isolates.
- Implement a watchdog similar to Node driver’s `q.getUnhandledReasons` logging, but tailored to .NET tasks (log unobserved task exceptions, sandbox aborts).

5. **API Shims**
- Provide functions identical to Node driver exports that the engine expects: `sendConsoleMessages`, `sendConsoleError`, `saveUserMemory`, `saveUserMemorySegments`, `saveUserIntents`, `saveUserMemoryInterShardSegment`.
- These functions become small wrappers around the new services so the engine (or compatibility layer) keeps calling the same names.

## Workflow Diagram
```
Runner Loop
  └─> RuntimeCoordinator.ExecuteUserAsync(userId)
        ├─ LoadContextAsync
        ├─ sandbox = SandboxPool.Get(userId)
        ├─ result = sandbox.ExecuteTick(context)
        ├─ PersistMemoryAsync(result.Memory)
        ├─ PersistSegmentsAsync(result.Segments)
        ├─ PersistIntentsAsync(result.Intents)
        ├─ PublishConsoleAsync(result.Console)
        ├─ PublishErrorsAsync(result.Errors)
        └─ UpdateCpuBucketsAsync(result.CpuUsed)
```

## Testing Strategy
- Unit tests for `RuntimeCoordinator` verifying persistence calls fire with the right payloads and order.
- Integration tests running a sample script through the sandbox + coordinator, asserting Memory/intents end up in Mongo/Redis as expected.
- Failure injection tests (sandbox throws, storage unavailable) to ensure runner recovers and requeues the user ticket.

## Next Steps
- Implement `IRuntimeService`/`RuntimeCoordinator` skeletons in code.
- Create mockable services for Memory/Intents/Console persistence to support testing.
- Update `AGENT.md` (D8) to “Plan completed (implementation pending)” until implementation follows.
