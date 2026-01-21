# Seed Data & Test Fixtures

This document explains the seed data system used for local development, integration tests, and test fixtures.

---

## Seed Data Overview

ScreepsDotNet uses MongoDB seed scripts that run automatically when the `mongo-data` Docker volume is empty. This ensures consistent test data across environments.

**Seed Location:** `src/docker/mongo-init/`
**Test Fixtures:** `src/ScreepsDotNet.Backend.Core/Seeding/SeedDataDefaults.cs`

---

## Seed Scripts

### seed-server-data.js
**Purpose:** Server metadata for `/api/server/info`

**Collections Seeded:**
- `servers` - Server version, welcome text, configuration

**Example Document:**
```javascript
{
  _id: "main",
  version: "1.0.0",
  welcomeText: "Welcome to ScreepsDotNet!",
  features: {
    auth: true,
    market: true,
    powerCreeps: true
  }
}
```

---

### seed-users.js
**Purpose:** Test users, rooms, spawns, power creeps, inbox threads

**Collections Seeded:**
- `users` - test-user, ally-user accounts
- `users.code` - Default code branches
- `users.memory` - Initial memory
- `users.power_creeps` - Power creep definitions
- `rooms` - Room metadata (W1N1, W1N2, etc.)
- `rooms.objects` - Spawns, controllers, sources, minerals
- `rooms.terrain` - Terrain data
- `users.notifications` - Sample inbox threads

**Test User IDs:**
```javascript
const TEST_USER_ID = "test-user";
const ALLY_USER_ID = "ally-user";
```

**Test Rooms:**
```javascript
const TEST_ROOMS = ["W1N1", "W1N2", "W2N1"];
```

**Example User Document:**
```javascript
{
  _id: "test-user",
  username: "TestUser",
  password: "$2b$10$...",  // bcrypt hash of "password"
  cpu: 100,
  cpuAvailable: 10000,
  gcl: 1,
  gclPoints: 0,
  power: 0,
  badge: {
    type: 21,
    color1: "#FF0000",
    color2: "#00FF00",
    color3: "#0000FF",
    flip: false,
    param: 0
  }
}
```

---

## SeedDataDefaults.cs

**Purpose:** Shared constants for seed scripts and C# tests

**Location:** `src/ScreepsDotNet.Backend.Core/Seeding/SeedDataDefaults.cs`

**Usage:**
```csharp
public static class SeedDataDefaults
{
    public const string TestUserId = "test-user";
    public const string AllyUserId = "ally-user";

    public const string TestRoomW1N1 = "W1N1";
    public const string TestRoomW1N2 = "W1N2";

    public const string TestSpawnW1N1 = "spawn-w1n1";

    public const string TestPassword = "password";
}
```

**Why:** Keep seed scripts and C# tests in sync by sharing constants.

---

## Seed Workflow

### Docker Compose Seed Flow
1. `docker compose up -d` starts MongoDB
2. MongoDB checks if `mongo-data` volume is empty
3. If empty, runs all `.js` files in `/docker-entrypoint-initdb.d/`
4. Seed scripts create collections and insert documents
5. Application starts with pre-seeded data

**Verification:**
```bash
# Check if seeds ran
docker compose -f src/docker-compose.yml logs mongo

# Expected output:
# "executed seed-server-data.js"
# "executed seed-users.js"
```

---

## Reset Workflows

### Full Reset (Mongo + Redis)
```bash
docker compose -f src/docker-compose.yml down -v
docker compose -f src/docker-compose.yml up -d
```

**Effect:** Deletes all data, re-runs seed scripts

---

### Mongo Only (Faster)
```bash
docker volume rm screepsdotnet_mongo-data
docker compose -f src/docker-compose.yml up -d mongo
```

**Effect:** Resets Mongo only, Redis keeps running

---

### Verify Seeds Ran
```bash
# Tail logs until seeds complete
docker compose -f src/docker-compose.yml logs -f mongo

# Check specific collection
docker exec -it screepsdotnet-mongo-1 mongosh screeps --eval "db.users.countDocuments()"
# Expected: 2 (test-user + ally-user)
```

---

## Test Fixtures (Testcontainers)

### Pattern
Integration tests use Testcontainers to spin up MongoDB/Redis with custom seed data.

**Example:**
```csharp
public class UserEndpointTests : IClassFixture<IntegrationTestHarness>
{
    private readonly IntegrationTestHarness _harness;

    public UserEndpointTests(IntegrationTestHarness harness)
    {
        _harness = harness;
    }

    [Fact]
    public async Task GetUser_ReturnsTestUser()
    {
        // Arrange - harness has seeded test-user
        var client = _harness.CreateClient();

        // Act
        var response = await client.GetAsync("/api/user/profile?id=test-user");

        // Assert
        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<User>();
        Assert.Equal(SeedDataDefaults.TestUserId, user?.Id);
    }
}
```

---

### IntegrationTestHarness Pattern
**Purpose:** Shared test harness for integration tests

**Features:**
- Spins up MongoDB + Redis containers
- Seeds test data via SeedDataDefaults
- Provides HTTP client factory
- Cleans up after tests

