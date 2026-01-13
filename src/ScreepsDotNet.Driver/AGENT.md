# Driver AGENT

Use this file for day-to-day coordination inside `ScreepsDotNet.Driver`. The full roadmap, plan docs, and milestone status live in `docs/driver.md` plus the per-topic files under `src/ScreepsDotNet.Driver/docs/`.

## Snapshot (January 2026)

- Queue infrastructure, worker scheduler skeleton, bulk writers, room/user services, notification/history services, and the ClearScript runtime coordinator are merged; runner/main/processor loops can execute a simplified tick.
- Runtime sandbox now exposes Node-style `RawMemory`/segment/inter-shard APIs and only persists data when user code mutates it; `V8RuntimeSandboxTests` cover the new behavior.
- Sandbox pooling + bundle caching keeps V8 isolates hot; runtime telemetry now flows through a dedicated monitor (`RuntimeTelemetryMonitor`) hooked into `IDriverConfig.RuntimeTelemetry`, and the new `RuntimeCoordinator` owns context hydration/persistence so `RunnerLoopWorker` just schedules users.
- Runtime telemetry (CPU used, timeout/script error flags, heap usage) now flows through `IDriverLoopHooks` into `IRuntimeTelemetrySink`, so schedulers/loggers/metrics collectors can tap into a single fan-out point.
- Scheduler telemetry listener + throttle registry watch the same events and temporarily delay users that repeatedly time out/script error, preventing the runner queue from being monopolized while still letting operators log/observe the behavior.
- Watchdog heuristics now track consecutive failures per user, request a cold sandbox restart, and raise throttled `"watchdog"` notifications; `RuntimeCoordinator` honors the cold-start flag so repeat offenders run in a fresh isolate.
- Room history uploads now durably persist to Mongo (`rooms.history`) before we emit `roomHistorySaved`, so downstream uploaders can treat the chunk as committed storage instead of transient Redis data.
- Native pathfinder lives in `src/native/pathfinder`; `PathfinderService` now always requires those binaries (managed A* fallback removed on January 13, 2026). Regression baselines (multi-room, flee, portal callbacks, and room-callback block semantics) run in `PathfinderNativeIntegrationTests`.
- Managed fallback now honors `roomCallback` cost matrices, room blocking, and flee goals so local builds without the native binary still behave like the Node driver for same-room searches.
- Compatibility shim (D10) and deeper processor logic (intent application, map view, stats) remain after runtime/pathfinder stabilization.
- `ObservabilityTelemetryListener` + `ObservabilityOptions` let you flip runtime telemetry export on/off per environment; by default dev builds keep the exporter disabled, while prod can register a custom `IObservabilityExporter` to push into Prometheus/OTLP/etc. without modifying loop code.

See `docs/driver.md` for the latest D1–D10 table and links to `DriverApi.md`, `QueueAndScheduler.md`, etc.

## Active focus

1. Finish D6 (native pathfinder swap + managed bindings) and D7/D8 polish items called out in `docs/driver.md`.
2. Expand processor handlers beyond metadata snapshots (creep/tower/link/lab done; need movement/controller/power actions).
3. Keep `DriverLoopHooks` as the integration surface—don’t let loops call services directly.

## How to work here

- Rely on cross-cutting settings from `Directory.Build.props`; avoid duplicating target framework or analyzer settings.
- Implicit usings are enabled solution-wide; only add explicit `using` directives when a namespace isn’t already imported. Dropping redundant `System.*` usings keeps IDE warnings down.
- Follow the agreed conventions:
  - Locks use the `Lock` type.
  - Empty collections use `[]` literals.
  - Prefer primary constructors and expression-bodied members for single-line methods.
  - One-line `if` bodies omit braces.
- Styles gleaned from cleanup notes (also mentioned in `docs/driver.md`): don’t add `using System;`, `System.Threading`, etc., unless required; respect implicit usings.
- When introducing new constants for object types or intent keys, put them under `ScreepsDotNet.Driver.Abstractions.Shared.Constants` and extend `StringLiteralGuardTests` if needed.
- Runtime execution currently expects `RuntimeData["modules"]`; keep module/require plumbing aligned with `docs/SandboxOptions.md`.
- Bulk mutations should flow through `IBulkWriterFactory`; avoid reaching directly into repositories.

## Hand-offs & references

- Native pathfinder build/release: `src/native/pathfinder/AGENT.md`.
- Config/event emitter, queue service, runtime lifecycle, history/notification design: see the matching doc under `src/ScreepsDotNet.Driver/docs/`.
- When updating this project, document the change in the relevant plan doc and call it out in `docs/driver.md` so other agents see the status shift.
