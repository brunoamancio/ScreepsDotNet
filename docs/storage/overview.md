# Storage Architecture Overview

This document provides a high-level overview of the storage subsystem used by ScreepsDotNet, including MongoDB collections, Redis keys, and connection management patterns.

---

## Storage Stack

ScreepsDotNet uses two primary storage technologies:

1. **MongoDB 7** - Document database for persistent game state
2. **Redis 7** - In-memory store for queues and environment keys

**Implementation:** `ScreepsDotNet.Storage.MongoRedis` provides repositories and services shared by HTTP backend, CLI, Driver, and Engine subsystems.

---

## MongoDB Collections (screeps database)

### User Collections
```javascript
users                   // User accounts, auth, badges
users.code              // Code branches (default, sim, tutorial)
users.memory            // RawMemory.get/set data
users.memory.segments   // RawMemory.segments[0-99]
users.notifications     // Notification queue
users.power_creeps      // Power creep definitions
```

### Room Collections
```javascript
rooms                   // Room metadata (active, status)
rooms.objects           // Game objects (creeps, structures, sources, etc.)
rooms.terrain           // Terrain tiles (wall/plain/swamp)
rooms.history           // Historical snapshots for charts
```

### Game Collections
```javascript
market.orders           // Market buy/sell orders
market.stats            // Historical market data
servers                 // Server metadata (/api/server/info)
```

**Details:** See [mongodb.md](mongodb.md) for schemas, indexing strategies, and repository patterns.

---

## Redis Keys

```
roomsQueue              # Room processing queue (driver)
runtimeQueue            # User code execution queue (driver)
gameTime                # Current game tick (environment service)
```

**Details:** See [redis.md](redis.md) for queue patterns, caching strategies, and pub/sub usage.

---

## Connection Management

### MongoDB Connection
**Connection String:** Configured in `appsettings.json`
```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "screeps"
  }
}
```

**Pattern:**
- Single `IMongoDatabase` instance injected via DI
- Repositories use `GetCollection<TDocument>(collectionName)`
- Connection pooling handled automatically by MongoDB.Driver

### Redis Connection
**Connection String:** Configured in `appsettings.json`
```json
{
  "Redis": {
    "ConnectionString": "localhost:16379"
  }
}
```

**Pattern:**
- Single `IConnectionMultiplexer` instance (StackExchange.Redis)
- Services use `GetDatabase()` for operations
- Connection multiplexing enabled by default

---

## Schema Versioning

**Current Approach:** No schema migrations (yet)
- Mongo schemas match legacy Node.js server
- Schema changes must maintain backward compatibility
- Future: Consider schema versioning for breaking changes

**Why:** Enables side-by-side operation of Node.js and .NET implementations during migration.

---

## Development Workflow

### Local Development
```bash
# Start MongoDB + Redis
docker compose -f src/docker-compose.yml up -d

# Verify connection
curl http://localhost:5210/health
```

### Docker Services
- **MongoDB:** `localhost:27017` (mongo-data volume)
- **Redis:** `localhost:16379` (ephemeral)

### Seed Data
Seeds run automatically on first start (empty `mongo-data` volume):
- `src/docker/mongo-init/seed-server-data.js`
- `src/docker/mongo-init/seed-users.js`

**Details:** See [seeding.md](seeding.md) for seed patterns and test fixtures.

---

## Testing

### Unit Tests
- Use fakes/mocks for repositories
- No external dependencies

### Integration Tests
- Use Testcontainers to spin up MongoDB + Redis
- Clean state per test
- See `ScreepsDotNet.Storage.MongoRedis.Tests` for examples

**Critical Rule:** ❌ Never rely on local Docker state in tests (use Testcontainers)

---

## Performance Considerations

### MongoDB
- **Bulk operations:** Always use `IBulkWriterFactory` for batched writes (10x faster)
- **Indexing:** Critical collections have indexes on frequently-queried fields
- **Projection:** Use projection to reduce network overhead
- **Connection pooling:** Handled automatically by driver

### Redis
- **Pipeline commands:** Batch operations when possible
- **Connection multiplexing:** Single connection handles all operations
- **TTL:** Use expiration for cache keys
- **Pub/Sub:** Separate connections for pub/sub vs commands

---

## Storage Patterns

### Repository Pattern
**✅ Good:**
```csharp
public class UserRepository(IMongoDatabase database)
{
    private readonly IMongoCollection<UserDocument> _users = database.GetCollection<UserDocument>("users");

    public async Task<UserDocument?> GetByIdAsync(string id)
        => await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
}
```

**❌ Bad:**
```csharp
// NEVER use BsonDocument
private readonly IMongoCollection<BsonDocument> _users;
```

### Bulk Writer Pattern
**✅ Good:**
```csharp
var bulkWriter = bulkWriterFactory.Create();
bulkWriter.Objects.Update(id, Builders<RoomObject>.Update.Set(o => o.Energy, 100));
await bulkWriter.FlushAsync();
```

**❌ Bad:**
```csharp
// NEVER call repository directly for mutations
await repository.UpdateAsync(...);  // Bypasses bulk batching
```

---

## Cross-References

- **MongoDB patterns:** [mongodb.md](mongodb.md)
- **Redis patterns:** [redis.md](redis.md)
- **Seed data:** [seeding.md](seeding.md)
- **Driver usage:** [docs/driver/d2-storage-adapters.md](../driver/d2-storage-adapters.md)
- **Backend usage:** [docs/backend/overview.md](../backend/overview.md)

---

## When to Update This Doc

- Adding new MongoDB collections
- Adding new Redis keys
- Changing connection management
- Introducing schema versioning
- Adding new storage patterns
