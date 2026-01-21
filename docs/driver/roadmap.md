# Driver Roadmap (D1-D10)

**Last Updated:** January 21, 2026 (All Milestones Complete)

This document tracks the Driver subsystem roadmap and implementation status. The driver provides queue infrastructure, JavaScript runtime coordination (ClearScript/V8), bulk writers, pathfinding, and loop orchestration for executing user code and applying game simulation logic.

---

## Goals

- Recreate the `driver.*` surface that the legacy engine expects (queues, bulk writers, runtime lifecycle, config emitter, notifications/history, pathfinding).
- Provide a managed JavaScript runtime (ClearScript + V8) with module/require plumbing, console/memory/intents bridges, and compatibility with today's `isolated-vm` behavior.
- Keep Mongo/Redis schemas untouched so the Node engine (and later the .NET engine) can talk to the new driver without migrations.
- Preserve modular boundaries so queue service, runtime host, pathfinder, and history subsystems can be tested in isolation.

---

## Milestone Overview

| ID | Status | Title | Exit Criteria | Design Doc |
|----|--------|-------|---------------|------------|
| D1 | ✅ | Driver API Inventory | `IScreepsDriver`, `IDriverLoopHooks` surface defined | [d1-driver-api.md](d1-driver-api.md) |
| D2 | ✅ | Storage Adapters | Mongo/Redis infrastructure shared with backend | [d2-storage-adapters.md](d2-storage-adapters.md) |
| D3 | ✅ | Sandbox | ClearScript/V8 runtime, module loader, CPU/heap guards | [d3-sandbox.md](d3-sandbox.md) |
| D4 | ✅ | Queues & Scheduler | Redis queues, worker orchestration, telemetry stages | [d4-queue-scheduler.md](d4-queue-scheduler.md) |
| D5 | ✅ | Bulk Writers | `IBulkWriterFactory` for batched Mongo mutations | [d5-bulk-writers.md](d5-bulk-writers.md) |
| D6 | ✅ | Pathfinder | Native solver integration, P/Invoke bindings | [d6-pathfinder.md](d6-pathfinder.md) |
| D7 | ✅ | Config/Events | Config emitter, environment service, tick knobs | [d7-config-events.md](d7-config-events.md) |
| D8 | ✅ | Runtime Lifecycle | Runtime coordinator, pooling, watchdog, throttling | [d8-runtime-lifecycle.md](d8-runtime-lifecycle.md) |
| D9 | ✅ | History & Notifications | Room history, notification delivery, console output | [d9-history-notifications.md](d9-history-notifications.md) |
| D10 | ✅ | Engine Contracts | Snapshot providers, mutation dispatcher for Engine | [d10-engine-contracts.md](d10-engine-contracts.md) |

---

## Current Status (January 2026)

**All driver milestones (D1-D10) complete ✅**

- Queue infrastructure, scheduler helpers, bulk writers, room/user services, and notification/history services run inside `ScreepsDotNet.Driver`.
- ClearScript-based runtime coordinator executes user code, captures console/memory/intents, and writes through the new services via `RunnerLoopWorker`/`ProcessorLoopWorker`.
- Native pathfinder binaries now live under `src/native/pathfinder`; `dotnet build` downloads the correct RID package (hash-verified) and `PathfinderService` now always uses the native solver (the managed A* fallback has been removed, so missing binaries fail fast).
- **Managed .NET Engine is production-ready**: E6 (Engine Loop Orchestration) complete - driver loops exclusively use the managed Engine for all processing. No legacy Node.js fallbacks remain. Track engine progress in [docs/engine/roadmap.md](../engine/roadmap.md) (E1-E6 complete, E7-E9 pending).

---

## Milestone Details

### D1: Driver API Inventory ✅

**Status:** Complete

**Summary:**
- Document every `driver.*` consumer in the Node engine
- Expose typed interfaces (`IScreepsDriver`, `IDriverLoopHooks`, etc.)
- Interface surface defined; remaining parity tweaks tracked under D10

**Details:** See [d1-driver-api.md](d1-driver-api.md)

---

### D2: Storage Adapters ✅

**Status:** Complete

