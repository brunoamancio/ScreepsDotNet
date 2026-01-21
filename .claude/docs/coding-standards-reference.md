# Coding Standards Reference

Condensed examples for the 21 most critical coding standards. For full rule documentation, see `src/.editorconfig` and `src/ScreepsDotNet.slnx.DotSettings`.

## 1. Variable Declarations (IDE0007)

**✅ Good:**
```csharp
var user = await _userRepository.GetByIdAsync(userId);
var count = 0;
```

**❌ Bad:**
```csharp
User user = await _userRepository.GetByIdAsync(userId);
int count = 0;
```

## 2. Collection Expressions (IDE0028)

**✅ Good:**
```csharp
var items = [];
var names = ["alice", "bob"];
```

**❌ Bad:**
```csharp
var items = new List<string>();
var names = new List<string> { "alice", "bob" };
```

## 3. Primary Constructors

**✅ Good:**
```csharp
public class UserService(IUserRepository userRepository)
{
    public async Task<User?> GetUserAsync(string id)
        => await userRepository.GetByIdAsync(id);
}
```

**❌ Bad:**
```csharp
public class UserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }
}
```

## 4. File-Scoped Namespaces (IDE0161)

**✅ Good:**
```csharp
namespace ScreepsDotNet.Backend.Http;

public class UserService { }
```

**❌ Bad:**
```csharp
namespace ScreepsDotNet.Backend.Http
{
    public class UserService { }
}
```

## 5. Async Method Naming

**✅ Good:**
```csharp
public async Task<User> GetUserAsync(string id)
    => await _repository.GetByIdAsync(id);
```

**❌ Bad:**
```csharp
public async Task<User> GetUser(string id)  // Missing "Async" suffix
    => await _repository.GetByIdAsync(id);
```

## 6. Pattern Matching (IDE0019, IDE0020)

**✅ Good:**
```csharp
if (obj is User user)
    return user.Name;

if (value is not null)
    Process(value);
```

**❌ Bad:**
```csharp
var user = obj as User;
if (user != null)
    return user.Name;

if (!(value is null))
    Process(value);
```

## 7. Ternary Return Statements

**✅ Good:**
```csharp
var result = success ? Results.Ok() : Results.BadRequest();
return result;

var intent = directions.Count == 0 ? null : new SetSpawnDirectionsIntent(directions);
return intent;
```

**❌ Bad:**
```csharp
return success ? Results.Ok() : Results.BadRequest();  // Direct return
return directions.Count == 0 ? null : new SetSpawnDirectionsIntent(directions);  // Direct return
```

## 8. Positive Conditions in Ternaries

**✅ Good:**
```csharp
var result = success ? "Success" : "Failed";
var interval = string.IsNullOrWhiteSpace(userId) ? null : ownedInterval;
```

**❌ Bad:**
```csharp
var result = !success ? "Failed" : "Success";  // Negated condition
var interval = !string.IsNullOrWhiteSpace(userId) ? ownedInterval : null;  // Negated condition
```

## 9. Expression-Bodied Members (IDE0022)

**✅ Good:**
```csharp
public string GetUserName()
    => _user.Name;  // => on new line

public string Name => _name;
```

**❌ Bad:**
```csharp
public string GetUserName()
{
    return _user.Name;
}

public string GetUserName() => _user.Name;  // => on same line
```

## 10. Trailing Commas

**Rule:** In top-level arrays and enums, use trailing commas between items. In dictionary/object initializers, NO trailing commas on last item or closing braces.

**✅ Good:**
```csharp
// Top-level array: trailing commas between items
var items = new[]
{
    "a",
    "b",
    "c",  // ✅ Trailing comma
};

// Enum: trailing comma after last value
public enum Status
{
    Active,
    Inactive,
    Pending,  // ✅ Trailing comma
}

// Dictionary initializers: NO trailing comma on last item
var metadata = new Dictionary<string, object?>
{
    [Market] = new Dictionary<string, object?>
    {
        [Order] = new Dictionary<string, object?>
        {
            [Type] = order.Type,
            [Price] = order.Price,
            [Amount] = order.Amount  // ✅ No trailing comma (last item)
        }  // ✅ No trailing comma on closing brace
    }  // ✅ No trailing comma on closing brace
};

// Single-line: no trailing comma
var single = new[] { "a", "b", "c" };
```

