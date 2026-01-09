# Market & World API Parity

This document captures the requirements for bringing the ScreepsDotNet backend to feature-parity with the legacy `backend-local` routes that live under `lib/game/api/market.js` and `lib/game/api/game.js`. It is the single source of truth for:

1. legacy route behavior (inputs, outputs, validation),
2. the Mongo collections/fields we must expose through typed POCO documents, and
3. the integration coverage we expect before considering the feature done.

The scope below covers both the read/management APIs that are consumed by the official client (`/api/game/*`) **and** the legacy write-heavy routes (spawn placement, construction intents, flags, invaders, notify toggles, manual intents). Use it as the canonical reference when touching any `/api/game/*` surface so Mongo schemas, validation rules, and integration tests stay aligned with backend-local.

---

## 1. Legacy Route Inventory

| Area   | Method & Route                                | Notes from Node implementation (`backend-local`)                                                                                                  |
|--------|-----------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------|
| Market | `GET /api/game/market/orders-index`           | Authenticated. Aggregates all *active* orders grouped by `resourceType`. Response contains `_id`, `count`, `buying`, `selling`.                     |
| Market | `GET /api/game/market/orders`                 | Authenticated. Requires `resourceType` query string. Returns all *active* orders for that resource. `price` stored as thousandths, divided by 1000. |
| Market | `GET /api/game/market/my-orders`              | Authenticated. Filters orders by `user`. Same price scaling as above.                                                                              |
| Market | `GET /api/game/market/stats`                  | Authenticated. Requires `resourceType`. Queries `market.stats` sorted by `date` desc; returns `{ stats: [...] }`.                                   |
| World  | `POST /api/game/map-stats`                    | Authenticated. Body `{ rooms: string[], statName: string }`. Validates stat name suffix digits. Returns `{ gameTime, stats, statsMax, users }`.      |
| World  | `GET /api/game/room-status`                   | Authenticated. Query `room`. Returns `{ room: { status, novice, respawnArea, openTime } }`.                                                         |
| World  | `GET /api/game/room-terrain`                  | Public. Query `room`, optional `encoded`. Without `encoded` decodes terrain into array of `{room, type, terrain}` like Node’s `common.decodeTerrain`. |
| World  | `POST /api/game/rooms`                        | Public. Body `{ rooms: string[] }`. Returns raw records from `rooms.terrain` for shard-level UI.                                                    |
| World  | `GET /api/game/world-size`                    | Public. Returns `{ width, height }`, cached per process. Uses `common.calcWorldSize` over `rooms` collection.                                       |
| World  | `GET /api/game/time`                          | Public. Equivalent to `common.getGametime()`, response `{ time }`.                                                                                  |
| World  | `GET /api/game/tick`                          | Public. Uses in-memory rolling min of last 30 tick durations. For parity we can proxy the node semantics via metrics captured from storage later.   |

Remaining backlog (detailed in §6): shard-scoped helper routes plus support for custom intent/object types declared via `mods.json`. These depend on extra storage seeds and configuration plumbing.

---

## 2. Storage Schema Plan

### 2.1 Market Collections

| Collection         | Required Fields                                                                                                                                                                                                              | Notes |
|--------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------|
| `market.orders`    | `_id` (ObjectId), `active` (bool), `type` (`"buy"`/`"sell"`), `user` (string, optional for NPC orders), `roomName`, `resourceType`, `remainingAmount`, `amount`, `totalAmount`, `price` (stored as int = credits \* 1000), `created`, `createdTimestamp`. | Need projection models that expose `price` as decimal for HTTP responses. |
| `market.stats`     | `_id`, `resourceType`, `date` (`YYYY-MM-DD` string), `transactions`, `volume`, `avgPrice` (double), `stddevPrice` (double).                                                                                                   | Integration harness must seed at least 2 days of stats for assertions.    |

### 2.2 World Collections

