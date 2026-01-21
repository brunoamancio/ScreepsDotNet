# D10 – Engine Contracts & Compatibility

## Goal
Expose stable driver-owned contracts so the upcoming ScreepsDotNet.Engine can consume world state and write back mutations strictly through the driver, without ever touching Mongo/Redis directly. Once these contracts are mature, we can swap the legacy Node engine for the new managed engine without changing storage schemas.

## Scope

1. **Room snapshots:** Provide immutable DTOs (`RoomSnapshot`, `RoomObjectSnapshot`, `RoomInfoSnapshot`, etc.) that encapsulate everything the processor needs for a single tick (objects, users, intents, terrain, flags). Builders live entirely inside the driver and translate Mongo documents to DTOs.
2. **Snapshot provider/cache:** Allow loops/engine to request snapshots via `IRoomSnapshotProvider`, reusing cached data per room/tick and invalidating when new intents land.
3. **Mutation batches:** Define driver-side abstractions that wrap `IBulkWriterFactory` so the engine can describe changes in a driver-friendly format (e.g., `RoomMutationBatch`). `IRoomMutationDispatcher` parses these JSON payloads back into Mongo-ready documents and handles history/map-view writes.
4. **Global snapshots:** Similar contract for inter-room data (`GetInterRoomSnapshotAsync`) so global processors receive typed DTOs.
5. **Compatibility shim:** Maintain an adapter layer so the legacy Node engine (or other consumers) can request the same contracts if needed during migration.

## Current Status (January 18, 2026)

### ✅ Implementation Complete (D10 code finished)

**Driver Abstractions:**
- ✅ DTOs added under `ScreepsDotNet.Driver.Contracts` (room snapshots, room objects, users, intents, mutation batches, plus the new `GlobalSnapshot`/market/power-creep/user-intent shapes).
- ✅ Snapshot builders/providers for both per-room (`IRoomSnapshotProvider`) and inter-room/global data (`IInterRoomSnapshotProvider`) are implemented with caching + regression tests.
- ✅ `RoomMutationBatch` + `IRoomMutationDispatcher` bridge engine-friendly mutation descriptions back to Mongo bulk writers; `RoomHistoryPipeline` now uses the same dispatcher path.
- ✅ `IEngineHost` contract defined and wired into `MainLoopGlobalProcessor` (optional).

**Engine Integration:**
- ✅ `ScreepsDotNet.Engine` consumes all D10 contracts correctly:
  - `RoomStateProvider` wraps `IRoomSnapshotProvider`
  - `GlobalStateProvider` wraps `IInterRoomSnapshotProvider`
  - `RoomMutationWriterFactory` wraps `IRoomMutationDispatcher`
  - `UserMemorySink` wraps `IUserDataService`
- ✅ `RoomProcessor` and `EngineGlobalProcessor` implemented using all 4 abstractions.
- ✅ `EngineHost` implements `IEngineHost` and orchestrates global processing.
- ✅ DI wiring complete via `AddEngineCore()` in `ScreepsDotNet.Engine/ServiceCollectionExtensions.cs`.

**Tests:**
- ✅ Unit tests for `RoomSnapshotProvider`, `RoomMutationDispatcher`, `InterRoomSnapshotProvider`.
- ✅ Engine processor step tests (see `ScreepsDotNet.Engine.Tests/Processors/`).

### ✅ Production Deployment Complete (E6 Integration)

**D10 + E6 Status: Complete**

1. **Engine is now required** - `MainLoopGlobalProcessor` and `ProcessorLoopWorker` require `IEngineHost` (not optional)
2. **Legacy code removed** - All fallback code paths deleted (385 lines removed from ProcessorLoopWorker)
3. **Active loop orchestration** - Driver loops exclusively use managed Engine for all processing
4. **All tests passing** - 754/754 tests pass (428 Engine + 70 Driver + 54 CLI + 202 HTTP)

**What Changed in E6:**
- `ProcessorLoopWorker`: `IEngineHost? engineHost = null` → `IEngineHost engineHost` (required)
- `MainLoopGlobalProcessor`: `IEngineHost? engineHost = null` → `IEngineHost engineHost` (required)
- Removed legacy BsonDocument-based intent processing (ProcessorLoopWorker: 455→70 lines)
- Removed legacy transfer processor fallback (MainLoopGlobalProcessor: 55→38 lines)

**Conclusion:** D10 contracts are in production use. The managed .NET Engine is the **only** processing path.

## Next Steps

1. **E6 (Engine Loop Orchestration):** Create a runnable process (CLI command or hosted service) that calls `AddDriverCore()` + `AddEngineCore()` and starts tick execution.
2. **End-to-end integration test:** Add test that exercises full tick: load snapshot → run Engine → apply mutations → verify DB state.
3. **Mark D10 complete in `docs/driver/roadmap.md`** once E6 has Engine running end-to-end (no longer just "wired but unused").
4. Expand contract docs with concrete engine examples (sample provider usage, mutation batch authoring).
5. After the engine is live, move remaining parity tracking to the engine project (E7 milestones).