**❌ Bad:**
```csharp
// Dictionary initializers: trailing commas on last item or closing braces
var metadata = new Dictionary<string, object?>
{
    [Market] = new Dictionary<string, object?>
    {
        [Order] = new Dictionary<string, object?>
        {
            [Type] = order.Type,
            [Amount] = order.Amount,  // ❌ Trailing comma on last item
        },  // ❌ Trailing comma on closing brace
    },  // ❌ Trailing comma on closing brace
};

// Single-line: unnecessary trailing comma
var single = new[] { "a", "b", "c", };  // ❌
```

## 11. Repository Patterns

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
public class UserRepository(IMongoDatabase database)
{
    private readonly IMongoCollection<BsonDocument> _users = database.GetCollection<BsonDocument>("users");  // BsonDocument not allowed

    public async Task<BsonDocument?> GetByIdAsync(string id)
        => await _users.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefaultAsync();
}
```

## 12. Testing Patterns

**✅ Good:**
```csharp
public class UserEndpointTests(IntegrationTestHarness harness)
    : IClassFixture<IntegrationTestHarness>
{
    [Fact]
    public async Task GetUser_ReturnsUser()
    {
        var client = harness.CreateClient();  // Testcontainers
        var response = await client.GetAsync("/api/user/profile");
        response.EnsureSuccessStatusCode();
    }
}
```

**❌ Bad:**
```csharp
[Fact]
public async Task GetUser_ReturnsUser()
{
    var client = new MongoClient("mongodb://localhost:27017");  // Local Docker state
    // ...
}
```

## 13. Lock Primitives

**✅ Good:**
```csharp
private readonly Lock _lock = new();

public void DoWork()
{
    lock (_lock)
    {
        // Critical section
    }
}
```

**❌ Bad:**
```csharp
private readonly object _lock = new object();  // Use Lock type instead
```

## 14. IDE0051/IDE0052 Pragma (Attribute Parameters)

**✅ Good:**
```csharp
#pragma warning disable IDE0051, IDE0052 // Used in attribute parameters
private const string UsernameQueryName = "username";
private const string UserIdQueryName = "id";
#pragma warning restore IDE0051, IDE0052

app.MapGet("/api/user",
    async ([FromQuery(Name = UsernameQueryName)] string? username,
           [FromQuery(Name = UserIdQueryName)] string? id) => { });
```

**❌ Bad:**
```csharp
// No pragma - will be deleted by dotnet format
private const string UsernameQueryName = "username";

app.MapGet("/api/user",
    async ([FromQuery(Name = UsernameQueryName)] string? username) => { });
```

## 15. Constant Organization in Endpoint Classes

**✅ Good:**
```csharp
internal static class UserEndpoints
{
    // 1. Value constants
    private const string BorderEnabledValue = "1";

    // 2. Validation/error messages
    private const string UserNotFoundMessage = "user not found";

    // 3. Endpoint names
    private const string GetUserProfileEndpointName = "GetUserProfile";

    // 4. Query parameter names
    private const string UsernameQueryName = "username";

    // 5. Default string values
    private const string DefaultOverviewStatName = "energyHarvested";

    // 6. Numeric arrays
    private static readonly int[] AllowedStatsIntervals = [8, 180, 1440];

    // 7. Numeric limits
    private const int MaxMemoryBytes = 1024 * 1024;

    // 8. Complex defaults (last)
    private static readonly IReadOnlyDictionary<string, object?> DefaultMemory = new Dictionary<string, object?> { };
}
```

**❌ Bad:**
```csharp
internal static class UserEndpoints
{
    private const int MaxMemoryBytes = 1024 * 1024;  // Wrong order
    private const string GetUserProfileEndpointName = "GetUserProfile";
    private const string UsernameQueryName = "username";  // Wrong order
    private const string UserNotFoundMessage = "user not found";  // Wrong order
}
```

## 16. Target-Typed New Expression (IDE0090)

**✅ Good:**
```csharp
private readonly Lock _lock = new();

var items = new Dictionary<string, int> { ["key"] = 42 };

var powers = new Dictionary<string, PowerCreepPowerSnapshot>(creep.Powers)
{
    [powerKey] = new (Level: 0)
};
```

**❌ Bad:**
```csharp
private readonly Lock _lock = new Lock();  // Explicit type

var items = new Dictionary<string, int> { ["key"] = 42 };