| Collection         | Required Fields                                                                                                                                                                                                                                                                                                           |
|--------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `rooms`            | `_id` (string room name), `status`, `novice`, `respawnArea`, `openTime`, `nextNpcMarketOrder`, `powerBankTime`, etc. For parity we must at least read `status`, `novice`, `respawnArea`, `openTime`.                                                                               |
| `rooms.objects`    | `_id`, `room`, `type`, `user`, `level`, `reservation.user`, `reservation.endTime`, `sign.user`, `sign.text`, `sign.time`, `safeMode`, `mineralType`, `density`. Used by `map-stats` to derive controller ownership, reservations, invader cores, minerals, safe mode flags.                                                   |
| `rooms.terrain`    | `_id`, `room`, `type`, `terrain` (string). When `type == "terrain"` we must decode to legacy array; otherwise return raw.                                                                                                                                                |
| `rooms.flags`      | `_id`, `user`, `room`, `data` (flag payload). Needed later for flag endpoints; tracked here for completeness.                                                                                                                                                                                                             |
| `users`            | `_id`, `username`, `badge`. Already mapped via `UserDocument`; we need to ensure the only extra fields referenced by these world endpoints (`badge`) are part of the POCO.                                                                                                      |

> **Update (Jan 2026):** Both the docker init scripts and the Testcontainers seed harness now include a `shard1` sample (`SeedDataDefaults.World.SecondaryShardRoom`) across `rooms`, `rooms.objects`, and `rooms.terrain`. Each document persists a `shard` field so shard-aware repositories/tests can filter deterministically.

All new POCOs should live under `ScreepsDotNet.Storage.MongoRedis.Repositories.Documents`. Planned additions/changes:

| POCO                                | Purpose                                                                                               |
|-------------------------------------|-------------------------------------------------------------------------------------------------------|
| `MarketOrderDocument`               | Maps to `market.orders`. Includes helper to compute decimal price.                                    |
| `MarketStatsDocument`               | Maps to `market.stats`.                                                                               |
| `RoomStatusDocument` (extend `Room`) | Add `Status`, `Novice`, `RespawnArea`, `OpenTime`, `NextNpcMarketOrder`.                               |
| `RoomObjectDocument` extensions     | Add nested types for `ReservationDocument`, `SignDocument`, `MineralDocument`.                        |
| `RoomTerrainDocument`               | Wraps `room`, `type`, `terrain`.                                                                      |

---

## 3. Repository & Endpoint Contracts

To keep the HTTP layer slim we will introduce the following repository interfaces in `ScreepsDotNet.Backend.Core` (with Mongo-backed implementations in `ScreepsDotNet.Storage.MongoRedis`):

| Interface                          | Methods (Async)                                                                                                   | Notes |
|-----------------------------------|--------------------------------------------------------------------------------------------------------------------|-------|
| `IMarketOrderRepository`         | `Task<IReadOnlyList<MarketResourceSummary>> GetActiveOrderIndexAsync()`, `Task<IReadOnlyList<MarketOrder>> GetOrdersByResourceAsync(string resourceType)`, `Task<IReadOnlyList<MarketOrder>> GetOrdersByUserAsync(string userId)` | `MarketOrder.PriceCredits` should be decimal. |
| `IMarketStatsRepository`         | `Task<IReadOnlyList<MarketStatsEntry>> GetStatsAsync(string resourceType)`                                                                               | Sort desc by `date`. |
| `IWorldStatsRepository`          | `Task<MapStatsResponse> GetMapStatsAsync(MapStatsRequest request)` pulling from `rooms`, `rooms.objects`, `users`. | Encapsulates `common.getGametime` equivalent via `ITickClock`. |
| `IRoomStatusRepository`          | `Task<RoomStatus?> GetRoomStatusAsync(string room)`                                                                                                       | Wraps `rooms` collection. |
| `IRoomTerrainRepository`         | `Task<IReadOnlyList<RoomTerrainDocument>> GetTerrainAsync(IEnumerable<string> rooms)` + decoder helper.                                                 | Used for both `/room-terrain` and `/rooms`. |
| `IWorldMetadataRepository`       | `Task<WorldSize> GetWorldSizeAsync()` plus cached result, `Task<int> GetGameTimeAsync()`.                                                                | May reuse existing tick service once introduced. |

**Routing additions**

Extend `ApiRoutes` with:

```
public static class Game
{
    private const string Base = "/api/game";
    public static class Market
    {
        public const string OrdersIndex = $"{Base}/market/orders-index";
        public const string Orders = $"{Base}/market/orders";
        public const string MyOrders = $"{Base}/market/my-orders";
        public const string Stats = $"{Base}/market/stats";
    }
    public static class World
    {
        public const string MapStats = $"{Base}/map-stats";
        public const string RoomStatus = $"{Base}/room-status";
        public const string RoomTerrain = $"{Base}/room-terrain";
        public const string Rooms = $"{Base}/rooms";
        public const string WorldSize = $"{Base}/world-size";
        public const string Time = $"{Base}/time";
        public const string Tick = $"{Base}/tick";
    }
}
```

Add dedicated endpoint classes (`MarketEndpoints`, `WorldEndpoints`) wired up in `EndpointRegistration`.

---

## 4. Integration Coverage Expectations

Every route must have:

1. **Unit tests** covering validation/edge cases (e.g., missing resource type, empty rooms array, malformed stat name).
2. **Integration tests** under `ScreepsDotNet.Backend.Http.Tests/Integration` that boot real Mongo/Redis containers via Testcontainers and seed deterministic data.
3. **HTTP scratch entries** (`MarketEndpoints.http`, `WorldEndpoints.http`) mirroring the new routes for manual smoke testing.

| Scenario                                              | Test Fixture                                                                 | Seed Data Needed                                                                                                                  |
|-------------------------------------------------------|-------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------|
| Orders index aggregates buy/sell counts               | `MarketEndpointsIntegrationTests.OrdersIndex_ReturnsAggregatedCounts`        | `market.orders`: NPC sell + player buy orders with varying `resourceType`.                                                         |
| Orders listing divides price by 1000                  | `MarketEndpointsIntegrationTests.Orders_ReturnsScaledPrices`                 | `market.orders`: price stored as integer 7500 => expect 7.5.                                                                        |
| My orders filters by authenticated user               | `MarketEndpointsIntegrationTests.MyOrders_ReturnsPlayerOrdersOnly`           | At least one user-specific order plus NPC entries.                                                                                 |
| Market stats sorted descending                        | `MarketEndpointsIntegrationTests.Stats_ReturnsLatestFirst`                   | `market.stats`: two documents with consecutive `date` strings.                                                                      |
| Map stats merges controller ownership + minerals      | `WorldEndpointsIntegrationTests.MapStats_ReturnsOwnershipAndMinerals`        | `rooms` entry + `rooms.objects` with controllers, minerals, signs, invader cores; `users` collection for username/badge lookups.    |
| Room status returns novice/respawn info               | `WorldEndpointsIntegrationTests.RoomStatus_ReturnsStatusFlags`               | `rooms`: sample doc with `status`, `novice`, etc.                                                                                  |
| Room terrain encoded vs decoded responses             | `WorldEndpointsIntegrationTests.RoomTerrain_ReturnsDecodedTerrain`           | `rooms.terrain`: `type: "terrain"` doc covering known pattern; add non-terrain doc to ensure pass-through.                         |
| Rooms bulk returns multiple entries                   | `WorldEndpointsIntegrationTests.Rooms_ReturnsAllRequestedTerrains`           | Same as above.                                                                                                                     |
| World size cached                                     | `WorldEndpointsIntegrationTests.WorldSize_ComputesFromRooms`                 | `rooms`: set of rooms representing at least 2x2 sector; assert consistent width/height.                                            |
| Time endpoint uses monotonic gametime                 | `WorldEndpointsIntegrationTests.Time_ReturnsSeededGametime`                  | Provide deterministic gametime via stub or `server.data`.                                                                          |

---

## 5. Work Breakdown

1. **Spec & Documentation (this document + AGENT summary)** – ✅ when committed.
2. **Data layer** – Introduce POCOs, repository interfaces, Mongo implementations, DI registration, docker/test harness seed updates.
3. **HTTP layer** – Routes, DTOs, validators, endpoint wiring, HTTP scratch files.
4. **Testing** – Unit + integration suites, ensuring docker compose + Testcontainers seeds stay in sync.

