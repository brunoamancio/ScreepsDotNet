# Driver API Surface
_Last updated: January 11, 2026_

This document catalogs the public contract that the legacy Screeps engine expects from the driver module (`@screeps/driver`). Rewriting the driver in .NET requires recreating every member below with equivalent behavior, timing, and side-effects.

## 1. Configuration & Globals
| Member | Purpose | Notes |
| --- | --- | --- |
| `config.engine` | `EventEmitter` shared with the engine; emits lifecycle hooks (`init`, `mainLoopStage`, `runnerLoopStage`, `processorLoopStage`, `playerSandbox`, etc.) and exposes tuning knobs such as `mainLoopMinDuration`, `mainLoopResetInterval`, `cpuMaxPerTick`, `cpuBucketSize`, `customIntentTypes`, `historyChunkSize`, `enableInspector`. | Defined in `lib/index.js` as a merged view of `common.configManager.config`. Must support `.emit`, `.on`, and ad-hoc properties mutated by engine. |
| `config.engine.registerCustomObjectPrototype(objectType, name, opts)` | Registers custom object prototypes supplied by mods; stored in `exports.customObjectPrototypes` and later injected into runtimes. | Validates inputs, coerces properties to strings; used by mod loader. |
| `exports.customObjectPrototypes` | Array consumed by runtime bootstrap (passed into JS sandbox). | Keep up-to-date when mods register prototypes. |
| `exports.constants`, `exports.strongholds`, `exports.system` | Pass-throughs from `common.configManager.config.common`. | Exposed so engine/runtime can reuse canonical constants. |

## 2. Lifecycle & Tick Coordination
| Member | Purpose |
| --- | --- |
| `connect(processType)` | Initializes storage, config, runtime helpers, `pathFinder` (processor), queue listeners (main), and inspector hooks. Computes world size and emits `config.engine.init`. |
| `notifyTickStarted()` | Checks pause flag (`env.MAIN_LOOP_PAUSED`); publishes `TICK_STARTED` if simulation is running, otherwise rejects with `"Simulation paused"`. |
| `notifyRoomsDone(gameTime)` | Publishes the ROOMS_DONE event with the new tick number. |
| `incrementGameTime()` | Reads current gametime (via `common.getGametime`) and increments `env.GAMETIME`. |
| `getGameTime()` | Returns current gametime (shared helper). |
| `commitDbBulk()` | Legacy stub (returns `q.when()`); maintain for compatibility. |
| `saveIdleTime(name, time)` | Stub; the engine still calls it. |
| `startLoop(name, fn)` | Utility used by the runner to parallelize user execution via a generic pool (default thread count from `RUNNER_THREADS`). |

## 3. Queue Service (`driver.queue`)
`lib/queue.js` exposes queue handles backed by `common.storage.queue`:
- `create(name)` → object with `fetch()`, `markDone(id)`, `add(id)`, `addMulti(ids)`, `whenAllDone()`, `reset()`; engine calls this for `rooms` and `users` queues. Legacy runner passes a second argument ("read"/"write") that is ignored—compat layer should accept it.
- `resetAll()` → resets every registered queue (used by main loop watchdog).
- `createDoneListener(name, callback)` → subscribe to queue completion events via pub/sub.

## 4. Data Retrieval & Persistence
| Member | Purpose |
| --- | --- |
| `getAllUsers()` | Returns active users sorted by `lastUsedDirtyTime`; filters out paused/no CPU. |
| `getAllRoomsNames()` | Reads `env.ACTIVE_ROOMS`, clears it, returns the list. |
| `activateRoom(roomIdOrArray)` | Adds rooms to the active-set (also used after intents insertion); exposed indirectly via `saveUserIntents`. |
| `getRoomIntents(roomId)` / `clearRoomIntents(roomId)` | Loads or clears per-room intents (`rooms.intents`). |
| `saveUserIntents(userId, intents)` | Persists notify/global/room intents, enqueues rooms in the active set, fan-out notifications when needed. |
| `getRoomObjects(roomId)` | Returns `{ objects, users }` for a room; helper `mapById` shapes the documents keyed by `_id`. |
| `getRoomFlags(roomId)` | Fetches `rooms.flags`. |
| `getRoomTerrain(roomId)` | Fetches `rooms.terrain` and key-maps entries. |
| `getRoomInfo(roomId)` / `saveRoomInfo(roomId, info)` | Read/write the `rooms` metadata document. |
| `setRoomStatus(roomId, status)` | Updates `rooms.status`. |
| `getInterRoom()` | Aggregates global data for the inter-room processor (game time, creeps flagged for movement, accessible rooms, special room objects, market orders, power creeps, user intents + owning users). |
| `updateAccessibleRoomsList()` | Writes accessible-room list into `env.ACCESSIBLE_ROOMS`. |
| `updateRoomStatusData()` | Builds novice/respawn/closed dictionaries and stores them in `env.ROOM_STATUS_DATA`. |
| `mapViewSave(roomId, mapView)` | Persists map overlays in `env.MAP_VIEW<roomId>`. |
| `saveRoomEventLog(roomId, eventLog)` | Stores the serialized event log via `env.hset`. |
| `history` | Module with `saveTick`, `upload`, etc., used by processor for room history. |