var powers = new Dictionary<string, PowerCreepPowerSnapshot>(creep.Powers)
{
    [powerKey] = new PowerCreepPowerSnapshot(Level: 0)  // Explicit type when context is clear
};
```

## 17. Tuple Variable Naming (camelCase)

**Rule:** Tuple deconstruction variables must use camelCase, not PascalCase. Use the discard pattern `_` for unused tuple elements.

**✅ Good:**
```csharp
// All variables used
var (objectId, payload) = writer.Patches.Single(p => p.ObjectId == creep.Id);
Assert.Equal(20, payload.Store![ResourceTypes.Energy]);
Console.WriteLine(objectId);  // Both used

// Discard unused variables
var (_, payload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
Assert.Equal(20, payload.Store![ResourceTypes.Energy]);  // Only payload used

var (id, _) = Assert.Single(writer.PowerCreepPatches);
Assert.Equal("user1", id);  // Only id used

foreach (var (x, y, _) in hotZones) {  // radius not used
    var posX = x + dx;
    var posY = y + dy;
}
```

**❌ Bad:**
```csharp
var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == creep.Id);  // PascalCase
Assert.Equal(20, Payload.Store![ResourceTypes.Energy]);

var (objectId, payload) = writer.Patches.Single(...);  // Declared but objectId never used
Assert.Equal(20, payload.Store![ResourceTypes.Energy]);  // Should use _ instead

foreach (var (X, Y, Radius) in hotZones) {  // PascalCase
    var posX = X + dx;
}
```

## 18. Dictionary GetValueOrDefault

**✅ Good:**
```csharp
var targetCurrent = targetStore.GetValueOrDefault(resourceType, 0);
var userName = userCache.GetValueOrDefault(userId, "Unknown");
```

**❌ Bad:**
```csharp
var targetCurrent = targetStore.TryGetValue(resourceType, out var tc) ? tc : 0;  // Verbose
var userName = userCache.TryGetValue(userId, out var name) ? name : "Unknown";  // Use GetValueOrDefault instead
```

## 19. Method Signature Line Length

**Rule:** Method signatures and record primary constructors should remain on a single line unless they exceed 185 characters. Only wrap when the line is actually too long, not based on parameter count.

**✅ Good:**
```csharp
// Method signature: 174 characters - keep on one line
private static void ProcessTransfer(RoomProcessorContext context, RoomObjectSnapshot creep, IntentRecord record, Dictionary<string, Dictionary<string, int>> storeLedger)
{
    // ...
}

// Method signature: 120 characters - keep on one line
private static bool IsBlockedByRampart(RoomProcessorContext context, RoomObjectSnapshot creep, RoomObjectSnapshot target)
{
    // ...
}

// Record primary constructor: 139 characters - keep on one line
public sealed record CommodityRecipe(int Amount, int Cooldown, IReadOnlyDictionary<string, int> Components, int? Level = null);

// Over 185 characters - wrap is acceptable
private static void SomeLongMethodName(VeryLongParameterTypeName firstParameter, AnotherLongTypeName secondParameter, YetAnotherLongType thirdParameter, FinallyAVeryLongTypeName fourthParameter)
{
    // ...
}
```

**❌ Bad:**
```csharp
// Method signature: 174 characters - DO NOT wrap unnecessarily
private static void ProcessTransfer(
    RoomProcessorContext context,
    RoomObjectSnapshot creep,
    IntentRecord record,
    Dictionary<string, Dictionary<string, int>> storeLedger)  // ❌ Wrapped even though under 185 chars
{
    // ...
}

// Method signature: 120 characters - DO NOT wrap unnecessarily
private static bool IsBlockedByRampart(
    RoomProcessorContext context,
    RoomObjectSnapshot creep,
    RoomObjectSnapshot target)  // ❌ Wrapped even though under 185 chars
{
    // ...
}

// Record primary constructor: 139 characters - DO NOT wrap unnecessarily
public sealed record CommodityRecipe(
    int Amount,
    int Cooldown,
    IReadOnlyDictionary<string, int> Components,
    int? Level = null);  // ❌ Wrapped even though under 185 chars
```

## 20. Enums vs Constants for Integer Sets

**Rule:** When defining a related set of integer values, use an `enum` instead of a static class with `const int` fields. Enums should be placed in the `Types/` directory, while string/complex constants belong in `Constants/`.

**✅ Good:**
```csharp
// Location: ScreepsDotNet.Common/Types/PowerTypes.cs
namespace ScreepsDotNet.Common.Types;