Deliverables #2–#4 should be developed in small, reviewable increments (market first, then world). This reduces the blast radius and keeps parity verifiable at each checkpoint.

---

## 6. Outstanding backlog – shard helpers & mod-defined intents

| Gap | Legacy behavior (Node reference) | Collections / config | Notes & action items |
|-----|----------------------------------|----------------------|----------------------|
| `GET /api/game/room-overview` | Returns `{ owner, stats, statsMax, totals }` for a room (see `backend-local/lib/game/api/game.js`, around the `router.get('/room-overview', …)` block). Currently only partially implemented in Node but the route exists and the client expects the owner badge + placeholder stats. | `rooms.objects`, `users` (resolve controller ownership) | Expose the route (even if stats remain empty) so the client stops erroring when it queries room overview panels. |
| `POST /api/game/gen-unique-flag-name` | Generates the next `FlagN` name that does not collide with the user’s existing flags. | `rooms.flags` (via `getFlags`/`setFlags`) | Needed so the in-game UI can suggest flag names before calling `/create-flag`. Requires the same Redis/Mongo helpers already wired for the flag CRUD routes. |
| `POST /api/game/check-unique-flag-name` | Validates that a proposed flag name is unused; returns `{}` or rejects with `name exists`. | `rooms.flags` | Used by the client before accepting manual names. Shares the same data path as `gen-unique-flag-name`. |
| `POST /api/game/set-notify-when-attacked` | Toggles the `notifyWhenAttacked` flag on a structure, enforcing controller ownership (see the `router.post('/set-notify-when-attacked', …)` block). | `rooms.objects` | Required for the structure detail panel. Implementation mirrors the legacy checks: verify ownership/reservation for controllers in the same room, then update the target object document. |
| Shard-scoped queries | Legacy comments call out “rooms terrain by shard”, and official clients send a `shard` field alongside `room` arrays. The Node route simply fetches `rooms.terrain` entries by name, but multi-shard setups expect us to honor the `shard` part of the payload (when prefixed as `shard/room`). | `rooms`, `rooms.terrain`, `rooms.objects` | ✅ Implemented: repositories now accept `RoomReference` (room + optional shard) and `/api/game/map-stats`, `/room-status`, `/room-terrain`, and `/rooms` expose optional `shard` parameters (query/body) so operators can hit the seeded `shard1` fixtures deterministically. |
| Mod-defined intent/object types | Mods can register additional sanitizers via `config.backend.customIntentTypes` / `customObjectTypes` (see `server/common/lib/system.js` and `backend-local/lib/game/server.js`). Node automatically merges definitions from every `mods/*.js`. ScreepsDotNet currently ignores these blocks. | `mods.json` + mod scripts, `system.sanitizeUserIntents` | Load custom intent/object definitions from `mods.json` (same file we already watch for bot manifests) and plumb them through the CLI + HTTP pipelines: <ul><li>`IntentEndpoints`, `MongoIntentService`, and CLI intent commands should pass the merged dictionary to the sanitizer.</li><li>Expose the definitions via `/api/server/info` so the client knows about custom objects.</li></ul> Add integration coverage using a sample mod directory that defines at least one faux intent. |

### Next steps for this backlog

1. **Seed data** – Add a second shard (e.g., `shard1`) to both docker and Testcontainers fixtures, including terrain/object documents so we can assert shard filtering.
2. **Repository updates** – Plumb `shard` identifiers through `IRoomTerrainRepository`, `IWorldStatsRepository`, `IFlagService`, etc., so the HTTP layer can honor the optional filter without duplicating logic.
3. **Endpoint work** – Implement the four missing routes plus shard-aware variants of the existing ones. Update `ApiRoutes`, `.http` scratch files, and AGENT docs accordingly.
4. **Mod plumbing** – Extend the configuration bootstrapping (CLI + HTTP hosts) to read `mods.json`, load any `customIntentTypes/customObjectTypes`, and pass them into `MongoIntentService` / `UserResponseFactory`. Add tests that drop a sample mod under `tests/fixtures/mods` and assert that the new intent shows up in `/api/server/info` and gets sanitized correctly.
