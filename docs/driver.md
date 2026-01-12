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
- Native pathfinder sources were extracted under `src/native/pathfinder`; managed bindings download per-RID binaries automatically during `dotnet build` (hash-verified) but the final `PathfinderService` swap is still in progress.
- Compatibility shim (D10) and parity validation against the Node engine remain after runtime + processor wiring solidify.

## Workstreams & Plan Docs

| Step | Summary | Design Notes | Status |
| --- | --- | --- | --- |
| **D1 – Driver API inventory** | Document every `driver.*` consumer in the Node engine and expose typed interfaces (`IScreepsDriver`, `IDriverLoopHooks`, etc.). | [src/ScreepsDotNet.Driver/docs/DriverApi.md](../src/ScreepsDotNet.Driver/docs/DriverApi.md) | Plan complete (implementation ongoing) |
| **D2 – Storage adapters** | Map Mongo/Redis usage (bulk writers, env keys, queues, history) onto reusable infrastructure shared with the backend. | [src/ScreepsDotNet.Driver/docs/StorageAdapters.md](../src/ScreepsDotNet.Driver/docs/StorageAdapters.md) | Plan complete |
| **D3 – Sandbox** | Decide on JS host (ClearScript + V8), implement module loader, CPU/memory guards, and runtime bootstrap. | [src/ScreepsDotNet.Driver/docs/SandboxOptions.md](../src/ScreepsDotNet.Driver/docs/SandboxOptions.md) | ClearScript path chosen; module plumbing in progress |
| **D4 – Queues & scheduler** | Provide Redis-backed queue service + worker scheduler for main/runner/processor loops. | [src/ScreepsDotNet.Driver/docs/QueueAndScheduler.md](../src/ScreepsDotNet.Driver/docs/QueueAndScheduler.md) | Queues running; scheduler logging/backoff TODO |
| **D5 – Bulk writers** | Port `BulkObjects`, `BulkUsers`, etc., so processor/global stages can mutate Mongo documents. | [src/ScreepsDotNet.Driver/docs/BulkWriters.md](../src/ScreepsDotNet.Driver/docs/BulkWriters.md) | Complete |
| **D6 – Pathfinder** | Ship native solver wrapper + managed bindings with terrain cache loading. | [src/ScreepsDotNet.Driver/docs/Pathfinder.md](../src/ScreepsDotNet.Driver/docs/Pathfinder.md) | Native sources extracted; managed swap WIP |
| **D7 – Config/events** | Recreate `config.emit(...)`, tick knobs, and environment service. | [src/ScreepsDotNet.Driver/docs/ConfigAndEvents.md](../src/ScreepsDotNet.Driver/docs/ConfigAndEvents.md) | Complete |
| **D8 – Runtime lifecycle** | Provide runtime hooks (make runtime, console, memory, intent persistence). | [src/ScreepsDotNet.Driver/docs/RuntimeLifecycle.md](../src/ScreepsDotNet.Driver/docs/RuntimeLifecycle.md) | RawMemory/segments/inter-shard persistence, telemetry events, sandbox pooling & bundle caching done; watchdog/coordinator polish pending |
| **D9 – History & notifications** | Save room history/map view diffs and deliver notifications/console output via hooks. | [src/ScreepsDotNet.Driver/docs/HistoryAndNotifications.md](../src/ScreepsDotNet.Driver/docs/HistoryAndNotifications.md) | In progress |
| **D10 – Legacy shim** | Run the Node engine against the .NET driver for parity validation. | _TBD_ | Not started |

Status legend: ✔ done, ◐ in progress, ☐ not started. See the driver AGENT file for the most up-to-date ticks.

## Hand-off Reference

- Day-to-day instructions and style expectations live in [src/ScreepsDotNet.Driver/AGENT.md](../src/ScreepsDotNet.Driver/AGENT.md). Link to this guide from status updates so other agents know where plan docs live.
- Native pathfinder build/deployment details live in [src/native/pathfinder/AGENT.md](../src/native/pathfinder/AGENT.md). The driver build automatically downloads matching binaries unless `NativePathfinderSkipDownload=true` is set.

When updating any subsystem, revise both the relevant plan doc and the AGENT snapshot so future agents can see what changed without re-reading the entire codebase.
