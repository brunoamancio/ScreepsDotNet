# Storage Adapter Design
_Last updated: January 11, 2026_

D2’s objective is to define how the .NET driver will replace `@screeps/common` storage helpers (Mongo/Redis/queue/pubsub) without breaking compatibility. This document captures the proposed architecture so implementation work can be split up cleanly.

## Goals
1. Mirror the semantics of `common.storage.db`, `env`, `pubsub`, and `queue` so the upcoming .NET engine can reuse the same persistence model without touching every caller (and so legacy modules still “feel” familiar during the transition).
2. Reuse `ScreepsDotNet.Storage.MongoRedis` whenever practical to avoid duplicating connection logic, serializers, and collection naming.
3. Provide testable abstractions so unit tests can plug in in-memory fakes while integration tests hit real Mongo/Redis instances.

## Key Responsibilities to Port
| Legacy Layer | Responsibilities | .NET Target |
| --- | --- | --- |
| `db` (Mongo) | CRUD on collections (`rooms.objects`, `users`, `transactions`, etc.), bulk operations, query builders. | `IMongoDataStore` built atop `MongoDB.Driver` with typed repositories already defined in `ScreepsDotNet.Storage.MongoRedis`. |
| `env` (Redis key/value + hashes) | Game-wide settings (`GAMETIME`, `ACCESSIBLE_ROOMS`, `MAP_VIEW*`, memory blobs, segments). | `IRedisKeyValueStore` using StackExchange.Redis; expose helpers for get/set, hash ops, publish. |
| `pubsub` | Event fan-out (`TICK_STARTED`, `ROOMS_DONE`, `user:<id>/console`, queue done notifications). | `IPubSubBus` built on Redis pub/sub; wrap subscribe/publish semantics with strongly typed channels. |
| `queue` | Durable FIFO lists for `rooms` + `users` (add/fetch/mark done/reset). | `IWorkQueue` abstraction backed by Redis lists or Mongo queues; match existing methods + reset-all. |
| `history` | Upload/save room history chunks. | Reuse `SeedDataService`’s Mongo connection plus filesystem/Blob storage if needed; expose `IRoomHistoryStore`. |

## Proposed Architecture
```
ScreepsDotNet.Driver
├── Storage
│   ├── IDriverDatabase (wraps Mongo operations)
│   ├── IDriverEnvironment (Redis key/value)
│   ├── IPubSubBus
│   ├── IWorkQueueProvider
│   └── IBulkWriterFactory
├── Services
│   ├── QueueService (rooms/users)
│   ├── BulkWriteService (rooms.objects, users, etc.)
│   ├── NotificationService (sendConsole/sendNotification)
│   ├── RoomDataService (getRoomObjects, mapViewSave, history)
│   └── ConfigService (tick settings, accessible rooms)
└── Runtime / Pathfinder / etc.
```

### Interfaces
- `IDriverDatabase`
  - `Task<IReadOnlyCollection<T>> FindAsync<T>(string collection, FilterDefinition<T> filter, ...)`
  - `Task<T?> FindOneAsync<T>(...)`
  - `Task BulkWriteAsync(string collection, IReadOnlyCollection<WriteModel<BsonDocument>> operations)`
  - Typed helpers for collections we already model in `ScreepsDotNet.Storage.MongoRedis` (e.g., `IRoomObjectsRepository`).
- `IDriverEnvironment`
  - `Task<string?> GetAsync(string key)` / `Task SetAsync(string key, string value)`
  - Hash helpers: `Task HashSetAsync(string key, IReadOnlyDictionary<string,string>)`, `Task HashGetAllAsync(string key)`
  - `Task<long> PublishAsync(string channel, string payload)`
- `IPubSubBus`
  - `Task PublishAsync(DriverChannel channel, string payload)`
  - `Task<IDisposable> SubscribeAsync(DriverChannel channel, Func<string,Task> handler)`