**Example Implementation:**
```csharp
public class IntegrationTestHarness : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer;
    private readonly RedisContainer _redisContainer;
    private WebApplicationFactory<Program> _factory = null!;

    public IntegrationTestHarness()
    {
        _mongoContainer = new MongoDbBuilder()
            .WithImage("mongo:7")
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _mongoContainer.StartAsync();
        await _redisContainer.StartAsync();

        var mongoConnectionString = _mongoContainer.GetConnectionString();
        var redisConnectionString = _redisContainer.GetConnectionString();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["MongoDB:ConnectionString"] = mongoConnectionString,
                        ["MongoDB:DatabaseName"] = "screeps",
                        ["Redis:ConnectionString"] = redisConnectionString
                    });
                });
            });

        await SeedTestDataAsync();
    }

    private async Task SeedTestDataAsync()
    {
        var mongoClient = new MongoClient(_mongoContainer.GetConnectionString());
        var database = mongoClient.GetDatabase("screeps");

        // Seed test user
        var users = database.GetCollection<UserDocument>("users");
        await users.InsertOneAsync(new UserDocument
        {
            Id = SeedDataDefaults.TestUserId,
            Username = "TestUser",
            Cpu = 100,
            CpuAvailable = 10000,
            Gcl = 1,
            Power = 0
        });

        // Seed more data...
    }

    public HttpClient CreateClient()
        => _factory.CreateClient();

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await _mongoContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }
}
```

---

## Seed Data Guidelines

### ✅ Do
- Keep seed scripts simple and readable
- Use consistent IDs (test-user, W1N1, etc.)
- Document what each seed script creates
- Keep SeedDataDefaults.cs in sync with seed scripts
- Use Testcontainers for integration tests

### ❌ Don't
- Rely on local Docker state in tests
- Create seed data that depends on timing
- Use production data in seed scripts
- Hardcode credentials (except test data)
- Create circular dependencies between collections

---

## Adding New Seed Data

### 1. Update Seed Scripts
```javascript
// src/docker/mongo-init/seed-users.js

// Add new test user
db.users.insertOne({
  _id: "new-test-user",
  username: "NewTestUser",
  // ... other fields
});

// Add to test rooms
db.rooms.insertOne({
  _id: "W3N3",
  active: true,
  status: "normal"
});
```

### 2. Update SeedDataDefaults.cs
```csharp
// src/ScreepsDotNet.Backend.Core/Seeding/SeedDataDefaults.cs

public static class SeedDataDefaults
{
    public const string TestUserId = "test-user";
    public const string NewTestUserId = "new-test-user";  // Add constant

    public const string TestRoomW3N3 = "W3N3";  // Add constant
}
```

### 3. Reset Data
```bash
# Reset to pick up new seeds
docker compose -f src/docker-compose.yml down -v
docker compose -f src/docker-compose.yml up -d
```

### 4. Update Tests
```csharp
[Fact]
public async Task GetNewTestUser_ReturnsUser()
{
    var response = await client.GetAsync($"/api/user/profile?id={SeedDataDefaults.NewTestUserId}");
    response.EnsureSuccessStatusCode();
}
```

---

## Seed Data Validation

### Manual Verification
```bash
# Connect to MongoDB
docker exec -it screepsdotnet-mongo-1 mongosh screeps

# Check users
db.users.find().pretty()

# Check rooms
db.rooms.find().pretty()

# Check spawns
db["rooms.objects"].find({ type: "spawn" }).pretty()
```

### Automated Verification (Tests)
```csharp
[Fact]
public async Task SeedData_HasTestUser()
{
    var repository = _harness.GetService<IUserRepository>();
    var user = await repository.GetByIdAsync(SeedDataDefaults.TestUserId);

    Assert.NotNull(user);
    Assert.Equal("TestUser", user.Username);
}
```

---

## Common Issues

### Seeds Not Running
**Symptom:** Empty database after docker compose up

**Causes:**
1. Existing `mongo-data` volume (seeds only run when empty)
2. Seed scripts have syntax errors
3. Mongo container failed to start

**Fix:**
```bash
# Delete volume and restart
docker compose -f src/docker-compose.yml down -v
docker compose -f src/docker-compose.yml up -d

# Check logs for errors
docker compose -f src/docker-compose.yml logs mongo
```

### Seeds Run Multiple Times
**Symptom:** Duplicate data in database

**Causes:**
1. Seed scripts don't check for existing data
2. Volume not properly persisted

**Fix:**
```javascript
// Add existence check in seed script
if (db.users.countDocuments({ _id: "test-user" }) === 0) {
  db.users.insertOne({ _id: "test-user", ... });
}
```

### Test Fixtures Out of Sync
**Symptom:** Tests fail with "user not found"

**Causes:**
1. SeedDataDefaults.cs constants don't match seed scripts
2. Test harness not seeding data correctly

**Fix:**
- Review SeedDataDefaults.cs vs seed scripts
- Update harness seed logic
- Verify Testcontainers are starting correctly

---

## Performance Tips

1. **Minimize seed data** - Only create what tests need
2. **Use bulk inserts** - `insertMany()` instead of multiple `insertOne()`
3. **Skip indexes** - Seed scripts don't need to create indexes (Mongo does it)
4. **Parallel tests** - Use test isolation to run tests in parallel

---

## Cross-References

- **Storage overview:** [overview.md](overview.md)
- **MongoDB patterns:** [mongodb.md](mongodb.md)
- **Getting started:** [docs/getting-started.md](../getting-started.md)
- **Testing patterns:** See `ScreepsDotNet.Backend.Http.Tests` for examples
