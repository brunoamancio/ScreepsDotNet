# Driver Rewrite Overview

This document summarizes the ongoing effort to port the legacy Screeps Node.js driver into the ScreepsDotNet solution. Use it to understand the scope, current status, and where to look for deeper design notes before touching driver code.

## Goals

- Recreate the `driver.*` surface that the legacy engine expects (queues, bulk writers, runtime lifecycle, config emitter, notifications/history, pathfinding).
- Provide a managed JavaScript runtime (ClearScript + V8) with module/require plumbing, console/memory/intents bridges, and compatibility with today’s `isolated-vm` behavior.
- Keep Mongo/Redis schemas untouched so the Node engine (and later the .NET engine) can talk to the new driver without migrations.
- Preserve modular boundaries so queue service, runtime host, pathfinder, and history subsystems can be tested in isolation.

## Snapshot (January 2026)

- Queue infrastructure, scheduler helpers, bulk writers, room/user services, and notification/history services run inside `ScreepsDotNet.Driver`.
- ClearScript-based runtime coordinator executes user code, captures console/memory/intents, and writes through the new services via `RunnerLoopWorker`/`ProcessorLoopWorker`.
- Native pathfinder binaries now live under `src/native/pathfinder`; `dotnet build` downloads the correct RID package (hash-verified) and `PathfinderService` now always uses the native solver (the managed A* fallback has been removed, so missing binaries fail fast).
- Next milestone is rewriting the engine itself on .NET; the driver is feature-complete and ready to plug into the new runtime stack without relying on a Node compatibility shim. Track that effort in [src/ScreepsDotNet.Engine/AGENT.md](../src/ScreepsDotNet.Engine/AGENT.md).

## Workstreams & Plan Docs

| Step | Summary | Design Notes | Status |
| --- | --- | --- | --- |
| **D1 – Driver API inventory** | Document every `driver.*` consumer in the Node engine and expose typed interfaces (`IScreepsDriver`, `IDriverLoopHooks`, etc.). | [src/ScreepsDotNet.Driver/docs/DriverApi.md](../src/ScreepsDotNet.Driver/docs/DriverApi.md) | **Completed** – Interface surface defined; any remaining parity tweaks are tracked under D10. |
| **D2 – Storage adapters** | Map Mongo/Redis usage (bulk writers, env keys, queues, history) onto reusable infrastructure shared with the backend. | [src/ScreepsDotNet.Driver/docs/StorageAdapters.md](../src/ScreepsDotNet.Driver/docs/StorageAdapters.md) | **Completed** – Shared Mongo/Redis infrastructure powers env, queues, history, and bulk writers. |
| **D3 – Sandbox** | Decide on JS host (ClearScript + V8), implement module loader, CPU/memory guards, and runtime bootstrap. | [src/ScreepsDotNet.Driver/docs/SandboxOptions.md](../src/ScreepsDotNet.Driver/docs/SandboxOptions.md) | **Completed** – ClearScript runtime, module loader, CPU/heap guards, sandbox pooling, and bundle caching are live; ongoing tweaks stay within D8. |
| **D4 – Queues & scheduler** | Provide Redis-backed queue service + worker scheduler for main/runner/processor loops. | [src/ScreepsDotNet.Driver/docs/QueueAndScheduler.md](../src/ScreepsDotNet.Driver/docs/QueueAndScheduler.md) | **Completed** – Queue backlogs emit stage-tagged telemetry (`idle`, `dequeue`, `drain*`, `throttleDelay`) via `IDriverLoopHooks`, `WorkerScheduler` outputs `Stage=scheduler` crash events, and `ObservabilityTelemetryListener` + `ObservabilityOptions` route everything into the exporter described in [src/docs/runtime-telemetry.md](../src/docs/runtime-telemetry.md). |
| **D5 – Bulk writers** | Port `BulkObjects`, `BulkUsers`, etc., so processor/global stages can mutate Mongo documents. | [src/ScreepsDotNet.Driver/docs/BulkWriters.md](../src/ScreepsDotNet.Driver/docs/BulkWriters.md) | **Completed** – Processor/global stages now rely entirely on `IBulkWriterFactory`. |
| **D6 – Pathfinder** | Ship native solver wrapper + managed bindings with terrain cache loading. | [src/ScreepsDotNet.Driver/docs/Pathfinder.md](../src/ScreepsDotNet.Driver/docs/Pathfinder.md) | **Completed** – Native solver/CI pipeline finished; managed fallback removed, regression fixtures guard parity, docs cover rebuild/download flow. |
| **D7 – Config/events** | Recreate `config.emit(...)`, tick knobs, and environment service. | [src/ScreepsDotNet.Driver/docs/ConfigAndEvents.md](../src/ScreepsDotNet.Driver/docs/ConfigAndEvents.md) | **Completed** – Config emitter + environment service mirror legacy behavior and feed every loop. |
| **D8 – Runtime lifecycle** | Provide runtime hooks (make runtime, console, memory, intent persistence). | [src/ScreepsDotNet.Driver/docs/RuntimeLifecycle.md](../src/ScreepsDotNet.Driver/docs/RuntimeLifecycle.md) | **Completed** – Runtime coordinator, sandbox pooling, telemetry fan-out, watchdog/scheduler throttling, and the observability exporter toggle/documentation are done (payload contract in [src/docs/runtime-telemetry.md](../src/docs/runtime-telemetry.md)). |
| **D9 – History & notifications** | Save room history/map view diffs and deliver notifications/console output via hooks. | [src/ScreepsDotNet.Driver/docs/HistoryAndNotifications.md](../src/ScreepsDotNet.Driver/docs/HistoryAndNotifications.md) | **Completed** – History pipeline writes map-view/event logs from Mongo, notification throttling covers console/watchdog/roomsDone, docs/tests updated. |
| **D10 – Engine contracts** | Expose driver-owned DTOs/providers (room + inter-room snapshots, mutation batches) so the new .NET engine consumes data via the driver only. | [src/ScreepsDotNet.Driver/docs/EngineContracts.md](../src/ScreepsDotNet.Driver/docs/EngineContracts.md) | **In progress** – Room + global snapshot providers, mutation dispatcher wiring, and regression tests are merged; remaining work is plugging ScreepsDotNet.Engine into these contracts and documenting usage patterns. |

Status legend: ✔ done, ◐ in progress, ☐ not started. See the driver AGENT file for the most up-to-date ticks.

## Hand-off Reference

- Day-to-day instructions and style expectations live in [src/ScreepsDotNet.Driver/AGENT.md](../src/ScreepsDotNet.Driver/AGENT.md). Link to this guide from status updates so other agents know where plan docs live.
- Native pathfinder build/deployment details live in [src/native/pathfinder/AGENT.md](../src/native/pathfinder/AGENT.md). The driver build automatically downloads matching binaries unless `NativePathfinderSkipDownload=true` is set.

When updating any subsystem, revise both the relevant plan doc and the AGENT snapshot so future agents can see what changed without re-reading the entire codebase.
