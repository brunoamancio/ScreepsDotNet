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

- ✅ DTOs added under `ScreepsDotNet.Driver.Contracts`.
- ✅ `IRoomSnapshotBuilder` + caching `IRoomSnapshotProvider` implemented and wired into DI.
- ✅ `RoomMutationBatch` contract and `IRoomMutationDispatcher` now bridge mutation requests back to Mongo bulk writers.
- ◐ Processor/runtime loops still use legacy data paths; wiring them to the snapshot/mutation services is the next milestone.

## Next Steps

1. Add mutation batch contracts and adapters that wrap `IBulkWriterFactory`.
2. Port the driver processor loops to consume `IRoomSnapshotProvider` so new engine code can plug in without bespoke wiring.
3. Document how to request snapshots/mutations in this file once the engine integration goes live.
