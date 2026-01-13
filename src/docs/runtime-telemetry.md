# Runtime Telemetry Contract

The driver surfaces all runtime health data through `IRuntimeTelemetrySink`. Every loop (runner/main/processor) produces the same payload structure so schedulers, watchdogs, or external observability stacks can subscribe once and fan out.

## Event flow

1. `RuntimeCoordinator` (runner loop) emits a `RuntimeTelemetryPayload` through `IDriverLoopHooks.PublishRuntimeTelemetryAsync`.
2. `DriverLoopHooks` forwards the payload to the configured `IRuntimeTelemetrySink`.
3. `RuntimeTelemetryMonitor` also republishes the payload via `IDriverConfig.RuntimeTelemetry`, enabling legacy-style `config.emit('runtimeTelemetry', …)` listeners.
4. The sink (default `RuntimeTelemetryPipeline`) dispatches to any registered `IRuntimeTelemetryListener` implementations. If no listener is registered, `NullRuntimeTelemetrySink` ensures loops can still emit without throwing.

## `RuntimeTelemetryPayload`

| Field | Type | Description |
| --- | --- | --- |
| `Loop` | `DriverProcessType` | Loop that produced the telemetry (`Runner`, `Processor`, `Main`, `Runtime`). |
| `UserId` | `string` | User the runtime work applied to (blank for loop-level heartbeats). |
| `GameTime` | `int` | Tick being processed. |
| `CpuLimit` | `int` | CPU limit assigned to the tick. |
| `CpuBucket` | `int` | Bucket size at the time the sandbox executed (before replenishment). |
| `CpuUsed` | `int` | CPU consumed by this execution. |
| `TimedOut` | `bool` | Indicates the sandbox exceeded its CPU limit. |
| `ScriptError` | `bool` | Indicates an unhandled exception propagated out of the sandbox. |
| `HeapUsedBytes` | `long` | V8 heap usage reported by the sandbox. |
| `HeapSizeLimitBytes` | `long` | Heap size limit configured for the sandbox isolate. |
| `ErrorMessage` | `string?` | Last error string emitted by the sandbox (console error, require failure, etc.). |
| `QueueDepth` | `int?` | Optional queue depth snapshot collected by the loop before/after executing the user. `null` means the loop did not collect the value. |
| `ColdStartRequested` | `bool` | True when the watchdog requested a cold sandbox restart before executing this tick. |
| `Stage` | `string?` | Optional stage descriptor (`execute`, `idle`, `dequeue`, `drainUsers`, etc.) so schedulers can distinguish runtime execution from queue/backpressure heartbeats. |

All new optional properties default to `null`/`false`, so older components can continue constructing payloads without supplying queue data.

## `RuntimeWatchdogAlert`

`IRuntimeTelemetrySink.PublishWatchdogAlertAsync` delivers structured watchdog notifications whenever a user fails three consecutive ticks:

| Field | Type | Description |
| --- | --- | --- |
| `Payload` | `RuntimeTelemetryPayload` | The payload that triggered the alert (useful for CPU/heap context). |
| `ConsecutiveFailures` | `int` | Number of failures observed so far. |
| `Timestamp` | `DateTimeOffset` | UTC timestamp for the alert (used for throttling/alert dedupe). |

## Null sink fallback

`NullRuntimeTelemetrySink.Instance` implements `IRuntimeTelemetrySink` with no-ops so hosts embedding the driver without registering listeners can still consume `IDriverLoopHooks`. `ServiceCollectionExtensions.AddDriverCore` wires the real pipeline + listeners, but consuming apps can fall back to the null instance if needed.

## Loop stage telemetry

- **Runner loop** now emits `Stage=idle` when the users queue is empty, `Stage=dequeue` when a user is popped, and `Stage=throttleDelay` when the runtime throttle forces an artificial delay. The runtime coordinator emits `Stage=execute` once the sandbox runs.
- **Processor loop** publishes `Stage=idle`/`Stage=dequeue` for the rooms queue before `ProcessorLoopWorker` runs, and the worker itself emits `Stage=processRoom` with the same queue depth snapshot.
- **Main loop** produces `Stage=enqueueUsers`, `Stage=enqueueRooms`, `Stage=drainUsers`, and `Stage=drainRooms` heartbeats with queue depths so operators can see backlog evolution per tick.
- **WorkerScheduler** (when used) sends `Stage=scheduler` telemetry with `ScriptError=true` whenever a background worker crashes before cancellation.

## Consuming the contract

- Register additional `IRuntimeTelemetryListener` instances to push data into metrics pipelines (Prometheus, OTEL, etc.), send alerts, or drive queue throttling.
- Avoid patching loop code directly; instead, inspect the payload in your listener and rely on `RuntimeWatchdogAlert` for high-severity cases.
- When you start emitting queue depth or loop-level telemetry, populate `QueueDepth` and `Stage` so schedulers can reason about backlog size and state transitions without introducing new event types.
