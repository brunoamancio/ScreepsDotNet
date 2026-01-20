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
| `RoomObjectSnapshot` | Flattened object representation with typed helpers (store, body, controller info, action logs). | Derived from `RoomObjectDocument` + computed fields (e.g., `_actionLog`). |
| `IntentEnvelope` | Normalized intent payload grouped by user/object/intent type. | Built from `RoomIntentDocument` + runner-generated intents. |

### Action Log DTOs

Driver-side mapping now surfaces `_actionLog` through typed contracts:

- `RoomObjectActionLogSnapshot` hangs off each `RoomObjectSnapshot`, exposing structured entries such as `Die` (time) and `Healed` (spawn coordinates). Engine steps can inspect prior tick logs without touching BSON.
- `RoomObjectActionLogPatch` mirrors the same structure for mutations. When engine code sets `Healed` or `Die`, the driver writes the correct nested documents back to Mongo via `RoomContractMapper`.

Always use these DTOs instead of ad-hoc dictionaries so action-log parity remains centralized and storage-agnostic.

These DTOs live in the driver assembly so both the engine and driver loops share the same shapes. The driver remains responsible for translating to/from Mongo/Redis.

### Harvest Metadata Surfaces

`RoomObjectSnapshot` now exposes the legacy harvest counters directly so the engine never needs to read raw BSON/JToken blobs:

- **Sources:** `Energy` (current capacity) and `InvaderHarvested` (NPC depletion tracker).
- **Minerals:** `MineralAmount` (remaining yield) alongside the existing `MineralType`.
- **Deposits:** `Harvested`, `Cooldown`, and `CooldownTime` so the engine can apply exhaustion math and respect active cooldown timers.

Each field can also be patched through `RoomObjectPatchPayload`, and `RoomContractMapper` writes the updates back to Mongo using the shared `RoomDocumentFields` constants. Harvest and extractor handlers must use these typed fields instead of ad-hoc dictionaries to keep the driver/engine boundary storage-agnostic.

## Schema Gaps & Parity Requirements

The following fields are **missing from `RoomObjectSnapshot`/`RoomObjectPatchPayload`** and are **required for E7 parity validation** (verified against Node.js engine source):

### Controller Fields

**Missing:** `SafeModeAvailable` (int) - Counter of available safe mode activations

- **Node.js behavior:** Incremented by 1 on every controller level-up (`upgradeController.js` line 73)
- **Current state:** Only `SafeMode` (active timer) exists in snapshot
- **Required for parity:** YES - affects safe mode activation logic
- **Blocking:** Controller level-up transitions (deferred from E2.3 ControllerIntentStep)
- **Schema change needed:**
  - Add `int? SafeModeAvailable = null` to `RoomObjectSnapshot` constructor
  - Add `int? SafeModeAvailable { get; init; }` to `RoomObjectPatchPayload`
  - Update `RoomContractMapper.ApplyPatchToDocument` to write `safeModeAvailable` field
  - Update all controller-related tests

**Tracking:** See `docs/engine/e2.3-plan.md` "Controller Intents (Deferred - PARITY-BLOCKING)"

### Global User Fields

**Missing:** GCL increment mutation via `IGlobalMutationWriter`

- **Node.js behavior:** Calls `bulkUsers.inc(user, 'gcl', amount)` on **every** controller upgrade (`upgradeController.js` line 80-82)
- **Current state:** `UserState` has `Gcl` field but no mutation path
- **Required for parity:** YES - affects user progression and room control limits
- **Blocking:** Controller upgrade intent (deferred from E2.3 ControllerIntentStep)
- **Implementation needed:**
  - Add `IGlobalMutationWriter.IncrementUserGcl(userId, amount)` method
  - Wire through `RoomProcessorContext` or separate global mutation sink
  - Implement in driver layer to batch user updates

**Tracking:** Blocked by E5 (Global Systems) - global mutation writer implementation

### Boost System

**Ready:** `CreepBodyPartSnapshot.Boost` field exists

- **Node.js behavior:** Calculates boosted upgrade power using `C.BOOSTS[WORK][boostType].upgradeController` multipliers (`upgradeController.js` lines 31-53)
- **Current state:** Boost field exists in schema but no constants or calculation logic
- **Required for parity:** YES - affects controller upgrade speed and GCL accumulation
- **Blocking:** Controller upgrade intent (deferred from E2.3 ControllerIntentStep)
- **Implementation needed:**
  - Add boost constants to `ScreepsGameConstants` (e.g., `BOOSTS[WORK]["UH"]["upgradeController"] = 2`)
  - Implement boost calculation helper in `ControllerIntentStep.CalculateUpgradePower`
  - Handle boost consumption (decrements `CreepBodyPartSnapshot.Boost` after use)

**Tracking:** See `docs/engine/e2.3-plan.md` "Controller Intents (Deferred - PARITY-BLOCKING)"

### Non-Critical Gaps (can defer to post-MVP)

- **Notifications:** `driver.sendNotification(userId, message)` - User experience only
- **Event log emissions:** `EVENT_UPGRADE_CONTROLLER`, `EVENT_TRANSFER`, etc. - Replay/visualization only

**E7 Requirement:** Schema gaps and boost system MUST be resolved before parity validation. Notifications and event logs can be deferred to post-MVP.

## Implementation Plan

1. **Introduce driver contracts (Step E2.1).** ✅ (handled on driver side; see D10 status)

2. **Add snapshot providers (Step E2.2).** ✅ Driver now exposes `IRoomSnapshotProvider` + `IInterRoomSnapshotProvider`; the engine wraps them via `RoomStateProvider`/`GlobalStateProvider` (registered through `AddEngineCore`).

3. **Wire engine consumption (Step E2.3).**
   - **In progress:** `RoomStateProvider`/`GlobalStateProvider` now pull `RoomSnapshot`/`GlobalSnapshot` from the driver, and `RoomProcessor` consumes them via the new `RoomProcessorContext` + `IRoomProcessorStep` pipeline. The current step set covers creep lifecycle, movement, combat resolution, structure decay, controller downgrade, power cooldowns, and intent event logging. Remaining work: port the rest of the legacy handlers (spawn logic, labs, notifications, etc.) so room diffs match the Node processor.
   - Shared structure metadata now lives in `ScreepsDotNet.Common.Structures.StructureBlueprintRegistry`, and the engine exposes `IStructureBlueprintProvider`/`StructureSnapshotFactory` through DI so room steps can materialize finished structures without duplicating Mongo constants.
   - Detailed task breakdown (dependencies, handler ports, telemetry work) lives in `docs/engine/e2.3-plan.md`.

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
