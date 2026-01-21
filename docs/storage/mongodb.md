# MongoDB Patterns & Collections

This document details MongoDB-specific patterns, collection schemas, and best practices used in ScreepsDotNet.

---

## Collection Reference

### users
**Purpose:** User accounts, authentication, badges

**Key Fields:**
```csharp
_id                 // string - User ID
username            // string - Display name
password            // string - Hashed password
cpu                 // int - CPU limit
cpuAvailable        // int - CPU bucket
gcl                 // int - Global Control Level
power               // int - Power balance
badge               // object - Badge configuration
steam               // object - Steam auth data
```

**Indexes:**
- `_id` (primary key)
- `username` (unique)

---

### users.code
**Purpose:** User code branches (main, sim, tutorial)

**Key Fields:**
```csharp
_id                 // string - User ID
branch              // string - Branch name (default, sim, tutorial)
modules             // object - Module name → code mapping
timestamp           // long - Last updated timestamp
```

**Indexes:**
- `_id` (primary key)

---

### users.memory
**Purpose:** RawMemory.get/set persistent data

**Key Fields:**
```csharp
_id                 // string - User ID
data                // string - JSON-serialized memory
```

**Indexes:**
- `_id` (primary key)

---

### users.memory.segments
**Purpose:** RawMemory.segments[0-99] persistent segments

**Key Fields:**
```csharp
_id                 // string - {userId}${segmentId}
data                // string - Segment data (100KB max)
```

**Indexes:**
- `_id` (primary key)

---

### users.notifications
**Purpose:** User notification queue

**Key Fields:**
```csharp
_id                 // ObjectId - Notification ID
user                // string - User ID
message             // string - Notification text
date                // long - Timestamp
read                // bool - Read status
type                // string - Notification type
```

**Indexes:**
- `user` + `date` (compound)
- `user` + `read` (compound)

---

### users.power_creeps
**Purpose:** Power creep definitions

**Key Fields:**
```csharp
_id                 // string - Power creep ID
user                // string - Owner user ID
name                // string - Power creep name
className           // string - Power creep class
level               // int - Current level
powers              // object - Unlocked powers
shard               // string - Current shard
spawnCooldownTime   // long - Respawn cooldown
deleteTime          // long? - Pending deletion time
```

**Indexes:**
- `_id` (primary key)
- `user` (indexed)

---

### rooms
**Purpose:** Room metadata (status, controller level, active flag)

**Key Fields:**
```csharp
_id                 // string - Room name
active              // bool - Active flag
status              // string - Status (normal/novice/respawn)
novice              // long? - Novice protection end time
openTime            // long? - Room open time
lastSurvivalTime    // long? - Last survival tick
```

**Indexes:**
- `_id` (primary key)
- `active` (indexed)

---

### rooms.objects
**Purpose:** Game objects (creeps, structures, sources, minerals, etc.)

**Key Fields:**
```csharp
_id                 // string - Object ID
room                // string - Room name
type                // string - Object type (creep, spawn, etc.)
x                   // int - X coordinate
y                   // int - Y coordinate
user                // string? - Owner user ID
// ... type-specific fields (energy, hits, store, etc.)
```

**Indexes:**
- `_id` (primary key)
- `room` (indexed for room queries)
- `room` + `type` (compound for filtered queries)

**Type-Specific Fields:**
- **Creeps:** body, fatigue, hits, hitsMax, spawning, ticksToLive, store
- **Structures:** hits, hitsMax, structureType, store, energy, energyCapacity
- **Sources:** energy, energyCapacity, ticksToRegeneration
- **Minerals:** mineralType, mineralAmount, density, ticksToRegeneration

---

### rooms.terrain
**Purpose:** Terrain tiles (wall/plain/swamp)

**Key Fields:**
```csharp
_id                 // string - Room name
room                // string - Room name (duplicate for compatibility)
terrain             // string - Packed terrain data (50x50 grid)
```

**Encoding:** Each tile is 2 bits: `00` plain, `01` wall, `10` swamp
**Format:** Base64-encoded packed bytes

**Indexes:**
- `_id` (primary key)