## 5. Bulk Writer Factories
Each returns the helper from `lib/bulk.js` pre-bound to a collection:
- `bulkObjectsWrite()` → `rooms.objects`
- `bulkFlagsWrite()` → `rooms.flags`
- `bulkUsersWrite()` → `users`
- `bulkRoomsWrite()` → `rooms`
- `bulkTransactionsWrite()` → `transactions`
- `bulkMarketOrders()` / `bulkMarketIntershardOrders()` → `market.orders`
- `bulkUsersMoney()` → `users.money`
- `bulkUsersResources()` → `users.resources`
- `bulkUsersPowerCreeps()` → `users.power_creeps`

## 6. Runtime & Memory APIs
| Member | Purpose |
| --- | --- |
| `makeRuntime(userContext)` | Entry point defined in `lib/runtime/make.js`; builds/executes user code via the sandbox. |
| `runtime/user-vm.js` helpers | Manage ClearScript-equivalent isolates (currently `isolated-vm`): create per-user engines, load runtime bundle/snapshot, enforce memory limit, expose `_halt`, track metrics. |
| `sendConsoleMessages(userId, payload)` / `sendConsoleError(userId, err)` | Push logs/errors to users or log to console for NPC IDs. |
| `saveUserMemory(userId, memoryBlob)` | Writes Memory JSON string to `env.MEMORY<userId>` with a 2 MB guard. |
| `saveUserMemorySegments(userId, segments)` | Writes hashed segments via `env.hmset`. |
| `saveUserMemoryInterShardSegment(userId, payload)` | (Inside `makeRuntime`) persists inter-shard data. |
| `saveUserIntents` (see Section 4) and `clearGlobalIntents()` for `users.intents`. |
| `bufferFromBase64()` | Utility used by runtime to deserialize code uploads. |
| `addRoomToUser(roomId, userDoc, bulk)` / `removeRoomFromUser(...)` | Helper invoked by controller logic. |

## 7. Pathfinding & Terrain
| Member | Purpose |
| --- | --- |
| `getAllTerrainData()` | Inflates cached terrain blob (`env.TERRAIN_DATA`). |
| `pathFinder` | Exposes `create`, `init`, `search`, etc., backed by native module (`native/build/Release/native.node`). Processor calls `driver.pathFinder.make({ RoomPosition })` before movement checks. |
| `getWorldSize()` | Returns cached `{width,height}` computed in `connect`. |

## 8. Notifications & Messaging
| Member | Purpose |
| --- | --- |
| `sendNotification(userId, message)` | Adds entries to `users.notifications` (type `msg`). |
| `checkNotificationOnline(userId)` | Currently returns resolved promise; placeholder for future push gating. |
| `notifyRoomsDone`, `pubsub.publish(...)` | Covered in lifecycle section. |

## 9. Miscellaneous Helpers
- `mapById(array, callback)` → Converts arrays to `_id`-keyed dictionaries.
- `roomsStatsSave()` / `getRoomStatsUpdater(room)` → Accumulate and flush room stats.
- `saveIdleTime(name, time)` → Stub.
- `queue` export (see Section 3).
- `system`, `strongholds` constants (see Section 1).

## Engine Touchpoints
A non-exhaustive mapping of engine files to driver members (helpful when validating coverage):
| Engine Area | Driver Members |
| --- | --- |
| `main.js` | `connect`, `queue.create`, `notifyTickStarted`, `getAllUsers`, `getAllRoomsNames`, `commitDbBulk`, `incrementGameTime`, `notifyRoomsDone`, `updateAccessibleRoomsList`, `updateRoomStatusData`, `config.mainLoopCustomStage`, `queue.resetAll`. |
| `runner.js` | `connect`, `queue.create`, `startLoop`, `makeRuntime`, `sendConsoleMessages`, `sendConsoleError`, `saveUserMemory`, `saveUserMemorySegments`, `saveUserMemoryInterShardSegment`, `saveUserIntents`, `config.emit`. |
| `processor.js` | `connect`, `queue.create`, `getRoomIntents`, `getRoomObjects`, `getRoomTerrain`, `getGameTime`, `getRoomInfo`, `getRoomFlags`, `bulk* writers`, `clearRoomIntents`, `mapViewSave`, `saveRoomEventLog`, `history`, `activateRoom`, `saveRoomInfo`, `roomsStatsSave`, `getRoomStatsUpdater`, `addRoomToUser`, `removeRoomFromUser`, `sendNotification`, `pathFinder.make`, `updateAccessibleRoomsList`. |
| `processor/global.js` | `getInterRoom`, `bulkRoomsWrite`, `bulkUsersWrite`, `bulkTransactionsWrite`, `bulkUsersMoney`, `bulkUsersResources`, `bulkMarketOrders`, `bulkMarketIntershardOrders`, `bulkUsersPowerCreeps`, `clearGlobalIntents`, `activateRoom`. |
| Various intent handlers | `constants`, `strongholds`, `system`, `bufferFromBase64`, `sendNotification`, `updateRoomStatusData`, etc. |

Use this table as the authoritative reference when defining the .NET `IDriver` interface and its backing services.