// Use enum for related integer values
public enum PowerTypes
{
    GenerateOps = 1,
    OperateSpawn = 2,
    DisruptTerminal = 15,
    // ...
}

// Usage - cast to int when needed as key
var effectKey = ((int)PowerTypes.DisruptTerminal).ToString();
effects[effectKey] = new PowerEffect(Power: PowerTypes.DisruptTerminal, Level: 1, EndTime: 200);
```

**❌ Bad:**
```csharp
// Location: ScreepsDotNet.Common/Constants/PowerTypes.cs  ❌ Wrong directory
namespace ScreepsDotNet.Common.Constants;

// Don't use static class with constants for integer sets
public static class PowerTypes  // ❌ Should be enum in Types/
{
    public const int GenerateOps = 1;
    public const int OperateSpawn = 2;
    public const int DisruptTerminal = 15;
    // ...
}

// Usage is the same but lacks type safety
var effectKey = PowerTypes.DisruptTerminal.ToString();  // ❌ Loses type safety
effects[effectKey] = new PowerEffect();
```

**File location:**
- **Enums** → `*/Types/` directory (e.g., `ScreepsDotNet.Common/Types/PowerTypes.cs`)
- **String/complex constants** → `*/Constants/` directory (e.g., `ScreepsDotNet.Common/Constants/ResourceTypes.cs`)

**When to use enum:**
- Set of related integer values (status codes, type IDs, effect types)
- Values are mutually exclusive
- Values have semantic meaning (not arbitrary magic numbers)

**When to use constants:**
- Unrelated values that happen to be the same type
- String constants (ResourceTypes, IntentKeys, RoomObjectTypes)
- Complex values (arrays, objects, computed values)
- Values used in attribute parameters (enums work here too, but constants are clearer)

## 21. Tuple Deconstruction (IDE0042)

**Rule:** Always deconstruct tuple return values into individual variables using camelCase naming. Use the discard pattern `_` for unused tuple elements.

**✅ Good:**
```csharp
// Deconstruct tuple with both elements used (camelCase)
var (objectId, payload) = Assert.Single(writer.Patches);
Assert.Equal(nuker.Id, objectId);
Assert.Equal(0, payload.Store![ResourceTypes.Energy]);

// Discard unused tuple elements with underscore
var (_, payload) = Assert.Single(writer.Patches);
Assert.Equal(200 + ScreepsGameConstants.NukerCooldown, payload.CooldownTime);

// Multiple tuple deconstructions in same scope
var (_, rampartPayload) = writer.Patches.Single(p => p.ObjectId == rampart.Id);
var (_, towerPayload) = writer.Patches.Single(p => p.ObjectId == tower.Id);
Assert.Equal(0, rampartPayload.Hits);
Assert.Equal(0, towerPayload.Hits);

// Deconstruct method return values
var (success, result) = ValidateInput(data);
if (success)
    Process(result);
```

**❌ Bad:**
```csharp
// Don't access tuple properties directly without deconstructing
var patch = Assert.Single(writer.Patches);  // ❌ Should deconstruct
Assert.Equal(nuker.Id, patch.ObjectId);  // ❌ Accessing tuple property
Assert.Equal(0, patch.Payload.Store![ResourceTypes.Energy]);

// Don't use PascalCase for tuple variables
var (ObjectId, Payload) = Assert.Single(writer.Patches);  // ❌ Should be camelCase
Assert.Equal(nuker.Id, ObjectId);

// Don't declare unused variables (use discard instead)
var (objectId, payload) = Assert.Single(writer.Patches);  // ❌ objectId never used
Assert.Equal(0, payload.Hits);  // Should use: var (_, payload) = ...
```

**IDE Configuration:**
```ini
# .editorconfig
csharp_style_deconstructed_variable_declaration = true:warning
dotnet_diagnostic.ide0042.severity = warning
```

## Summary

For complete rule documentation, see:
- `src/.editorconfig` - C# style rules (ERROR/WARNING severity)
- `src/ScreepsDotNet.slnx.DotSettings` - ReSharper inspections (ERROR/WARNING severity)
- `src/Directory.Build.props` - Implicit usings, global MSBuild properties
