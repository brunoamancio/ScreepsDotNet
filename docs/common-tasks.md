# Common Development Tasks

This guide provides step-by-step instructions for common development tasks in ScreepsDotNet.

## Add a New HTTP Endpoint

### 1. Add Route Handler
**Location:** `src/ScreepsDotNet.Backend.Http/Endpoints/<Area>Endpoints.cs`

```csharp
// Example
app.MapPost("/api/user/set-preference", async (
    [FromBody] SetPreferenceRequest request,
    [FromServices] IUserPreferenceService service,
    HttpContext context) =>
{
    var userId = context.GetUserId();  // From auth token
    await service.SetPreferenceAsync(userId, request.Key, request.Value);
    return Results.Ok(new { ok = 1 });
});
```

### 2. Add Service Logic
**Location:** `src/ScreepsDotNet.Backend.Core/Services/`

```csharp
public class UserPreferenceService(IUserRepository userRepository) : IUserPreferenceService
{
    public async Task SetPreferenceAsync(string userId, string key, string value)
    {
        var update = Builders<UserDocument>.Update.Set($"prefs.{key}", value);
        await userRepository.UpdateAsync(userId, update);
    }
}
```

### 3. Add Repository Method (if needed)
**Location:** `src/ScreepsDotNet.Storage.MongoRedis/Repositories/`

### 4. Create .http File for Manual Testing
**Location:** `src/ScreepsDotNet.Backend.Http/UserEndpoints.http`

```http
### Set Preference
POST http://localhost:5210/api/user/set-preference
Content-Type: application/json
X-Token: {{ScreepsDotNet_User_Token}}

{
  "key": "theme",
  "value": "dark"
}
```

### 5. Add Integration Test
**Location:** `src/ScreepsDotNet.Backend.Tests/Endpoints/UserEndpointTests.cs`

```csharp
[Fact]
public async Task SetPreference_ValidRequest_ReturnsOk()
{
    var client = _harness.CreateClient();
    var response = await client.PostAsJsonAsync("/api/user/set-preference",
        new { key = "theme", value = "dark" });
    response.EnsureSuccessStatusCode();
}
```

### 6. Update Documentation
- `docs/backend/http-api.md` - Route reference
- `docs/backend/overview.md` - Feature coverage list

## Add a New CLI Command

### 1. Add Command Class
**Location:** `src/ScreepsDotNet.Backend.Cli/Commands/<Area>/<CommandName>Command.cs`

```csharp
using Spectre.Console.Cli;

public class ListUsersCommand(IUserRepository userRepository)
    : AsyncCommand<ListUsersCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--format")]
        [DefaultValue("table")]
        public string Format { get; set; } = "table";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var users = await userRepository.GetAllAsync();

        // Format output (table/markdown/json)
        FormatHelper.Print(users, settings.Format);

        return 0;
    }
}
```

### 2. Register in CLI App
**Location:** `src/ScreepsDotNet.Backend.Cli/Program.cs`

```csharp
app.Configure(config =>
{
    config.AddBranch("user", user =>
    {
        user.AddCommand<ListUsersCommand>("list");
    });
});
```

### 3. Update Documentation
- `docs/backend/cli.md` - Command reference

### 4. Test
```bash
dotnet run --project src/ScreepsDotNet.Backend.Cli/ScreepsDotNet.Backend.Cli.csproj -- user list --format table
```

## Add a New Storage Collection

### 1. Define POCO
**Location:** `src/ScreepsDotNet.Storage.MongoRedis/Repositories/Documents/`

```csharp
public class AchievementDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("unlocked")]
    public DateTime Unlocked { get; set; }
}
```

### 2. Add Repository Interface + Implementation
**Location:** `src/ScreepsDotNet.Storage.MongoRedis/Repositories/`

```csharp
public interface IAchievementRepository
{
    Task<List<AchievementDocument>> GetByUserIdAsync(string userId);
    Task InsertAsync(AchievementDocument achievement);
}

public class MongoAchievementRepository(IMongoDatabase database) : IAchievementRepository
{
    private readonly IMongoCollection<AchievementDocument> _collection
        = database.GetCollection<AchievementDocument>("achievements");

    public async Task<List<AchievementDocument>> GetByUserIdAsync(string userId)
        => await _collection.Find(a => a.UserId == userId).ToListAsync();

    public async Task InsertAsync(AchievementDocument achievement)
        => await _collection.InsertOneAsync(achievement);
}
```

### 3. Register in DI
**Location:** `src/ScreepsDotNet.Storage.MongoRedis/ServiceCollectionExtensions.cs`

```csharp
services.AddSingleton<IAchievementRepository, MongoAchievementRepository>();
```

### 4. Add Seed Data
**Location:** `src/docker/mongo-init/seed-users.js` (or new file)

```javascript
db.achievements.insertMany([
    {
        userId: testUserId,
        type: "first_spawn",
        unlocked: new Date()
    }
]);
```

### 5. Add to Test Fixtures
**Location:** `src/ScreepsDotNet.Backend.Core/Seeding/SeedDataDefaults.cs`

```csharp
public static class AchievementDefaults
{
    public static readonly AchievementDocument FirstSpawn = new()
    {
        UserId = UserDefaults.TestUser.Id,
        Type = "first_spawn",
        Unlocked = DateTime.UtcNow
    };
}
```

## Update Seed Data

### 1. Modify Seed Scripts
**Location:** `src/docker/mongo-init/*.js`

### 2. Update SeedDataDefaults
**Location:** `src/ScreepsDotNet.Backend.Core/Seeding/SeedDataDefaults.cs`

### 3. Reset Volumes
```bash
docker volume rm screepsdotnet_mongo-data
docker compose -f src/docker-compose.yml up -d mongo
```

### 4. Verify
```bash
docker compose -f src/docker-compose.yml logs -f mongo
dotnet test --filter "Category=Integration"
```

## Troubleshoot Common Issues

### Issue: DLL is Locked During Build
```bash
# Stop all dotnet run instances
pkill -f "dotnet run"

# Then rebuild
dotnet build src/ScreepsDotNet.slnx
```

### Issue: Tests Fail with "Cannot Connect to MongoDB"
```bash
# Ensure Docker is running
docker ps

# Testcontainers should auto-start containers
# Check test output for container startup logs

# If still failing, try running Docker compose first
docker compose -f src/docker-compose.yml up -d
```

### Issue: Seed Data Not Loading
```bash
# Check if volume already exists
docker volume ls | grep screepsdotnet

# Remove and recreate
docker volume rm screepsdotnet_mongo-data
docker compose -f src/docker-compose.yml up -d mongo

# Watch logs
docker compose -f src/docker-compose.yml logs -f mongo
# Should see: "seed-users.js complete"
```

### Issue: Git Shows ScreepsNodeJs/ Changes
```bash
# You're in the wrong directory
cd ..  # Go to repo root
cd ScreepsDotNet  # Enter solution directory
git status  # Should NOT show ScreepsNodeJs/
```

### Issue: Implicit Usings Not Working
```bash
# Rebuild solution to refresh SDK settings
dotnet clean src/ScreepsDotNet.slnx
dotnet build src/ScreepsDotNet.slnx

# Check Directory.Build.props for ImplicitUsings setting
```
