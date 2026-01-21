# Engine Data Model Plan (E2)

**Status:** E2.3 is 95% Complete - January 21, 2026

**Progress:**
- E2.1-E2.2: ✅ Complete (driver contracts, snapshot providers)
- E2.3: ⚠️ 95% Complete (11/11 handler families, 240/240 tests, 4 features blocked by E5)
- E2.4: ✅ Complete (mutation writers, memory sinks)
- E2.5: ✅ Complete (test coverage, parity checks)
 
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

## Schema Implementation Status

### ✅ Implemented Features

#### Controller Fields
- ✅ **SafeModeAvailable** - COMPLETE (January 20, 2026)
  - Implemented in `RoomObjectSnapshot` and `RoomObjectPatchPayload`
  - Tracked by `ControllerIntentStep` during level transitions
  - Tests verify counter increments on level-up
  - **Schema:** `int? SafeModeAvailable` in snapshot/patch contracts

#### Boost System
- ⚠️ **Partially Implemented** (January 20, 2026)
  - ✅ Controller progress calculation complete (`CalculateBoostEffect`)
  - ✅ Constants exist (`WorkBoostUpgradeMultipliers`: GH 1.5x, GH2O 1.8x, XGH2O 2.0x)
  - ✅ 4 comprehensive tests covering boost mechanics
  - ❌ GCL updates blocked by E5 (requires `IGlobalMutationWriter.IncrementUserGcl`)
  - **Schema:** `CreepBodyPartSnapshot.Boost` field ready

#### Power Effects System
- ✅ **Power Effect Infrastructure** - COMPLETE (January 21, 2026)
  - `PowerEffectSnapshot` added to `RoomObjectSnapshot.Effects` (dictionary keyed by power type)
  - `PowerEffectDecayStep` removes expired effects each tick
  - `PowerAbilityStep` implements 18 power abilities (1 deferred to E5)
  - Effect consumption in Lab/PowerSpawn/Factory handlers
  - 58 power ability tests (decay, core abilities, effect-based, direct-action)
  - **Schema:** `Dictionary<PowerTypes, PowerEffectSnapshot>? Effects` in snapshot

### ⚠️ Blocked by E5 (Global Systems)

The following features require E5 global mutation infrastructure. See `docs/engine/e5.md` for details.

#### Global User Mutations
1. **User GCL updates** - Controller upgrades should increment global GCL
   - **Needs:** `IGlobalMutationWriter.IncrementUserGcl(userId, amount)`
   - **Impact:** Both boosted and non-boosted GCL gains blocked
   - **Parity:** Required for E7 validation

2. **User power balance** - PowerSpawn processing should increment global power
   - **Needs:** `IGlobalMutationWriter.IncrementUserPower(userId, amount)`
   - **Impact:** Power balance tracking for generateOps ability
   - **Parity:** Required for E7 validation

3. **PWR_GENERATE_OPS ability** - Power creep ability to generate ops
   - **Needs:** `IGlobalMutationWriter.DecrementUserPower(userId, amount)`
   - **Impact:** 1 of 19 power abilities blocked
   - **Parity:** Required for E7 validation

### 📝 Deferred (Non-Parity-Critical)

These features don't affect simulation correctness and can be implemented post-E7:

1. **Event log emissions** - `EVENT_TRANSFER`, `EVENT_UPGRADE_CONTROLLER`, etc. (replay visualization)
2. **Level-up notifications** - User notifications on controller level-up (UX only)
3. **Stats recording** - Power processed, resources transferred, etc. (analytics only)

## Implementation Status

### 1. Driver Contracts (E2.1) - ✅ Complete
- Driver-side snapshot providers (`IRoomSnapshotProvider`, `IInterRoomSnapshotProvider`)
- Engine DTOs in `ScreepsDotNet.Driver.Contracts`
- Room/global state contracts
- Action log and harvest metadata surfaces