---

### rooms.history
**Purpose:** Historical snapshots for charts and replay

**Key Fields:**
```csharp
_id                 // ObjectId - History entry ID
room                // string - Room name
user                // string - User ID
tick                // long - Game tick
stats               // object - Room statistics
```

**Indexes:**
- `room` + `tick` (compound)
- `user` + `tick` (compound)

---

### market.orders
**Purpose:** Market buy/sell orders

**Key Fields:**
```csharp
_id                 // string - Order ID
user                // string - Owner user ID
resourceType        // string - Resource type
type                // string - "buy" or "sell"
price               // int - Price in thousandths (stored * 1000)
totalAmount         // int - Total order amount
remainingAmount     // int - Remaining amount
roomName            // string? - Room name
active              // bool - Active flag
```

**Indexes:**
- `_id` (primary key)
- `resourceType` + `active` (compound)
- `user` (indexed)

**Price Scaling:** Prices stored as `price * 1000` (integer), divided by 1000 on read

---

### market.stats
**Purpose:** Historical market statistics

**Key Fields:**
```csharp
_id                 // ObjectId - Stats entry ID
resourceType        // string - Resource type
date                // string - Date string (YYYY-MM-DD)
avgPrice            // double - Average price
transactions        // int - Transaction count
volume              // int - Total volume
```

**Indexes:**
- `resourceType` + `date` (compound)

---

### servers
**Purpose:** Server metadata (/api/server/info)

**Key Fields:**
```csharp
_id                 // string - Server ID
version             // string - Server version
welcomeText         // string - Welcome message
// ... other server config
```

**Indexes:**
- `_id` (primary key)

---

## Repository Patterns

### Basic Repository
```csharp
public class UserRepository(IMongoDatabase database)
{
    private readonly IMongoCollection<UserDocument> _users
        = database.GetCollection<UserDocument>("users");

    public async Task<UserDocument?> GetByIdAsync(string id)
        => await _users.Find(u => u.Id == id).FirstOrDefaultAsync();

    public async Task<List<UserDocument>> GetAllAsync()
        => await _users.Find(_ => true).ToListAsync();
}
```

### With Projection
```csharp
public async Task<UserDocument?> GetByIdProjectedAsync(string id)
{
    var projection = Builders<UserDocument>.Projection
        .Include(u => u.Username)
        .Include(u => u.Gcl)
        .Exclude(u => u.Id);

    var result = await _users.Find(u => u.Id == id)
        .Project<UserDocument>(projection)
        .FirstOrDefaultAsync();

    return result;
}
```

### With Complex Filters
```csharp
public async Task<List<RoomObjectDocument>> GetCreepsInRoomAsync(string roomName)
{
    var filter = Builders<RoomObjectDocument>.Filter.And(
        Builders<RoomObjectDocument>.Filter.Eq(o => o.Room, roomName),
        Builders<RoomObjectDocument>.Filter.Eq(o => o.Type, "creep")
    );

    return await _objects.Find(filter).ToListAsync();
}
```

---

## Bulk Writer Pattern

**Critical:** Always use `IBulkWriterFactory` for mutations to batch operations.

### Usage Example
```csharp
public class ProcessorHandler(IBulkWriterFactory bulkWriterFactory)
{
    public async Task ProcessAsync(Intent intent)
    {
        var bulkWriter = bulkWriterFactory.Create();

        // Queue multiple operations
        bulkWriter.Objects.Update(
            creepId,
            Builders<RoomObjectDocument>.Update.Set(o => o.Energy, 100)
        );

        bulkWriter.Objects.Update(
            sourceId,
            Builders<RoomObjectDocument>.Update.Inc(o => o.Energy, -10)
        );

        // Flush all operations in one batch
        await bulkWriter.FlushAsync();
    }
}
```

### Available Bulk Writers
```csharp
bulkWriter.Objects      // rooms.objects operations
bulkWriter.Users        // users operations
bulkWriter.Rooms        // rooms operations
bulkWriter.Market       // market.orders operations
// ... etc
```

**Details:** See [docs/driver/d5-bulk-writers.md](../driver/d5-bulk-writers.md)

