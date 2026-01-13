# D10 – Engine Contracts & Compatibility

## Goal
Expose stable driver-owned contracts so the upcoming ScreepsDotNet.Engine can consume world state and write back mutations strictly through the driver, without ever touching Mongo/Redis directly. Once these contracts are mature, we can swap the legacy Node engine for the new managed engine without changing storage schemas.

## Scope

1. **Room snapshots:** Provide immutable DTOs (`RoomSnapshot`, `RoomObjectState`, `RoomInfoSnapshot`, etc.) that encapsulate everything the processor needs for a single tick (objects, users, intents, terrain, flags). Builders live entirely inside the driver and translate Mongo documents to DTOs.
2. **Snapshot provider/cache:** Allow loops/engine to request snapshots via `IRoomSnapshotProvider`, reusing cached data per room/tick and invalidating when new intents land.
3. **Mutation batches:** Define driver-side abstractions that wrap `IBulkWriterFactory` so the engine can describe changes in a driver-friendly format (e.g., `RoomMutationBatch`). `IRoomMutationDispatcher` parses these JSON payloads back into Mongo-ready documents and handles history/map-view writes.
4. **Global snapshots:** Similar contract for inter-room data (`GetInterRoomSnapshotAsync`) so global processors receive typed DTOs.
5. **Compatibility shim:** Maintain an adapter layer so the legacy Node engine (or other consumers) can request the same contracts if needed during migration.

## Current Status (January 2026)

- ✅ DTOs added under `ScreepsDotNet.Driver.Contracts` (room snapshots, room objects, users, intents, mutation batches, plus the new `GlobalSnapshot`/market/power-creep/user-intent shapes).
- ✅ Snapshot builders/providers for both per-room (`IRoomSnapshotProvider`) and inter-room/global data (`IInterRoomSnapshotProvider`) are implemented with caching + regression tests.
- ✅ `RoomMutationBatch` + `IRoomMutationDispatcher` bridge engine-friendly mutation descriptions back to Mongo bulk writers; `RoomHistoryPipeline` now uses the same dispatcher path.
- ◐ Complete driver coverage: all driver loops already consume the room/global providers, but **D10 remains “In progress” until ScreepsDotNet.Engine (or another engine client) actually runs on these contracts**. Once the engine consumes them end-to-end, mark D10 complete in `docs/driver.md`.

## Next Steps

1. Wire ScreepsDotNet.Engine directly into `IRoomSnapshotProvider`, `IInterRoomSnapshotProvider`, and `IRoomMutationDispatcher` so the managed processor runs entirely on these contracts. **When this is done, flip D10 to “Completed” in `docs/driver.md`.**
2. Expand contract docs with concrete engine examples (sample provider usage, mutation batch authoring) and keep the regression fixtures under version control for future agents.
3. After the engine is live on these contracts, move remaining parity tracking to the engine project (E milestones).
