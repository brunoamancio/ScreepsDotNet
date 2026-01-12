# Runtime Lifecycle Plan
_Last updated: January 12, 2026_

D8 covers the operations executed around each user tick: sandbox creation, console/memory/intents persistence, error reporting, telemetry, and watchdogs.

## Responsibilities
- `makeRuntime(userId)` equivalent: assemble runtime context, execute user code via the sandbox from D3, return outputs (console entries, intents, memory deltas, inter-shard data, errors).
- Persist outputs via storage adapters: `saveUserMemory`, `saveUserMemorySegments`, `saveUserMemoryInterShardSegment`, `saveUserIntents`.
- Expose `RawMemory.*` parity (get/set, segments, inter-shard) so user code can manipulate the same primitives as the Node driver.
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
- Implement a watchdog similar to Node driver’s `q.getUnhandledReasons` logging, but tailored to .NET tasks (log unobserved task exceptions, sandbox aborts, and request sandbox recycling after repeated failures).

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

## Current Progress
- `V8RuntimeSandbox` now injects a Node-like `RawMemory` object (get/set, segment proxy, inter-shard access) and only persists memory/segment/inter-shard data when user code actually mutates them.
- Runner loop saves `Memory`, segments, and inter-shard payloads only when non-null, reducing needless Redis writes.
- Bundle caching keyed by `codeHash` avoids rebuilding module graphs every tick, and `RuntimeSandboxPool` reuses `V8RuntimeSandbox` instances instead of creating a new engine per tick.
- Runtime telemetry (cpu used, timeout/script error flags, heap usage) now flows through `IDriverLoopHooks.PublishRuntimeTelemetryAsync` **and** `config.emit('runtimeTelemetry', payload)`, so both loop hooks and config subscribers can react.
- `RuntimeTelemetryMonitor` subscribes to those events and logs warnings for timeouts/script errors, giving schedulers/logging immediate visibility.
- Watchdog heuristics now track consecutive failures per user, request a cold sandbox restart once the threshold is crossed, and raise throttled `"watchdog"` notifications so operators can follow up; `RuntimeCoordinator` honors these requests by forcing a cold sandbox per affected user.
- New integration tests (`V8RuntimeSandboxTests`) verify memory diffing, RawMemory overrides, and segment persistence.
- `RuntimeCoordinator` now owns the entire lifecycle (context hydration, sandbox execution, persistence, telemetry, CPU bucket bookkeeping), so `RunnerLoopWorker` just schedules users.

## Next Steps
1. Feed telemetry into the future logging/metrics pipeline (or config events) so operators get aggregated CPU/heap warnings.
2. Add watchdog/restart policies on top of the coordinator (detect isolate crashes, recycle sandboxes, emit alerts) and surface metrics via upcoming endpoints.
