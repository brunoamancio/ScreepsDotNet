# Redis Patterns & Keys

This document details Redis-specific patterns, key structures, and best practices used in ScreepsDotNet.

---

## Redis Keys Reference

### Driver Queues

#### roomsQueue
**Purpose:** Room processing queue for processor loop
**Type:** List (LPUSH/RPOP)
**Value:** Room names (strings)

**Usage:**
```csharp
// Enqueue room for processing
await database.ListLeftPushAsync("roomsQueue", "W1N1");

// Dequeue room for processing
var roomName = await database.ListRightPopAsync("roomsQueue");
```

**Consumer:** `ProcessorLoopWorker` (driver)

---

#### runtimeQueue
**Purpose:** User code execution queue for runner loop
**Type:** List (LPUSH/RPOP)
**Value:** User IDs (strings)

**Usage:**
```csharp
// Enqueue user for code execution
await database.ListLeftPushAsync("runtimeQueue", "user-123");

// Dequeue user for execution
var userId = await database.ListRightPopAsync("runtimeQueue");
```

**Consumer:** `RunnerLoopWorker` (driver)

---

### Environment Keys

#### gameTime
**Purpose:** Current game tick
**Type:** String (stored as integer)
**Value:** Game tick number

**Usage:**
```csharp
// Get current game time
var gameTime = await database.StringGetAsync("gameTime");
var tick = int.Parse(gameTime!);

// Increment game time
await database.StringIncrementAsync("gameTime");
```

**Consumer:** Environment service, all loops

---

### Future Keys (Planned)

```
config:{key}            # Configuration values
env:{key}               # Environment variables
cache:user:{userId}     # User data cache
cache:room:{roomName}   # Room data cache
lock:{resource}         # Distributed locks
```

---

## Connection Management

### Connection Multiplexer
**Pattern:** Single `IConnectionMultiplexer` instance shared across application

**Configuration:**
```csharp
services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["Redis:ConnectionString"];
    return ConnectionMultiplexer.Connect(connectionString);
});
```

**Usage:**
```csharp
public class QueueService(IConnectionMultiplexer redis)
{
    private readonly IDatabase _database = redis.GetDatabase();

    public async Task EnqueueAsync(string queue, string value)
        => await _database.ListLeftPushAsync(queue, value);
}
```

---

## Queue Patterns

### FIFO Queue (List-based)
**Use case:** Room/user processing queues

**Pattern:**
- Producer: `LPUSH` (left push)
- Consumer: `RPOP` (right pop)
- Result: First-in, first-out order

```csharp
// Producer
await database.ListLeftPushAsync("queue", value);

// Consumer
var value = await database.ListRightPopAsync("queue");
if (!value.HasValue)
{
    // Queue is empty
}
```

### Blocking Pop (BRPOP)
**Use case:** Worker loops waiting for work

```csharp
// Block until item available (or timeout)
var result = await database.ListRightPopAsync("queue", TimeSpan.FromSeconds(5));
if (result.HasValue)
{
    Process(result.ToString());
}
```

**Note:** StackExchange.Redis doesn't have native async BRPOP. Use polling with short timeout instead.

---

## Caching Patterns

### Cache-Aside Pattern
**Use case:** User/room data caching

```csharp
public async Task<User?> GetUserAsync(string userId)
{
    var cacheKey = $"cache:user:{userId}";

    // Try cache first
    var cached = await _database.StringGetAsync(cacheKey);
    if (cached.HasValue)
    {
        return JsonSerializer.Deserialize<User>(cached!);
    }

    // Cache miss - fetch from MongoDB
    var user = await _mongoRepository.GetByIdAsync(userId);
    if (user is not null)
    {
        // Store in cache with TTL
        var json = JsonSerializer.Serialize(user);
        await _database.StringSetAsync(cacheKey, json, TimeSpan.FromMinutes(10));
    }

    return user;
}
```

### Write-Through Pattern
**Use case:** Critical data that must stay in sync

```csharp
public async Task UpdateUserAsync(User user)
{
    // Update MongoDB first
    await _mongoRepository.UpdateAsync(user);

    // Update cache
    var cacheKey = $"cache:user:{user.Id}";
    var json = JsonSerializer.Serialize(user);
    await _database.StringSetAsync(cacheKey, json, TimeSpan.FromMinutes(10));
}
```

### Cache Invalidation
```csharp
public async Task InvalidateUserCacheAsync(string userId)
{
    var cacheKey = $"cache:user:{userId}";
    await _database.KeyDeleteAsync(cacheKey);
}
```

---

## TTL (Time-To-Live) Patterns

### Set with Expiration
```csharp
// Set key with 10-minute expiration
await database.StringSetAsync(key, value, TimeSpan.FromMinutes(10));
```

### Update Expiration
```csharp
// Reset expiration on existing key
await database.KeyExpireAsync(key, TimeSpan.FromMinutes(10));
```

### Get TTL
```csharp
// Check remaining TTL
var ttl = await database.KeyTimeToLiveAsync(key);
if (ttl.HasValue)
{
    Console.WriteLine($"Key expires in {ttl.Value.TotalSeconds} seconds");
}
```

---

## Pub/Sub Patterns

### Publisher
```csharp
var subscriber = redis.GetSubscriber();
await subscriber.PublishAsync("channel:events", "message");
```

### Subscriber
```csharp
var subscriber = redis.GetSubscriber();
await subscriber.SubscribeAsync("channel:events", (channel, message) =>
{
    Console.WriteLine($"Received: {message}");
});
```

