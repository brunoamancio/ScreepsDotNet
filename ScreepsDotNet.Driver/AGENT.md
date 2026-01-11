# Driver Rewrite Plan

## Purpose
Track the strategy and status for porting the legacy Screeps Node.js driver into the new .NET solution. This document is a shared hand-off point for every agent touching the driver work.

## Current Snapshot (January 11, 2026)
- `ScreepsDotNet.Driver` class library scaffold exists and is referenced from `ScreepsDotNet.slnx`.
- No interfaces or code yet beyond the empty project file.
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
| D3 | Choose and prototype the JavaScript sandbox (ClearScript + V8). Implement module/require plumbing, runtime bootstrap, memory/CPU quotas, and host bridges. | `docs/SandboxOptions.md` | Plan completed (implementation pending) ◐ |
| D4 | Implement queue + scheduler services (room/user queues, add/fetch/mark-done/reset, rate limiting). Ensure graceful shutdown semantics. | `docs/QueueAndScheduler.md` | Plan completed (implementation pending) ◐ |
| D5 | Port bulk writer abstractions (`BulkObjects`, `BulkUsers`, `BulkFlags`, `BulkTransactions`, etc.). | `docs/BulkWriters.md` | Plan completed (implementation pending) ◐ |
| D6 | Implement pathfinder integration (reuse native algorithm or wrap existing Node addon through interop). Seed terrain data cache and expose `driver.pathFinder`. | `docs/Pathfinder.md` | Plan completed (implementation pending) ◐ |
| D7 | Wire global config and events (`config.engine.emit`, tick scheduling knobs, custom object prototypes). | `docs/ConfigAndEvents.md` | Plan completed (implementation pending) ◐ |
| D8 | Provide runtime lifecycle endpoints (make runtime, send console messages, save memory/segments/intents, notify errors). | `docs/RuntimeLifecycle.md` | Plan completed (implementation pending) ◐ |
| D9 | Build history/map view writers and notification helpers (`activateRoom`, `updateAccessibleRoomsList`, `notifyRoomsDone`, etc.). | `docs/HistoryAndNotifications.md` | Plan completed (implementation pending) ◐ |
| D10 | Integration phase: run the legacy Node engine against the new driver via a compatibility shim to validate parity before attempting the .NET engine rewrite. | _TBD_ | Pending ☐ |

_Progress Legend:_ ☐ not started, ◐ in progress, ✔ complete. Update this table as work advances.

## Next Up
- D1: interface/spec capture. Once done, every subsequent task can hang off a shared contract.

## Notes for Future Agents
- Keep cross-cutting settings in `Directory.Build.props`; avoid duplicating target framework info inside this project.
- Record meaningful decisions (e.g., sandbox tech, storage schema tweaks) in this file so new agents don’t repeat discovery work.
- Prefer relying on implicit/usings inherited from `Directory.Build.props`. Only add explicit `using` directives when a file needs a namespace that isn’t already imported; redundant `System.*` usings make future cleanups harder.