### 2. Snapshot Providers (E2.2) - ✅ Complete
- `RoomStateProvider`/`GlobalStateProvider` wrap driver contracts
- Registered through `AddEngineCore` DI
- Engine consumes `RoomSnapshot`/`GlobalSnapshot` without database knowledge

### 3. Engine Consumption (E2.3) - ⚠️ 95% Complete
**Implemented Handlers (11/11 families, 240/240 tests):**
- ✅ Movement (25 tests) - crashes, pull chains, inter-room/portal transfers
- ✅ Spawn (18 tests) - create, renew, recycle
- ✅ Lifecycle (included) - TTL expiration, despawn
- ✅ Build/Repair (12 tests) - construction sites, structure repairs
- ✅ Harvest (15 tests) - sources, minerals, deposits
- ✅ Towers (10 tests) - attack, heal, repair with range falloff
- ✅ Resource I/O (31 tests) - transfer, withdraw, pickup, drop
- ✅ Controller (12 tests) - upgrade, reserve, attack, level transitions
- ✅ Lab (27 tests) - reactions (62 formulas), boosts (40 types), unboost
- ✅ Structure Energy Routing (28 tests) - Links (8), PowerSpawns (9), Factories (11)
- ✅ Power Abilities (58 tests) - 18 abilities, effect decay, cooldowns
- ✅ Market (4 tests) - orders, deals, terminal sends
- ✅ Inter-Room Transfers (included) - edge exits, portals

**Infrastructure:**
- ✅ `StructureBlueprintRegistry` - Shared structure metadata
- ✅ `IStructureBlueprintProvider`/`StructureSnapshotFactory` - Structure materialization
- ✅ `RoomProcessorContext` + `IRoomProcessorStep` pipeline
- ✅ Combat resolution, decay, downgrade systems
- ✅ Power effect tracking (`PowerEffectDecayStep`, `PowerAbilityStep`)
- ✅ Intent event logging (`RoomIntentEventLogStep`)

**Blocked by E5 (4 features):**
- PWR_GENERATE_OPS power ability
- User power balance tracking (PowerSpawn)
- User GCL updates (Controller)
- Boost effects GCL component (Controller)

See `docs/engine/e2.md` for detailed handler breakdown.

### 4. Mutation Path Alignment (E2.4) - ✅ Complete
- `RoomMutationWriterFactory` produces per-room writers
- Writers stage JSON upserts/patches and flush via `IRoomMutationDispatcher`
- `UserMemorySink` wraps `IUserDataService` for memory persistence
- Event log and map view payloads integrated
- Global mutation writer design ready (pending E5 implementation)

### 5. Tests & Parity Checks (E2.5) - ✅ Complete
- 240/240 engine tests passing
- Driver regression fixtures for room/global snapshots
- Unit tests cover `RoomStateProvider`/`GlobalStateProvider` consumers
- Integration tests use Testcontainers (never local Docker state)

## E2 Deliverables

### ✅ Completed
- ✅ Driver contracts + snapshot provider interfaces (E2.1)
- ✅ Engine consumes only driver contracts (no `ScreepsDotNet.Storage.MongoRedis` references)
- ✅ Documentation updated (this file + `src/ScreepsDotNet.Engine/CLAUDE.md`)
- ✅ 240 unit tests covering snapshot building, mutation contract serialization, handler logic
- ✅ 11/11 handler families implemented with full test coverage
- ✅ Mutation writers integrated with driver `IRoomMutationDispatcher`
- ✅ Memory persistence via `UserMemorySink`

### ⚠️ Remaining Work (Blocked by E5)
- E5 must implement `IGlobalMutationWriter` for global user mutations (GCL, power balance)
- After E5 Phase 1, return to E2.3 to implement 4 blocked features (1-2 hours effort)

### Next Steps
With E2 95% complete, the engine is ready for **E3 (Intent Gathering & Validation)** and can proceed to E7 parity validation once E5 global mutations are in place. The engine operates entirely through driver abstractions without touching the database layer.
