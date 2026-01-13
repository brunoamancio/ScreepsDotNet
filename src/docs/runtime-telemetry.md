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

## Consuming the contract

- Register additional `IRuntimeTelemetryListener` instances to push data into metrics pipelines (Prometheus, OTEL, etc.), send alerts, or drive queue throttling.
- Avoid patching loop code directly; instead, inspect the payload in your listener and rely on `RuntimeWatchdogAlert` for high-severity cases.
- When you start emitting queue depth or loop-level telemetry, populate `QueueDepth` so schedulers can reason about backlog size without introducing new event types.