---

## Index Strategies

### Query Performance
- Always ensure filters use indexed fields
- Use compound indexes for multi-field queries
- Use projection to reduce network overhead

### Index Verification
```csharp
// Check existing indexes
var indexes = await collection.Indexes.ListAsync();
var indexList = await indexes.ToListAsync();
```

### Creating Indexes
```csharp
// Create single field index
var indexKeys = Builders<UserDocument>.IndexKeys.Ascending(u => u.Username);
await collection.Indexes.CreateOneAsync(new CreateIndexModel<UserDocument>(indexKeys));

// Create compound index
var compoundKeys = Builders<RoomObjectDocument>.IndexKeys
    .Ascending(o => o.Room)
    .Ascending(o => o.Type);
await collection.Indexes.CreateOneAsync(new CreateIndexModel<RoomObjectDocument>(compoundKeys));
```

---

## Schema Evolution

### Adding New Fields
**Safe:** Add optional fields to documents
```csharp
public class UserDocument
{
    public string Id { get; set; }
    public string Username { get; set; }
    public int? NewField { get; set; }  // Optional - won't break existing docs
}
```

### Removing Fields
**Caution:** Ensure no code relies on field before removing
```csharp
// Step 1: Make field optional
public int? OldField { get; set; }

// Step 2: Deploy code that doesn't use OldField

// Step 3: Remove field from POCO
// (Mongo will ignore extra fields in documents)
```

### Changing Field Types
**Unsafe:** Requires migration script
- Write migration script to transform all documents
- Run migration before deploying new code
- Consider backward compatibility during transition

---

## Testing Patterns

### Testcontainers Integration
```csharp
public class MongoIntegrationTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder()
        .WithImage("mongo:7")
        .Build();

    public async Task InitializeAsync()
    {
        await _mongoContainer.StartAsync();
        var connectionString = _mongoContainer.GetConnectionString();
        // Initialize database and seed data
    }

    [Fact]
    public async Task Test_UserRepository()
    {
        var database = _client.GetDatabase("screeps");
        var repository = new UserRepository(database);

        var user = await repository.GetByIdAsync("test-user");
        Assert.NotNull(user);
    }

    public async Task DisposeAsync()
    {
        await _mongoContainer.DisposeAsync();
    }
}
```

---

## Performance Tips

1. **Use projection** - Only retrieve needed fields
2. **Use indexes** - Ensure all queries hit indexes
3. **Batch operations** - Use bulk writers for mutations
4. **Limit results** - Use `.Limit()` for large result sets
5. **Connection pooling** - Let driver manage connections
6. **Avoid N+1 queries** - Fetch related data in single query

---

## Common Pitfalls

### ❌ Using BsonDocument
```csharp
// NEVER DO THIS
var collection = database.GetCollection<BsonDocument>("users");
var user = await collection.Find(new BsonDocument("_id", userId)).FirstOrDefaultAsync();
var username = user["username"].AsString;  // Brittle, no type safety
```

### ✅ Use Typed POCOs
```csharp
// ALWAYS DO THIS
var collection = database.GetCollection<UserDocument>("users");
var user = await collection.Find(u => u.Id == userId).FirstOrDefaultAsync();
var username = user?.Username;  // Type-safe, refactorable
```

### ❌ Direct Repository Mutations
```csharp
// DON'T DO THIS
await repository.UpdateAsync(id, update);  // Bypasses bulk batching
```

### ✅ Use Bulk Writers
```csharp
// DO THIS
var bulkWriter = bulkWriterFactory.Create();
bulkWriter.Objects.Update(id, update);
await bulkWriter.FlushAsync();
```

---

## Cross-References

- **Storage overview:** [overview.md](overview.md)
- **Redis patterns:** [redis.md](redis.md)
- **Seeding:** [seeding.md](seeding.md)
- **Bulk writers:** [docs/driver/d5-bulk-writers.md](../driver/d5-bulk-writers.md)
- **Storage adapters:** [docs/driver/d2-storage-adapters.md](../driver/d2-storage-adapters.md)