- `IWorkQueue`
  - `Task EnqueueAsync(string id)` / `Task EnqueueManyAsync(IEnumerable<string> ids)`
  - `Task<string?> FetchAsync()`
  - `Task MarkDoneAsync(string id)`
  - `Task ResetAsync()` / `Task WhenAllDoneAsync()`
- `IBulkWriterFactory`
  - `IBulkWriter<T> CreateRoomsObjectsWriter()` etc., where `IBulkWriter` exposes `Update`, `Insert`, `Remove`, `ExecuteAsync` mirroring `lib/bulk.js`.

### Bulk Writer Strategy
Reuse the semantics of `lib/bulk.js` (merge nested objects, strip `_` fields, dedupe inserts/updates) in a managed implementation:
- Keep operations in memory until `ExecuteAsync()`; translate into Mongo `UpdateOneModel` / `DeleteOneModel` / `InsertOneModel` before dispatch.
- Provide typed bulk writers for each collection so calling code can depend on strongly typed documents rather than `BsonDocument`.

### Queue Strategy
- Implement `RedisWorkQueue` with three keys per queue: `pending`, `processing`, `lock`. `FetchAsync` pops from `pending` (BRPOPLPUSH) into `processing`, `MarkDoneAsync` removes from `processing`. `ResetAsync` moves everything back to `pending`.
- Expose `QueueService.Create(string name, QueueMode mode)` returning `IWorkQueue`. Accept a mode parameter to stay backwards compatible with the legacy signature, even if .NET ignores it.

### Pub/Sub & Notifications
- Map constant channels (tick started, rooms done, user consoles) to an enum to avoid typos.
- Provide `NotificationService` that wraps `IPubSubBus` + `IDriverEnvironment` to fan out console messages, errors, system notifications. This ensures all fan-out logic lives in one place, simplifying logging/backpressure.

### Environment Helpers
Implement typed helpers over Redis keys used by the engine:
| Key | Helper |
| --- | --- |
| `MAIN_LOOP_PAUSED` | `Task<bool> IsSimulationPausedAsync()` |
| `GAMETIME` | `Task<long> GetGameTimeAsync()`, `Task IncrementGameTimeAsync()` |
| `ACTIVE_ROOMS` | `Task<IReadOnlyList<string>> DrainActiveRoomsAsync()` + `Task AddActiveRoomsAsync(IEnumerable<string>)` |
| `MAP_VIEW<room>` | `Task SaveMapViewAsync(string room, string payload)` |
| `ROOM_EVENT_LOG` (hash) | `Task SaveRoomEventLogAsync(string room, string payload)` |
| Memory keys (`MEMORY<user>`, `MEMORY_SEGMENTS<user>`) | `Task SaveMemoryAsync(string userId, string data)` etc. |

### Reuse From Existing Projects
- `ScreepsDotNet.Storage.MongoRedis` already defines repositories/documents for many collections. Plan is to:
  - Reference that project from `ScreepsDotNet.Driver`.
  - Wrap its repositories with driver-specific interfaces to avoid leaking storage details into higher layers.
- Redis connection settings & options can reuse the same configuration objects (`MongoRedisStorageOptions`).

## Implementation Phases
1. **Contracts (In progress)** — define the interfaces above in `ScreepsDotNet.Driver.Storage`; add unit tests to validate semantics (bulk write merge, queue behavior).
2. **Adapters** — implement Mongo + Redis adapters using options pulled from configuration (`appsettings` or CLI args) so both driver and backend share the same connection info.
3. **Services** — build higher-level services (QueueService, NotificationService, RoomDataService) that aggregate adapters to satisfy driver APIs.
4. **Compatibility Shim** — temporary layer that exposes the same shape as `@screeps/driver` to the legacy engine while calling into the new services, enabling incremental rollout.

Once these phases are complete we can mark D2 done and proceed to the sandbox work (D3).