**Summary:**
- Map Mongo/Redis usage (bulk writers, env keys, queues, history) onto reusable infrastructure
- Shared infrastructure powers env, queues, history, and bulk writers
- Used by both backend and driver

**Details:** See [d2-storage-adapters.md](d2-storage-adapters.md)

---

### D3: Sandbox ✅

**Status:** Complete

**Summary:**
- ClearScript + V8 JavaScript host
- Module loader with require() plumbing
- CPU/memory guards and runtime bootstrap
- Sandbox pooling and bundle caching

**Details:** See [d3-sandbox.md](d3-sandbox.md)

---

### D4: Queues & Scheduler ✅

**Status:** Complete

**Summary:**
- Redis-backed queue service for main/runner/processor loops
- Worker scheduler with telemetry stages
- Queue backlogs emit stage-tagged telemetry via `IDriverLoopHooks`
- Observability integration (see [src/docs/runtime-telemetry.md](../../src/docs/runtime-telemetry.md))

**Details:** See [d4-queue-scheduler.md](d4-queue-scheduler.md)

---

### D5: Bulk Writers ✅

**Status:** Complete

**Summary:**
- Port `BulkObjects`, `BulkUsers`, etc. from Node.js
- Processor/global stages mutate Mongo documents via `IBulkWriterFactory`
- Batched writes for performance

**Details:** See [d5-bulk-writers.md](d5-bulk-writers.md)

---

### D6: Pathfinder ✅

**Status:** Complete

**Summary:**
- Native solver wrapper + managed bindings
- Terrain cache loading
- Managed fallback removed (native-only)
- Regression fixtures guard parity with Node.js

**Details:** See [d6-pathfinder.md](d6-pathfinder.md)

**Cross-reference:** [src/native/pathfinder/CLAUDE.md](../../src/native/pathfinder/CLAUDE.md) for build/deployment

---

### D7: Config/Events ✅

**Status:** Complete

**Summary:**
- Recreate `config.emit(...)` from Node.js
- Environment service (tick knobs, game time)
- Config emitter mirrors legacy behavior

**Details:** See [d7-config-events.md](d7-config-events.md)

---

### D8: Runtime Lifecycle ✅

**Status:** Complete

**Summary:**
- Runtime hooks (make runtime, console, memory, intent persistence)
- Sandbox pooling for performance
- Watchdog and scheduler throttling
- Telemetry fan-out to observability stack

**Details:** See [d8-runtime-lifecycle.md](d8-runtime-lifecycle.md)

**Cross-reference:** [src/docs/runtime-telemetry.md](../../src/docs/runtime-telemetry.md) for payload contract

---

### D9: History & Notifications ✅

**Status:** Complete

**Summary:**
- Save room history/map view diffs
- Deliver notifications/console output via hooks
- History pipeline writes map-view/event logs from Mongo
- Notification throttling covers console/watchdog/roomsDone

**Details:** See [d9-history-notifications.md](d9-history-notifications.md)

---

### D10: Engine Contracts ✅

**Status:** Complete (D10+E6 Integration)

**Summary:**
- Expose driver-owned DTOs/providers for Engine
- Room + inter-room snapshot providers
- Mutation dispatcher for Engine writes
- Managed .NET Engine is now required in all driver loops
- No legacy Node.js fallbacks remain

**Details:** See [d10-engine-contracts.md](d10-engine-contracts.md)

---

## Hand-off Reference

- **AI context:** [src/ScreepsDotNet.Driver/CLAUDE.md](../../src/ScreepsDotNet.Driver/CLAUDE.md) - Code patterns, common tasks, D1-D10 status
- **Native pathfinder:** [src/native/pathfinder/CLAUDE.md](../../src/native/pathfinder/CLAUDE.md) - Build/deployment details
- **Engine roadmap:** [docs/engine/roadmap.md](../engine/roadmap.md) - E1-E9 milestone tracking

---

## Next Steps

All driver milestones complete. The driver is production-ready and serves as the foundation for:
- ✅ Managed .NET Engine (E1-E6 complete)
- 📋 Engine parity validation (E7)
- 📋 Observability & tooling (E8)
- 📋 NPC AI logic (E9)

When updating any subsystem, revise both the relevant plan doc and the AGENT snapshot so future agents can see what changed without re-reading the entire codebase.
