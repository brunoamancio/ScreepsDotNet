# Engine Data Model Plan (E2)

Status: drafted January 13, 2026. Updated January 13, 2026 (E2 now in progress; engine providers wrap driver contracts).

## Constraints

1. **Driver-only boundary:** The engine may only talk to the driver layer. We cannot reference Mongo/Redis services, repositories, or documents directly. All world data must arrive via driver abstractions.
2. **Parity with legacy schemas:** Even though the engine avoids database knowledge, the in-memory representation must preserve every field the processor relied on in the Node engine (room objects, intents, users, power creeps, market data, etc.).
3. **Incremental adoption:** Existing driver services already expose Mongo-shaped documents (e.g., `RoomObjectDocument`). We need a migration path where the driver projects own the transformation into engine-friendly DTOs so the engine remains storage-agnostic.

## Current Driver Interfaces Supplying Data

| Interface | Key Methods | Notes |
| --- | --- | --- |
| `IRoomDataService` | `GetRoomObjectsAsync`, `GetRoomFlagsAsync`, `GetRoomTerrainAsync`, `GetRoomInfoAsync`, `GetRoomIntentsAsync`, `GetInterRoomSnapshotAsync`, `ActivateRoomsAsync`, `UpdateAccessibleRoomsListAsync`, etc. | Returns Mongo document types today; already used by driver loops. |
| `IUserDataService` | `GetActiveUsersAsync`, `GetUserAsync`, `SaveUserMemory*`, `SaveUserIntentsAsync`, `ClearGlobalIntentsAsync`, `AddRoomToUserAsync`, `RemoveRoomFromUserAsync`. | Primary source for per-user state + persistence hooks. |
| `IBulkWriterFactory` | `CreateRoomObjectsWriter`, `CreateRoomsWriter`, etc. | Engine mutations must be expressed via these writers so the driver continues to batch DB operations. |
| `IDriverEnvironmentService` / `IEnvironmentService` | `NotifyTickStarted`, `IncrementGameTime`, `UpdateRoomStatusDataAsync`, `SetRoomStatusAsync`. | Provides global tick context, Redis-backed env keys. |
| `IPathfinderService`, `IRuntimeTelemetrySink`, `IDriverLoopHooks` | Provide supporting services (pathfinding, telemetry) that consume the same data.

## Target Engine DTOs

To decouple the engine from storage documents, the driver will expose trimmed, immutable DTOs defined in a driver-facing namespace (e.g., `ScreepsDotNet.Driver.Contracts`). The engine will depend purely on these DTOs.

| DTO | Purpose | Source Fields |
| --- | --- | --- |
| `RoomSnapshot` | Aggregated per-room state (objects, users, intents, terrain, flags, metadata, power creeps) ready for simulation. | Built inside the driver using `IRoomDataService` + `IUserDataService` + `IPowerCreepService`. |
| `GlobalSnapshot` | Cross-room data (inter-room creeps, market state, shard info) consumed by global processors. | Built from `IRoomDataService.GetInterRoomSnapshotAsync`. |
| `UserState` | Minimal per-user info (CPU, bucket, GCL, money, active flag) needed for intents, notifications, bucket accounting. | Derived from `UserDocument`. |
| `RoomObjectState` | Flattened object representation with typed helpers (store, body, controller info, action logs). | Derived from `RoomObjectDocument` + computed fields (e.g., `_actionLog`). |
| `IntentEnvelope` | Normalized intent payload grouped by user/object/intent type. | Built from `RoomIntentDocument` + runner-generated intents. |

These DTOs live in the driver assembly so both the engine and driver loops share the same shapes. The driver remains responsible for translating to/from Mongo/Redis.

## Implementation Plan

1. **Introduce driver contracts (Step E2.1).** ✅ (handled on driver side; see D10 status)

2. **Add snapshot providers (Step E2.2).** ✅ Driver now exposes `IRoomSnapshotProvider` + `IInterRoomSnapshotProvider`; the engine wraps them via `RoomStateProvider`/`GlobalStateProvider` (registered through `AddEngineCore`).

3. **Wire engine consumption (Step E2.3).**
   - **In progress:** `RoomStateProvider`/`GlobalStateProvider` now pull `RoomSnapshot`/`GlobalSnapshot` from the driver, and `RoomProcessor` consumes them via the new `RoomProcessorContext` + `IRoomProcessorStep` pipeline. The current step set covers creep lifecycle, movement, combat resolution, structure decay, controller downgrade, power cooldowns, and intent event logging. Remaining work: port the rest of the legacy handlers (spawn logic, labs, notifications, etc.) so room diffs match the Node processor.

4. **Mutation path alignment (Step E2.4).**
   - **In progress:** `RoomMutationWriterFactory` produces per-room writers that stage JSON upserts/patches and flush via the driver `IRoomMutationDispatcher`. Upcoming work: integrate these writers into the processor/global systems and add helpers for event log/map view payloads.

   - Memory surfaces: `UserMemorySink` wraps `IUserDataService` so engine code can persist raw memory, segments, and inter-shard data without touching Redis directly.

5. **Tests & parity checks (Step E2.5).**
   - Driver already owns the regression fixtures for room/global snapshots; once the engine builds higher-level caches, add engine-side unit tests around `RoomStateProvider`/`GlobalStateProvider` consumers.

## Deliverables for E2 Completion

- New driver contracts + snapshot provider interfaces merged.
- Engine project updated to consume only those contracts (no `ScreepsDotNet.Storage.MongoRedis` references).
- Documentation (this file + engine AGENT) describing the layering and migration steps.
- Unit tests covering snapshot building + mutation contract serialization.

Once these pieces land, we can proceed to **E3 (Intent Gathering & Validation)** using the same contracts, confident that the engine never reaches down to the database layer.
