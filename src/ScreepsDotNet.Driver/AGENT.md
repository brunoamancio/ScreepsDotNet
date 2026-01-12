# Driver AGENT

Use this file for day-to-day coordination inside `ScreepsDotNet.Driver`. The full roadmap, plan docs, and milestone status live in `docs/driver.md` plus the per-topic files under `src/ScreepsDotNet.Driver/docs/`.

## Snapshot (January 2026)

- Queue infrastructure, worker scheduler skeleton, bulk writers, room/user services, notification/history services, and the ClearScript runtime coordinator are merged; runner/main/processor loops can execute a simplified tick.
- Runtime sandbox now exposes Node-style `RawMemory`/segment/inter-shard APIs and only persists data when user code mutates it; `V8RuntimeSandboxTests` cover the new behavior.
- Sandbox pooling + bundle caching keeps V8 isolates hot; runtime telemetry now flows through a dedicated monitor (`RuntimeTelemetryMonitor`) hooked into `IDriverConfig.RuntimeTelemetry`, and the new `RuntimeCoordinator` owns context hydration/persistence so `RunnerLoopWorker` just schedules users.
- Runtime telemetry (CPU used, timeout/script error flags, heap usage) now flows through `IDriverLoopHooks.PublishRuntimeTelemetryAsync`, so schedulers/loggers can react as soon as the logging pipeline is wired.
- Native pathfinder work is split into `src/native/pathfinder` (see that AGENT for build/CI info); the managed service still needs to switch over once the new binaries are wired.
- Compatibility shim (D10) and deeper processor logic (intent application, map view, stats) remain after runtime/pathfinder stabilization.

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
