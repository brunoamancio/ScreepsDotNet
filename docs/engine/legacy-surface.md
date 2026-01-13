# Legacy Engine Surface Map

Status: drafted January 13, 2026 as part of **E1 – Map Legacy Engine Surface**.

This document records the public surface area of the legacy Screeps Node.js engine (`ScreepsNodeJs/engine/src`) so we can plan equivalent abstractions inside `ScreepsDotNet.Engine`. It focuses on executable entry points (`main.js`, `runner.js`, `processor.js`), the driver APIs they call, and the domain services the managed rewrite must provide.

## Sources Reviewed

- `ScreepsNodeJs/engine/src/main.js` (tick orchestrator / scheduler)
- `ScreepsNodeJs/engine/src/runner.js` (user runtime executor)
- `ScreepsNodeJs/engine/src/processor.js` plus `processor/global.js` and representative intent handlers
- `ScreepsNodeJs/engine/src/game/*` (runtime-facing API that reaches back into the driver)
- `ScreepsNodeJs/engine/src/utils.js` (driver discovery + shared helpers)

## 1. Legacy Entry Scripts

### main.js – “Main loop”
Responsibilities:
- Kick off each tick by notifying the driver (`driver.notifyTickStarted`) and fetching the tick’s user + room list (`driver.getAllUsers`, `driver.getAllRoomsNames`).
- Populate Redis-backed queues via `driver.queue.create('users'/'rooms', 'write')`, wait for completion, and reset queues if the loop stalls.
- Commit DB bulks (`driver.commitDbBulk`) before and after global processors.
- Run `processor/global`, increment game time, refresh derived room metadata (`driver.updateAccessibleRoomsList`, `driver.updateRoomStatusData`), and send `notifyRoomsDone`.
- Emit `mainLoopStage` events for monitoring and execute `config.mainLoopCustomStage()` hooks.

Required driver surface:
- Config emitter (`driver.config.emit`, tick duration knobs, custom hook delegates).
- Queue factory/reset operations.
- Bulk commit primitives + game time/environment updaters.
- Global processors (invoked via `require('./processor/global')` but backed by driver bulk writers).

### runner.js – “Runner loop”
Responsibilities:
- Dequeue user IDs (`driver.queue.create('users','read')`), create sandboxed runtimes (`driver.makeRuntime`), and persist outputs.
- Persist console logs/error, memory (main + segments + inter-shard), and intents (`driver.saveUser*`, `driver.saveUserIntents`).
- Notify watchers via `runnerLoopStage` events; optionally feed instrumentation (`driver.influxAccumulator` in legacy).
- Run forever through `driver.startLoop('runner', handler)` to integrate with the shared scheduler.

Required driver surface:
- Runtime coordinator (`makeRuntime`, `startLoop` semantics, watchdog/reset policies).
- Persistence APIs for user memory, segments, inter-shard data, intents.
- Console + error delivery sinks.
- Visibility resets (`driver.resetUserRoomVisibility`) and telemetry hooks.

### processor.js – “Room processor”
Responsibilities:
- For each room, gather inputs (`driver.getRoomIntents`, `getRoomObjects`, `getRoomTerrain`, `getRoomInfo`, `getRoomFlags`, `getGameTime`).
- Instantiate write buffers (`driver.bulkObjectsWrite`, `bulkUsersWrite`, `bulkFlagsWrite`, `bulkUsersPowerCreeps`, room stats updaters) and the pathfinder bridge (`driver.pathFinder.make`).
- Run pretick hooks (nukes, keepers, invaders), apply every structure/creep intent, and record action logs/events.
- Persist derived artifacts: map view diffs (`driver.mapViewSave`), history chunks (`driver.history.store`), event logs (`driver.saveRoomEventLog`), activation flags (`driver.activateRoom`), room info + stats.
- Clear intents (`driver.clearRoomIntents`) and emit `processRoom`/`processObject` events.
- Coordinates with `processor/global.js`, which reads inter-room payloads via `driver.getInterRoom()` and writes through bulk collections (rooms, users, transactions, market, power creeps, etc.).

Required driver surface:
- Comprehensive bulk writer factory (`bulkObjectsWrite`, `bulkRoomsWrite`, `bulkUsersWrite`, `bulkTransactionsWrite`, `bulkUsersMoney`, `bulkUsersResources`, `bulkUsersPowerCreeps`, `bulkMarketOrders`, `bulkMarketIntershardOrders`).
- History/map-view/notification services.
- Environment helpers (`activateRoom`, `addRoomToUser`, `removeRoomFromUser`, `sendNotification`, `strongholds`, etc.).
- Pathfinder host + cost-matrix APIs.
- Room stats updater / saver.

## 2. Driver API Usage Summary