**Note:** Pub/sub connections should be separate from command connections for best performance.

---

## Distributed Lock Pattern

### Acquire Lock
```csharp
public async Task<bool> TryAcquireLockAsync(string resource, TimeSpan expiration)
{
    var lockKey = $"lock:{resource}";
    var lockValue = Guid.NewGuid().ToString();

    // NX = only set if not exists
    var acquired = await _database.StringSetAsync(
        lockKey,
        lockValue,
        expiration,
        When.NotExists
    );

    return acquired;
}
```

### Release Lock
```csharp
public async Task ReleaseLockAsync(string resource)
{
    var lockKey = $"lock:{resource}";
    await _database.KeyDeleteAsync(lockKey);
}
```

**Use case:** Coordinating exclusive access to shared resources across multiple processes.

---

## Serialization

### JSON Serialization (Recommended)
```csharp
// Serialize
var json = JsonSerializer.Serialize(user);
await database.StringSetAsync(key, json);

// Deserialize
var json = await database.StringGetAsync(key);
var user = JsonSerializer.Deserialize<User>(json!);
```

### MessagePack (High Performance)
```csharp
// Serialize
var bytes = MessagePackSerializer.Serialize(user);
await database.StringSetAsync(key, bytes);

// Deserialize
var bytes = await database.StringGetAsync(key);
var user = MessagePackSerializer.Deserialize<User>(bytes!);
```

**Recommendation:** Use JSON for human-readable debugging, MessagePack for performance-critical paths.

---

## Performance Tips

1. **Connection multiplexing** - Single connection handles all operations
2. **Pipeline commands** - Batch operations with `IBatch`
3. **Avoid KEYS command** - Use SCAN instead for production
4. **Use TTL** - Set expiration on cache keys to prevent memory bloat
5. **Monitor memory** - Track Redis memory usage with INFO command
6. **Separate pub/sub** - Use dedicated connection for pub/sub

### Pipelining Example
```csharp
var batch = database.CreateBatch();

var task1 = batch.StringSetAsync("key1", "value1");
var task2 = batch.StringSetAsync("key2", "value2");
var task3 = batch.StringSetAsync("key3", "value3");

batch.Execute();

await Task.WhenAll(task1, task2, task3);
```

---

## Testing Patterns

### Testcontainers Integration
```csharp
public class RedisIntegrationTests : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7")
        .Build();

    private IConnectionMultiplexer _redis = null!;

    public async Task InitializeAsync()
    {
        await _redisContainer.StartAsync();
        var connectionString = _redisContainer.GetConnectionString();
        _redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
    }

    [Fact]
    public async Task Test_QueueOperations()
    {
        var database = _redis.GetDatabase();

        await database.ListLeftPushAsync("test-queue", "item1");
        var item = await database.ListRightPopAsync("test-queue");

        Assert.Equal("item1", item.ToString());
    }

    public async Task DisposeAsync()
    {
        _redis?.Dispose();
        await _redisContainer.DisposeAsync();
    }
}
```

---

## Monitoring

### Get Redis Info
```csharp
var server = redis.GetServer(redis.GetEndPoints()[0]);
var info = await server.InfoAsync();

Console.WriteLine($"Used Memory: {info[0]["used_memory"]}");
Console.WriteLine($"Connected Clients: {info[0]["connected_clients"]}");
```

### Monitor Commands
```csharp
// Subscribe to all commands (development only!)
var subscriber = redis.GetSubscriber();
await subscriber.SubscribeAsync("__redis__:*", (channel, message) =>
{
    Console.WriteLine($"{channel}: {message}");
});
```

**Warning:** MONITOR command is expensive - use only in development.

---

## Common Pitfalls

### ❌ Creating Multiple Connections
```csharp
// DON'T DO THIS
public class Service1
{
    private readonly IConnectionMultiplexer _redis
        = ConnectionMultiplexer.Connect("localhost:6379");  // ❌ New connection per service
}
```

### ✅ Inject Shared Connection
```csharp
// DO THIS
public class Service1(IConnectionMultiplexer redis)
{
    private readonly IDatabase _database = redis.GetDatabase();
}
```

### ❌ Synchronous Operations
```csharp
// DON'T DO THIS
var value = database.StringGet(key);  // ❌ Blocking call
```

### ✅ Async Operations
```csharp
// DO THIS
var value = await database.StringGetAsync(key);  // ✅ Non-blocking
```

### ❌ No TTL on Cache Keys
```csharp
// DON'T DO THIS
await database.StringSetAsync(cacheKey, value);  // ❌ No expiration = memory leak
```

### ✅ Always Set TTL
```csharp
// DO THIS
await database.StringSetAsync(cacheKey, value, TimeSpan.FromMinutes(10));  // ✅ Auto-expire
```

---

## Key Naming Conventions

**Pattern:** `{namespace}:{entity}:{id}`

Examples:
```
cache:user:123456
cache:room:W1N1
lock:room:W1N1
config:tickRate
env:maintenanceMode
```

**Benefits:**
- Easy to understand
- Easy to bulk delete (KEYS/SCAN with pattern)
- Namespace isolation

---

## Cross-References

- **Storage overview:** [overview.md](overview.md)
- **MongoDB patterns:** [mongodb.md](mongodb.md)
- **Queue implementation:** [docs/driver/d4-queue-scheduler.md](../driver/d4-queue-scheduler.md)
- **Driver usage:** [docs/driver/d2-storage-adapters.md](../driver/d2-storage-adapters.md)
