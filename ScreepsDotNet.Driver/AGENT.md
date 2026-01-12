# Driver Rewrite Plan

## Purpose
Track the strategy and status for porting the legacy Screeps Node.js driver into the new .NET solution. This document is a shared hand-off point for every agent touching the driver work.

## Current Snapshot (January 11, 2026)
- `ScreepsDotNet.Driver` now ships queue infrastructure, scheduler helpers, bulk writers, room/user data services, notification/history services, and a ClearScript-based runtime executor (expects `RuntimeData.script`, captures console/memory/intents).
- Runner/Main/Processor loops are wired: the main loop fills Redis queues, `RunnerLoopWorker` bundles code from `users.code`, executes it via ClearScript, and writes intents/memory; `ProcessorLoopWorker` drains room work, snapshots history, and clears intents so we can exercise a simplified end-to-end tick.
- Remaining driver entry points (pathfinder and the legacy-engine compatibility shim) are still pending implementation.
- Engine still consumes the legacy Node driver (via `@screeps/core`).

## Guiding Objectives
1. Recreate the full driver contract expected by the engine (`driver.*` methods, config emitter, bulk writers, queues, runtime bootstrap, pathfinding, notifications, history/map storage, etc.).
2. Provide a sandboxed JavaScript runtime (ClearScript + V8) that can execute user scripts with the same semantics and safety limits as `isolated-vm` today.
3. Maintain storage compatibility with the current Mongo/Redis schema so the Node engine (and later the .NET engine) can swap in without data migrations.
4. Keep the implementation modular so we can unit/integration test each subsystem (queue service, runtime host, pathfinder, history uploads) independently.

## Work Plan & Status
| Step | Description | Plan Doc | Status |
|------|-------------|----------|--------|
| D1 | Enumerate the complete driver API surface by scanning `ScreepsNodeJs/engine` for `driver.*` usage and documenting the method signatures + behavior. Turn this into a C# interface (e.g., `IScreepsDriver`). | `docs/DriverApi.md` | Plan completed (implementation pending) ◐ |
| D2 | Design storage adapters for Mongo/Redis that cover bulk writes, env keys, queues, pub/sub, history storage, map view persistence. Leverage existing `ScreepsDotNet.Storage.MongoRedis` types where possible. | `docs/StorageAdapters.md` | Plan completed (implementation pending) ◐ |
| D3 | Choose and prototype the JavaScript sandbox (ClearScript + V8). Implement module/require plumbing, runtime bootstrap, memory/CPU quotas, and host bridges. | `docs/SandboxOptions.md` | ClearScript V8 runtime with CommonJS loader + sandbox prototype; pathfinder/host bridges remain ◐ |
| D4 | Implement queue + scheduler services (room/user queues, add/fetch/mark-done/reset, rate limiting). Ensure graceful shutdown semantics. | `docs/QueueAndScheduler.md` | Queues + loop workers wired (runner/processor use Redis queues); worker scheduler logging/backoff still TODO ◐ |
| D5 | Port bulk writer abstractions (`BulkObjects`, `BulkUsers`, `BulkFlags`, `BulkTransactions`, etc.). | `docs/BulkWriters.md` | Implementation complete (services/Bulk) ✔ |
| D6 | Implement pathfinder integration (reuse native algorithm or wrap existing Node addon through interop). Seed terrain data cache and expose `driver.pathFinder`. | `docs/Pathfinder.md` | Plan completed (implementation pending) ◐ |
| D7 | Wire global config and events (`config.engine.emit`, tick scheduling knobs, custom object prototypes). | `docs/ConfigAndEvents.md` | Implementation complete (Redis-backed config + Node-style emit/on) ✔ |
| D8 | Provide runtime lifecycle endpoints (make runtime, send console messages, save memory/segments/intents, notify errors). | `docs/RuntimeLifecycle.md` | RuntimeService invoked via RunnerLoopWorker; JS prelude now supports `registerIntent` + `notify` to drive intents/notifications ◐ |
| D9 | Build history/map view writers and notification helpers (`activateRoom`, `updateAccessibleRoomsList`, `notifyRoomsDone`, etc.). | `docs/HistoryAndNotifications.md` | History + notification services wired through `DriverLoopHooks` with unit tests for diff/throttle logic ◐ |
| D10 | Integration phase: run the legacy Node engine against the new driver via a compatibility shim to validate parity before attempting the .NET engine rewrite. | _TBD_ | Pending ☐ |

_Progress Legend:_ ☐ not started, ◐ in progress, ✔ complete. Update this table as work advances.

## Next Up
- Exercise `DriverLoopHooks` once processor/main/runner scaffolding exists so tick stages call into the new runtime/notification/history surface instead of touching services directly.
- Expand the runtime pipeline: load real module bundles, persist memory segments/inter-shard payloads, and integrate with the upcoming runner coordinator.
- Pathfinder implementation (D6) and the legacy-engine compatibility shim (D10) remain to be tackled after the runtime + main-loop wiring solidifies.

## Notes for Future Agents
- Keep cross-cutting settings in `Directory.Build.props`; avoid duplicating target framework info inside this project.
- Record meaningful decisions (e.g., sandbox tech, storage schema tweaks) in this file so new agents don’t repeat discovery work.
- Prefer relying on implicit/usings inherited from `Directory.Build.props`. Only add explicit `using` directives when a file needs a namespace that isn’t already imported; redundant `System.*` usings make future cleanups harder.
- Match the existing style conventions: declare locks with the `Lock` type, use collection expressions (`[]`) for empty initializers, favor primary constructors when possible, convert one-line methods to expression-bodied members, and drop braces for single-line `if` statements.
- Bulk writer infrastructure (`Services/Bulk`) powers the new `RoomDataService`, `UserDataService`, and downstream services; prefer going through `IBulkWriterFactory` for collection mutations.
- History snapshots live under `Services/History` (Redis-backed hashes + `HistoryDiffBuilder`) and raise `RoomHistorySaved` via `IDriverConfig`. Notification fan-out (`Services/Notifications` + `NotificationThrottler`) publishes console/rooms-done events and manages `users.notifications`.
- Runtime execution now loads player modules directly (`RuntimeExecutionContext.RuntimeData["modules"]`), builds them via a CommonJS-style loader in `V8RuntimeSandbox`, and falls back to the legacy bundled script only if modules are missing. Module caching beyond per-tick compilation still needs work.
- `registerIntent` in the ClearScript prelude inspects `payload.room` to route intents to rooms; use `notify("text", intervalMinutes)` inside user code to queue notifications (they enter `UserIntentWritePayload.Notifications` and flow through `UserDataService`).
- Processor loop currently snapshots room objects + clears intents only; real intent application, map view, and stats flushing still need to be ported.
- Processor loop now stores per-object `lastIntent*` metadata via `IBulkWriterFactory`, applies manual deltas (`damage`, `set`, `patch`, `remove`) plus a first wave of typed handlers (creep/tower attacks & heals, link transfers, lab reactions) to mutate `rooms.objects`, writes intent summaries to map view + event logs, and triggers aggregated intent notifications via `IDriverLoopHooks`. Full parity with the Node processor (movement, power actions, controller logic, etc.) remains TODO.
- Object types / intent keys moved to `Constants/{RoomObjectTypes,IntentKeys}` (with matching enum helpers), so future handlers avoid hand-typed strings. Update new code to reuse these constants.
- `StringLiteralGuardTests` enforces that no other .cs files introduce raw object-type or intent-key strings; update the exclusions if you add new constant files.
- Outstanding TODOs: connect queue scheduler error handling to the shared logging infrastructure once it exists.