| Category | Legacy Calls | Purpose | Managed Replacement Target |
| --- | --- | --- | --- |
| Config & Telemetry | `config.emit`, `config.mainLoopCustomStage`, `mainLoopMinDuration`, `mainLoopResetInterval` | Stage notifications, customizable hook injection, loop pacing. | `IEngineConfigService` + `IEngineTelemetrySink` (wraps driver’s `IDriverLoopHooks`). |
| Queues | `queue.create(name, mode)`, `queue.resetAll`, `usersQueue.addMulti/fetch/markDone`, `roomsQueue.whenAllDone` | Coordinate work between main/runner/processor. | Use .NET driver’s `IQueueService`/`IWorkQueueChannel`. |
| Runtime | `makeRuntime`, `startLoop`, `sendConsoleMessages`, `sendConsoleError`, `saveUserMemory*`, `saveUserIntents`, `resetUserRoomVisibility` | Execute player code; persist outputs. | `RuntimeCoordinator` + `IRuntimePersistenceService` inside driver; engine needs adapters to call them. |
| Storage/Bulk | `bulkObjectsWrite`, `bulkUsersWrite`, `bulkFlagsWrite`, `bulkRoomsWrite`, `bulkTransactionsWrite`, `bulkUsersMoney`, `bulkUsersResources`, `bulkUsersPowerCreeps`, `bulkMarketOrders`, `bulkMarketIntershardOrders`, `commitDbBulk` | Batch Mongo writes per tick. | `IBulkWriterFactory` + new repositories already in `ScreepsDotNet.Driver`; engine layer must consume same abstractions. |
| Game State Fetch | `getAllUsers`, `getAllRoomsNames`, `getRoomIntents`, `getRoomObjects`, `getRoomTerrain`, `getRoomInfo`, `getRoomFlags`, `getGameTime`, `getInterRoom` | Provide snapshots for processing. | `IRoomDataService`, `IUserDataService`, `IInterShardService` (already present in driver rewrite). |
| Environment | `notifyTickStarted`, `incrementGameTime`, `updateAccessibleRoomsList`, `updateRoomStatusData`, `notifyRoomsDone`, `activateRoom`, `addRoomToUser`, `removeRoomFromUser`, `clearRoomIntents`, `clearGlobalIntents` | Maintain global tick state. | `IDriverEnvironmentService` + new engine coordinator (ties into existing config/events). |
| Pathfinding | `pathFinder.make`, `pathFinder.search`, `fakeRuntime.RoomPosition`, `driver.pathFinder` | Provide pathfinder host inside processor/runtime. | Managed `IPathfinderService` (already native-backed). |
| History/Notifications | `mapViewSave`, `saveRoomEventLog`, `history.store`, `sendNotification`, `notifyRoomsDone`, `sendConsole*` | Publish artifacts for UI + players. | History/notification services completed in driver; engine must call through these interfaces. |
| Strongholds & NPCs | `strongholds`, `sendNotification`, invader/keeper helpers | Manage NPC logic. | New `INpcService`/`IStrongholdService` inside engine, backed by same data stores. |

## 3. Proposed .NET Abstractions

| Legacy Component | Planned ScreepsDotNet.Engine Component | Notes |
| --- | --- | --- |
| `main.js` loop | `EngineHost` + `MainLoopCoordinator` | Wraps driver queues (`IQueueService`), config (`IDriverConfigService`), and telemetry sink. Handles tick pacing + custom hooks. |
| `runner.js` loop | `RuntimeLoopWorker` | Reuses driver `RuntimeCoordinator`, persistence services, and telemetry, but lives inside engine assembly to allow direct invocation. |
| `processor.js` (room) | `RoomProcessor` module (per-room service) | Consumes `IRoomDataService`, `IBulkWriterFactory`, `IPathfinderService`, history/notification services. Intent handlers codified as modules under `ScreepsDotNet.Engine.Processor.Intents`. |
| `processor/global.js` | `GlobalProcessor` | Handles inter-room creep movement, market + power intents, shard operations; relies on bulk repositories + environment service. |
| Game runtime bindings (`game/*.js`) | `EngineRuntimeApi` / `IGameApiBridge` | Provide the fake runtime objects to player code. Tightly coupled to runtime service. |
| Notifications/history hooks | `HistoryPublisher`, `NotificationRouter` | Already exist in driver; engine will consume through interfaces instead of direct driver calls. |

## 4. Gaps & Questions (to resolve during E2/E3)

1. **IPC expectations:** The Node engine assumed separate Node processes communicating via Redis queues. Our managed rewrite can run in-process with the driver; confirm whether we still need compatibility channels for the legacy server (probably through a thin shim once the rewrite is complete).
2. **Custom config hooks:** `config.mainLoopCustomStage()` is user-extensible in legacy deployments. We need to define how (or whether) managed plugins register custom hooks.
3. **Influx metrics:** Legacy runner referenced `driver.influxAccumulator` but it is a no-op in most deployments. Decide whether to expose equivalent telemetry counters or rely solely on the new observability sink.
4. **Stronghold scripts:** The Node engine loads NPC behaviors from JSON/JS modules. Determine whether to embed these definitions as data files or convert them into C# strategy objects.
5. **Fake runtime helpers:** `processor/common/fake-runtime.js` emulates parts of the in-game API for system creeps. Decide whether to port this literally or expose a trimmed managed API.

## 5. Next Steps

1. Socialize this document with the driver/runtime owners to confirm the target interfaces (especially around queues, bulk writers, and telemetry).
2. Use the driver-side interfaces already implemented (`IRoomDataService`, `IBulkWriterFactory`, `IQueueService`, etc.) as the foundation for **E2 – Data & Storage Model** and **E3 – Intent Pipeline**.
3. Link this doc from `src/ScreepsDotNet.Engine/AGENT.md` and update it whenever we discover additional legacy entry points (e.g., CLI-triggered maintenance scripts).

This file satisfies the “E1 exit criteria” once the AGENT references it and downstream steps consume its mapping.
