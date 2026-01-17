# ScreepsDotNet - Claude Context

## Mission

Modern .NET rewrite of the Screeps private server backend. Exposes legacy HTTP + CLI surface while gradually replacing Node.js driver/engine with managed code. Goal: Full-featured private server with better performance, maintainability, and extensibility.

## Critical Rules (Read First)

- ✅ **ALWAYS** use Context7 MCP for library/API documentation without being asked
- ✅ **ALWAYS** use `var` for all variable declarations (never explicit types)
- ✅ **ALWAYS** use collection expressions `[]` (never `new List<T>()`)
- ✅ **ALWAYS** use primary constructors (never classic constructor syntax)
- ✅ **ALWAYS** use file-scoped namespaces (`namespace Foo;` not `namespace Foo { }`)
- ✅ **ALWAYS** suffix async methods with "Async"
- ✅ **ALWAYS** specify accessibility modifiers explicitly (public/private/internal/etc)
- ✅ **ALWAYS** use `is null` not `== null` for null checks
- ✅ **ALWAYS** use pattern matching (`if (obj is User user)` not `as` + null check)
- ✅ **ALWAYS** use trailing commas in multi-line collections/arrays
- ✅ **ALWAYS** keep lines under 185 characters (don't wrap unnecessarily)
- ✅ **ALWAYS** use positive conditions in ternary operators (never negate: use `condition ? true : false` not `!condition ? false : true`)
- ✅ **ALWAYS** assign ternary expressions to a variable before returning - applies to ALL ternaries (simple, multi-line, nested, complex) - never `return x ? a : b;` always `var result = x ? a : b; return result;`
- ✅ **ALWAYS** run `dotnet format style --exclude-diagnostics IDE0051 IDE0052 IDE0060` before committing
- ✅ **ALWAYS** run `git status` from `ScreepsDotNet/` directory (not repo root)
- ✅ **ALWAYS** use Testcontainers for integration tests (never local Docker state)
- ❌ **NEVER** modify files in `ScreepsNodeJs/` (separate git repository)
- ❌ **NEVER** run `dotnet build` while `dotnet run` is active (DLL lock issues)
- ❌ **NEVER** add `using System;` or other implicit usings manually
- ❌ **NEVER** use `BsonDocument` in repositories (use typed POCOs)
- ❌ **NEVER** use `this.` or static class qualifiers for members in same class
- ❌ **NEVER** wrap lines under 185 characters

## Quick Start

### First Time Setup
```bash
# 1. Navigate to solution directory
cd ScreepsDotNet

# 2. Start MongoDB + Redis with seed data
docker compose -f src/docker-compose.yml up -d

# 3. Verify seeds loaded
docker compose -f src/docker-compose.yml logs -f mongo

# 4. Run tests to verify environment
dotnet test src/ScreepsDotNet.slnx

# 5. Start HTTP backend
dotnet run --project src/ScreepsDotNet.Backend.Http/ScreepsDotNet.Backend.Http.csproj
```

### Quick Smoke Test
```http
# Get server info
GET http://localhost:5210/api/server/info

# Authenticate (dev token from appsettings.Development.json)
POST http://localhost:5210/api/auth/steam-ticket
Content-Type: application/json

{
  "ticket": "dev-ticket",
  "useNativeAuth": false
}

# Use returned token for authenticated requests
GET http://localhost:5210/api/user/code
X-Token: <token-from-auth>
```

## Project Structure

**Solution File:** `src/ScreepsDotNet.slnx` (XML-based solution format)

**Working Directory:** `/home/th3b0y/screeps-rewrite/ScreepsDotNet`
- Always run git commands from this directory (NOT the repo root)
- Solution file is in `src/` subdirectory

**Key Configuration Files:**
- `.editorconfig`: `src/.editorconfig` (coding style rules - ERROR/WARNING levels)
- `.DotSettings`: `src/ScreepsDotNet.slnx.DotSettings` (ReSharper settings)
- `Directory.Build.props`: `src/Directory.Build.props` (implicit usings, global MSBuild properties)
- Docker Compose: `src/docker-compose.yml` (MongoDB + Redis)

```
ScreepsDotNet/
├── CLAUDE.md                                    # This file - AI context
├── README.md                                    # Human-facing overview
├── docs/                                        # Human documentation
│   ├── getting-started.md                      # Setup tutorial
│   ├── backend.md                              # HTTP API coverage
│   ├── http-endpoints.md                       # Route reference
│   ├── cli.md                                  # CLI command reference
│   ├── driver.md                               # Driver design overview
│   └── README.md                               # Documentation ownership map
├── ScreepsNodeJs/                               # ⚠️ Separate git repo - DO NOT MODIFY
└── src/
    ├── ScreepsDotNet.slnx                      # ⚠️ SOLUTION FILE
    ├── ScreepsDotNet.slnx.DotSettings          # ⚠️ ReSharper settings (ERROR/WARNING rules)
    ├── .editorconfig                           # ⚠️ Coding style rules (ERROR/WARNING levels)
    ├── Directory.Build.props                   # ⚠️ Global MSBuild properties, implicit usings
    ├── docker-compose.yml                      # ⚠️ MongoDB + Redis orchestration
    ├── ScreepsDotNet.Backend.Core/             # Contracts, DTOs, abstractions
    │   └── Seeding/SeedDataDefaults.cs         # Test fixture constants
    ├── ScreepsDotNet.Backend.Http/             # ASP.NET Core API host
    │   ├── Endpoints/                          # Route handlers
    │   └── *.http                              # Manual test files
    ├── ScreepsDotNet.Backend.Cli/              # Spectre.Console CLI
    │   └── Commands/                           # CLI command implementations
    ├── ScreepsDotNet.Storage.MongoRedis/       # Data access layer
    │   └── Repositories/                       # Typed Mongo/Redis repos
    ├── ScreepsDotNet.Driver/                   # Runtime coordinator
    │   ├── CLAUDE.md                           # ⚠️ Driver-specific context
    │   ├── Services/                           # Queue, runtime, processor
    │   └── docs/                               # Driver design docs
    ├── ScreepsDotNet.Engine/                   # Simulation kernel rewrite
    │   ├── CLAUDE.md                           # ⚠️ Engine-specific context
    │   └── docs/                               # Engine design docs
    ├── native/pathfinder/                      # C++ pathfinder P/Invoke
    │   └── CLAUDE.md                           # ⚠️ Pathfinder-specific context
    ├── docker/                                 # Docker configs
    │   └── mongo-init/                         # Seed scripts
    └── docker-compose.yml                      # Mongo + Redis orchestration
```

## Coding Standards (Enforced)

**Rules defined in:**
- `src/.editorconfig` - C# style rules (ERROR/WARNING severity)
- `src/ScreepsDotNet.slnx.DotSettings` - ReSharper inspections (ERROR/WARNING severity)
- `src/Directory.Build.props` - Implicit usings, MSBuild properties

All rules below are enforced at **ERROR** level unless otherwise noted.

### Implicit Usings
Configured in `Directory.Build.props` (`<ImplicitUsings>enable</ImplicitUsings>`).

**The following 7 usings are IMPLICIT (never add manually):**
1. `System`
2. `System.Collections.Generic`
3. `System.IO`
4. `System.Linq`
5. `System.Net.Http`
6. `System.Threading`
7. `System.Threading.Tasks`

**ALL other System.* usings are EXPLICIT (must be added):**
- ✅ `System.Text` - Required for `Encoding`
- ✅ `System.Text.Json` - Required for `JsonSerializer`, `JsonElement`, `JsonValueKind`
- ✅ `System.Text.RegularExpressions` - Required for `Regex`, `GeneratedRegexAttribute`
- ✅ Any other System.* namespace not in the list above

**❌ Bad (adding implicit usings):**
```csharp
using System;                          // ❌ Implicit - remove
using System.Collections.Generic;      // ❌ Implicit - remove
using System.Linq;                     // ❌ Implicit - remove
using System.Threading.Tasks;          // ❌ Implicit - remove
```

**✅ Good (only explicit usings):**
```csharp
// Implicit System.* usings omitted (automatically available)
// Only add NON-implicit System.* usings and external namespaces
using System.Text;                     // ✅ Explicit - required for Encoding
using System.Text.Json;                // ✅ Explicit - required for JsonSerializer
using MongoDB.Driver;                  // ✅ External - always required
using ScreepsDotNet.Backend.Core.Abstractions;  // ✅ Project - always required
```

### Variable Declarations
**✅ Good:**
```csharp
// ALWAYS use var for all declarations
var user = await _userRepository.GetByIdAsync(userId);
var items = await _collection.Find(filter).ToListAsync();
var response = new ApiResponse { Success = true };
var count = 0;
var userName = "test";
var role = UserRole.Admin;
```

**❌ Bad:**
```csharp
// Don't use explicit types
int count = 0;
string userName = "test";
UserRole role = UserRole.Admin;
ApiResponse response = new ApiResponse();
```

### Collection Expressions
**✅ Good:**
```csharp
// ALWAYS use collection expressions
var items = [];
var users = [];
var names = ["alice", "bob", "charlie"];
var dict = new Dictionary<string, int> { ["key"] = 1 };
```

**❌ Bad:**
```csharp
// Don't use old syntax
var items = new List<string>();
var dict = new Dictionary<string, int>();
var names = new List<string> { "alice", "bob", "charlie" };
```

### Expression-Bodied Members
**✅ Good:**
```csharp
// Use for single-line methods (=> on new line)
public string GetUserName()
    => _user.Name;

public int GetCount()
    => _items.Count;

public bool IsValid()
    => _status == Status.Active;

// Properties
public string Name => _name;
public int Count => _items.Count;
```

**❌ Bad:**
```csharp
// Don't use block syntax for one-liners
public string GetUserName()
{
    return _user.Name;
}

// Don't put => on same line
public string GetUserName() => _user.Name;
```

### Primary Constructors
**✅ Good:**
```csharp
// ALWAYS use primary constructors
public class UserService(IUserRepository userRepository)
{
    public async Task<User?> GetUserAsync(string id)
        => await userRepository.GetByIdAsync(id);
}

// Works with multiple parameters
public class OrderProcessor(
    IOrderRepository orderRepository,
    ILogger<OrderProcessor> logger,
    IMessageQueue messageQueue)
{
    public async Task ProcessAsync(string orderId)
    {
        logger.LogInformation("Processing order {OrderId}", orderId);
        var order = await orderRepository.GetByIdAsync(orderId);
        await messageQueue.PublishAsync(order);
    }
}
```

**❌ Bad:**
```csharp
// Don't use classic constructor syntax
public class UserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<User?> GetUserAsync(string id)
        => await _userRepository.GetByIdAsync(id);
}
```

### File-Scoped Namespaces
**✅ Good:**
```csharp
// ALWAYS use file-scoped namespaces
namespace ScreepsDotNet.Backend.Http;

public class UserService(IUserRepository userRepository)
{
    public async Task<User?> GetUserAsync(string id)
        => await userRepository.GetByIdAsync(id);
}
```

**❌ Bad:**
```csharp
// Don't use block-scoped namespaces
namespace ScreepsDotNet.Backend.Http
{
    public class UserService(IUserRepository userRepository)
    {
        public async Task<User?> GetUserAsync(string id)
            => await userRepository.GetByIdAsync(id);
    }
}
```

### Async Method Naming
**✅ Good:**
```csharp
// ALWAYS suffix async methods with "Async"
public async Task<User> GetUserAsync(string id)
    => await _repository.GetByIdAsync(id);

public async Task ProcessAsync()
{
    await DoWorkAsync();
}
```

**❌ Bad:**
```csharp
// Don't omit "Async" suffix
public async Task<User> GetUser(string id)  // ERROR!
    => await _repository.GetByIdAsync(id);

public async Task Process()  // ERROR!
{
    await DoWorkAsync();
}
```

### Accessibility Modifiers
**✅ Good:**
```csharp
// ALWAYS explicitly specify accessibility
public class UserService { }
internal class InternalHelper { }
private readonly IUserRepository _repository;
public async Task DoWorkAsync() { }
```

**❌ Bad:**
```csharp
// Don't omit accessibility modifiers
class UserService { }  // ERROR: Missing 'public' or 'internal'
readonly IUserRepository _repository;  // ERROR: Missing 'private'
async Task DoWorkAsync() { }  // ERROR: Missing 'public/private/etc'
```

### Modifier Order
**✅ Good:**
```csharp
// Correct order: public, private, protected, internal, static, extern, new, virtual, abstract, sealed, override, readonly, unsafe, volatile, async
public static readonly string DefaultName = "test";
private async Task DoWorkAsync() { }
protected virtual void OnChanged() { }
public override string ToString() => "User";
```

**❌ Bad:**
```csharp
// Don't use wrong modifier order
static public readonly string DefaultName = "test";  // ERROR!
async private Task DoWorkAsync() { }  // ERROR!
virtual protected void OnChanged() { }  // ERROR!
```

### Predefined Types
**✅ Good:**
```csharp
// ALWAYS use language keywords
string name = "test";
int count = int.Parse("42");
object obj = new();
bool isValid = true;
```

**❌ Bad:**
```csharp
// Don't use BCL type names
String name = "test";  // ERROR!
int count = Int32.Parse("42");  // ERROR!
Object obj = new();  // ERROR!
Boolean isValid = true;  // ERROR!
```

### Pattern Matching
**✅ Good:**
```csharp
// ALWAYS use pattern matching
if (obj is User user)
    return user.Name;

if (value is not null)
    Process(value);

if (input is > 0 and < 100)
    return true;
```

**❌ Bad:**
```csharp
// Don't use old-style type checks
var user = obj as User;  // ERROR!
if (user != null)
    return user.Name;

if (!(value is null))  // ERROR!
    Process(value);

if (input > 0 && input < 100)  // Use 'is > 0 and < 100'
    return true;
```

### Null Checking
**✅ Good:**
```csharp
// Use 'is null' and null-coalescing operators
if (value is null)
    throw new ArgumentNullException(nameof(value));

var name = user?.Name ?? "Unknown";
handler?.Invoke();

// Use throw expressions
var user = GetUser() ?? throw new InvalidOperationException("User not found");
```

**❌ Bad:**
```csharp
// Don't use == null or explicit null checks
if (value == null)  // ERROR: Use 'is null'
    throw new ArgumentNullException(nameof(value));

if (handler != null)  // ERROR: Use 'handler?.Invoke()'
    handler.Invoke();
```

### Braces for Multi-Line Statements
**✅ Good:**
```csharp
// Single line: braces optional
if (condition) return;
for (var i = 0; i < 10; i++) Process(i);

// Multi-line: braces required
if (condition)
{
    DoSomething();
    DoSomethingElse();
}

foreach (var item in items)
{
    Process(item);
    Log(item);
}
```

**❌ Bad:**
```csharp
// Don't omit braces for multi-line
if (condition)  // ERROR!
    DoSomething();
    DoSomethingElse();

foreach (var item in items)  // ERROR!
    Process(item);
    Log(item);
```

### Trailing Commas
**✅ Good:**
```csharp
// Multi-line: always use trailing comma
var items = new[]
{
    "a",
    "b",
    "c",
};

var dict = new Dictionary<string, int>
{
    ["key1"] = 1,
    ["key2"] = 2,
};

// Single line: no trailing comma
var items = new[] { "a", "b", "c" };
```

**❌ Bad:**
```csharp
// Don't omit trailing comma in multi-line
var items = new[]
{
    "a",
    "b",
    "c"  // ERROR: Missing trailing comma
};

// Don't add trailing comma in single line
var items = new[] { "a", "b", "c", };  // ERROR!
```

### Object Creation When Type Evident
**✅ Good:**
```csharp
// Use target-typed new when type is evident
UserService service = new();
List<string> items = [];
Dictionary<string, int> dict = new();
```

**❌ Bad:**
```csharp
// Don't repeat type when evident
UserService service = new UserService();  // ERROR!
List<string> items = new List<string>();  // ERROR!
```

### Inline Variable Declaration
**✅ Good:**
```csharp
// Declare variables inline with out parameter
if (int.TryParse(input, out var number))
    return number;

if (_cache.TryGetValue(key, out var value))
    return value;
```

**❌ Bad:**
```csharp
// Don't declare variables before out parameter
int number;  // ERROR!
if (int.TryParse(input, out number))
    return number;
```

### this. and Static Qualifiers
**✅ Good:**
```csharp
// Don't use 'this.' for members
public class UserService(IUserRepository repository)
{
    public async Task<User> GetAsync(string id)
        => await repository.GetByIdAsync(id);  // Not this.repository
}

// Don't use class name for static members (within same class)
public class MathHelper
{
    public static int Add(int a, int b) => a + b;

    public static int Calculate() => Add(1, 2);  // Not MathHelper.Add(1, 2)
}
```

**❌ Bad:**
```csharp
// Don't use unnecessary qualifiers
public class UserService(IUserRepository repository)
{
    public async Task<User> GetAsync(string id)
        => await this.repository.GetByIdAsync(id);  // ERROR!
}

public class MathHelper
{
    public static int Add(int a, int b) => a + b;

    public static int Calculate() => MathHelper.Add(1, 2);  // ERROR!
}
```

### Line Length and Wrapping
**✅ Good:**
```csharp
// Keep lines under 185 characters - don't wrap unless necessary
public async Task<UserProfileResponse> GetUserProfileWithAllDetailsAsync(string userId, bool includeHistory, CancellationToken cancellationToken)
    => await _repository.GetUserProfileAsync(userId, includeHistory, cancellationToken);

// Only wrap if exceeding 185 characters
public async Task<UserProfileResponse> GetUserProfileWithAllDetailsIncludingHistoryAndPreferencesAsync(
    string userId,
    bool includeHistory,
    bool includePreferences,
    CancellationToken cancellationToken)
{
    return await _repository.GetUserProfileAsync(userId, includeHistory, includePreferences, cancellationToken);
}
```

**❌ Bad:**
```csharp
// Don't wrap lines unnecessarily (under 185 chars)
public async Task<UserProfileResponse> GetUserProfileAsync(
    string userId,  // ERROR: Don't wrap short parameter lists
    bool includeHistory)
{
    return await _repository.GetUserProfileAsync(
        userId,
        includeHistory);  // ERROR: Don't wrap short method calls
}
```

### Namespace Match Folder
**✅ Good:**
```csharp
// File: src/ScreepsDotNet.Backend.Http/Services/UserService.cs
namespace ScreepsDotNet.Backend.Http.Services;

// File: src/ScreepsDotNet.Storage.MongoRedis/Repositories/UserRepository.cs
namespace ScreepsDotNet.Storage.MongoRedis.Repositories;
```

**❌ Bad:**
```csharp
// File: src/ScreepsDotNet.Backend.Http/Services/UserService.cs
namespace ScreepsDotNet.Backend.Http;  // ERROR: Doesn't match folder

// File: src/ScreepsDotNet.Storage.MongoRedis/Repositories/UserRepository.cs
namespace ScreepsDotNet.Storage;  // ERROR: Incomplete namespace
```

### Constant Organization in Endpoint Classes
**✅ Good:**
```csharp
// Organization pattern for endpoint classes (e.g., UserEndpoints.cs, MarketEndpoints.cs)
internal static class UserEndpoints
{
    // 1. Value constants (border enabled, special identifiers)
    private const string BorderEnabledValue = "1";
    private const string BorderEnabledAlternateValue = "true";

    // 2. Validation/error messages
    private const string MissingUserContextMessage = "User context is not available.";
    private const string MissingUsernameMessage = "username is required.";
    private const string UserNotFoundMessage = "user not found";

    // 3. Endpoint names (route identifiers)
    private const string GetUserProfileEndpointName = "GetUserProfile";
    private const string PostUserBadgeEndpointName = "PostUserBadge";
    private const string FindEndpointName = "GetUserFind";

    // 4. Query parameter names
    private const string UsernameQueryName = "username";
    private const string UserIdQueryName = "id";
    private const string BorderQueryName = "border";

    // 5. Default string values
    private const string DefaultOverviewStatName = "energyHarvested";

    // 6. Numeric arrays
    private static readonly int[] AllowedStatsIntervals = [8, 180, 1440];

    // 7. Numeric limits/sizes
    private const int MaxMemoryBytes = 1024 * 1024;
    private const int MaxConsoleExpressionBytes = 1024;

    // 8. Dictionary/complex defaults (last)
    private static readonly IReadOnlyDictionary<string, object?> DefaultMemory = new Dictionary<string, object?>
    {
        ["settings"] = new Dictionary<string, object?> { }
    };
}
```

**❌ Bad:**
```csharp
// Don't mix constant types randomly
internal static class UserEndpoints
{
    private const int MaxMemoryBytes = 1024 * 1024;  // ERROR: Numeric constant before messages
    private const string GetUserProfileEndpointName = "GetUserProfile";
    private const string UsernameQueryName = "username";  // ERROR: Query param before endpoint names
    private const string MissingUserContextMessage = "User context is not available.";  // ERROR: Message after endpoint names
}
```

**Organization Rules:**
1. **Value constants** - Special values used for comparisons (e.g., "1", "true", "$activeWorld")
2. **Validation messages** - Error messages, missing param messages, validation failures
3. **Endpoint names** - Route identifiers for URL generation/testing
4. **Query parameter names** - Names used in `[FromQuery(Name = ...)]` attributes
5. **Default string values** - Default values for string properties
6. **Numeric arrays** - Arrays of allowed values
7. **Numeric limits** - Size limits, max values, min values
8. **Complex defaults** - Dictionaries, complex objects (always last)

**Exception:** Query parameter names like "segment", "respondent", "room", "shard" used only once inline can remain inline:
```csharp
// OK for one-off query parameters
app.MapGet(ApiRoutes.User.MemorySegment,
           async ([FromQuery(Name = "segment")] int? segment,
```

## Code Quality Warnings

**Rules defined in:**
- `src/.editorconfig` - C# style rules (WARNING severity)
- `src/ScreepsDotNet.slnx.DotSettings` - ReSharper inspections (WARNING severity)

The following rules are set to **WARNING** level. While not blocking errors, they should be followed for consistent code quality.

### Naming Conventions
**✅ Good:**
```csharp
// Constants: PascalCase
private const string DefaultName = "test";
public const int MaxRetries = 3;

// Properties: PascalCase
public string UserName { get; set; }
public int Count { get; private set; }

// Private fields: _camelCase
private readonly IUserRepository _repository;
private string _userName;

// Private static readonly: PascalCase
private static readonly string ApiVersion = "1.0";
private static readonly int DefaultTimeout = 30;

// Methods: PascalCase
public async Task ProcessAsync() { }
private void ValidateInput() { }
```

**⚠️ Warning:**
```csharp
// Don't use camelCase for constants
private const string defaultName = "test";  // Should be DefaultName

// Don't use camelCase for properties
public string userName { get; set; }  // Should be UserName

// Don't omit underscore for private fields
private readonly IUserRepository repository;  // Should be _repository
private string userName;  // Should be _userName

// Don't use camelCase for private static readonly
private static readonly string apiVersion = "1.0";  // Should be ApiVersion
```

### Readonly Fields
**✅ Good:**
```csharp
// Mark fields readonly if never reassigned
public class UserService(IUserRepository repository)
{
    private readonly IUserRepository _repository = repository;
    private readonly Lock _lock = new();
}
```

**⚠️ Warning:**
```csharp
// Field never reassigned - should be readonly
public class UserService(IUserRepository repository)
{
    private IUserRepository _repository = repository;  // Add readonly
    private Lock _lock = new();  // Add readonly
}
```

### Object and Collection Initializers
**✅ Good:**
```csharp
// Use object initializers
var user = new User
{
    Name = "test",
    Age = 25,
    Email = "test@example.com"
};

// Use collection initializers
var items = new List<string>
{
    "item1",
    "item2",
    "item3"
};
```

**⚠️ Warning:**
```csharp
// Don't set properties after construction
var user = new User();
user.Name = "test";
user.Age = 25;
user.Email = "test@example.com";

// Don't add items after construction
var items = new List<string>();
items.Add("item1");
items.Add("item2");
items.Add("item3");
```

### Null Propagation and Coalescing
**✅ Good:**
```csharp
// Use null-conditional operator
var name = user?.Name;
handler?.Invoke();

// Use null-coalescing operator
var displayName = user?.Name ?? "Anonymous";
var count = items?.Count ?? 0;

// Combined
var result = cache?.Get(key) ?? defaultValue;
```

**⚠️ Warning:**
```csharp
// Don't use explicit null checks
var name = user != null ? user.Name : null;
if (handler != null)
    handler.Invoke();

// Don't use ternary for null checks
var displayName = user != null && user.Name != null ? user.Name : "Anonymous";
```

### Conditional Expressions for Assignment
**✅ Good:**
```csharp
// Use ternary for simple assignments
var status = isValid ? "Valid" : "Invalid";
var result = count > 0 ? "Found" : "Empty";

// Use compound assignment
count += 5;
total *= 2;
name ??= "Default";
```

**⚠️ Warning:**
```csharp
// Don't use if/else for simple assignments
string status;
if (isValid)
    status = "Valid";
else
    status = "Invalid";

// Don't use long form
count = count + 5;  // Use count += 5
total = total * 2;  // Use total *= 2
```

### Positive Conditions in Ternary Operators
**✅ Good:**
```csharp
// Always use positive conditions (not negated)
var result = success ? "Success" : "Failed";
var value = isValid ? trueValue : falseValue;
var interval = string.IsNullOrWhiteSpace(userId) ? null : ownedInterval;

// Assign to variable, then return
var result = success ? Results.Ok() : Results.BadRequest();
return result;
```

**❌ Bad:**
```csharp
// Don't negate the condition - flip the values instead
var result = !success ? "Failed" : "Success";  // ❌ Negated condition
var value = !isValid ? falseValue : trueValue;  // ❌ Negated condition
var interval = !string.IsNullOrWhiteSpace(userId) ? ownedInterval : null;  // ❌ Negated condition

// Don't return ternary directly - assign to variable first
return !success ? Results.BadRequest() : Results.Ok();  // ❌ Negated condition AND direct return
return success ? Results.Ok() : Results.BadRequest();  // ❌ Direct return of ternary
```

### Ternary Return Statements
**✅ Good:**
```csharp
// Always assign ternary result to variable first, then return
var result = success ? Results.Ok(UserResponseFactory.CreateEmpty()) : Results.BadRequest(new ErrorResponse(UserNotFoundMessage));
return result;

var intent = directions.Count == 0 ? null : new SetSpawnDirectionsIntent(directions);
return intent;

var exitCode = status.IsConnected ? 0 : 1;
return exitCode;

// Also applies to complex/nested/multi-line ternaries
var healthResult = status.IsConnected ? HealthCheckResult.Healthy(HealthyMessage)
                                      : HealthCheckResult.Unhealthy(status.Details ?? UnhealthyFallbackMessage);
return healthResult;

var blueprint = string.IsNullOrWhiteSpace(structureType)
    ? null
    : blueprintProvider.TryGet(structureType, out var foundBlueprint) ? foundBlueprint : null;
return blueprint;
```

**❌ Bad:**
```csharp
// Don't return ternary expressions directly (simple)
return success ? Results.Ok(UserResponseFactory.CreateEmpty()) : Results.BadRequest(new ErrorResponse(UserNotFoundMessage));  // ❌
return directions.Count == 0 ? null : new SetSpawnDirectionsIntent(directions);  // ❌
return status.IsConnected ? 0 : 1;  // ❌

// Don't return multi-line ternaries directly
return status.IsConnected ? HealthCheckResult.Healthy(HealthyMessage)
                          : HealthCheckResult.Unhealthy(status.Details ?? UnhealthyFallbackMessage);  // ❌

// Don't return nested ternaries directly
return string.IsNullOrWhiteSpace(structureType)
    ? null
    : blueprintProvider.TryGet(structureType, out var blueprint) ? blueprint : null;  // ❌
```

### Inferred Member Names
**✅ Good:**
```csharp
// Let compiler infer member names
var name = "John";
var age = 30;
var person = new { name, age };  // Inferred

// Tuples
var result = (name, age);
return (name, age);
```

**⚠️ Warning:**
```csharp
// Don't repeat names unnecessarily
var name = "John";
var age = 30;
var person = new { name = name, age = age };  // Redundant

var result = (name: name, age: age);  // Redundant
```

### Simple Using Statement
**✅ Good:**
```csharp
// Use simple using declaration
public async Task ProcessFileAsync(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = new StreamReader(stream);
    var content = await reader.ReadToEndAsync();
    // stream and reader disposed automatically
}
```

**⚠️ Warning:**
```csharp
// Don't use block syntax when simple using works
public async Task ProcessFileAsync(string path)
{
    using (var stream = File.OpenRead(path))
    using (var reader = new StreamReader(stream))
    {
        var content = await reader.ReadToEndAsync();
    }
}
```

### Deconstruction
**✅ Good:**
```csharp
// Deconstruct tuples directly
var (name, age) = GetPerson();
var (success, result) = TryGetValue(key);

// Deconstruct in foreach
foreach (var (key, value) in dictionary)
{
    Console.WriteLine($"{key}: {value}");
}
```

**⚠️ Warning:**
```csharp
// Don't access tuple items by position
var person = GetPerson();
var name = person.Item1;
var age = person.Item2;

// Don't use KeyValuePair properties
foreach (var kvp in dictionary)
{
    Console.WriteLine($"{kvp.Key}: {kvp.Value}");
}
```

### Simplify Default Expression
**✅ Good:**
```csharp
// Use simplified default
int value = default;
User user = default;
List<string> items = default;

// In return statements
return default;
```

**⚠️ Warning:**
```csharp
// Don't specify type for default
int value = default(int);
User user = default(User);
List<string> items = default(List<string>);
```

### Index and Range Operators
**✅ Good:**
```csharp
// Use index from end operator
var last = array[^1];
var secondLast = array[^2];

// Use range operator
var firstThree = array[..3];
var lastTwo = array[^2..];
var middle = array[2..^2];

// Skip and take
var slice = array[5..10];
```

**⚠️ Warning:**
```csharp
// Don't calculate indices manually
var last = array[array.Length - 1];
var secondLast = array[array.Length - 2];

// Don't use LINQ when range works
var firstThree = array.Take(3).ToArray();
var lastTwo = array.Skip(array.Length - 2).ToArray();
var slice = array.Skip(5).Take(5).ToArray();
```

### Parentheses for Clarity
**✅ Good:**
```csharp
// Use parentheses for complex expressions
var result = (a + b) * (c - d);
var isValid = (x > 0) && (y < 10);
var flag = (condition1 || condition2) && condition3;

// Not needed for simple expressions
var sum = a + b;
var isPositive = x > 0;
```

**⚠️ Warning:**
```csharp
// Don't add unnecessary parentheses
var result = (x);  // Unnecessary
var flag = (true);  // Unnecessary
var value = (42);  // Unnecessary
```

### Switch Expression Cases
**✅ Good:**
```csharp
// Add all possible cases in switch expressions
var result = status switch
{
    Status.Active => "Active",
    Status.Inactive => "Inactive",
    Status.Pending => "Pending",
    Status.Deleted => "Deleted",
    _ => throw new ArgumentOutOfRangeException(nameof(status))
};
```

**⚠️ Warning:**
```csharp
// IDE0010: Add missing cases
var result = status switch
{
    Status.Active => "Active",
    Status.Inactive => "Inactive",
    // Missing: Pending, Deleted
    _ => "Unknown"
};
```

### Code Simplification
**✅ Good:**
```csharp
// Simplify names
using System.Text;
var builder = new StringBuilder();  // Not System.Text.StringBuilder

// Remove unnecessary casts
var items = (List<string>)GetItems();  // Only if needed

// Simplify member access
var count = items.Count;  // Not this.items.Count
```

**⚠️ Warning:**
```csharp
// IDE0001: Simplify name
var builder = new System.Text.StringBuilder();  // Remove System.Text

// IDE0004: Remove unnecessary cast
var number = (int)5;  // Unnecessary

// IDE0002: Simplify member access
var count = this.items.Count;  // Remove 'this.'
```

### IDE0051/IDE0052 False Positives (Attribute Parameters)
**⚠️ CRITICAL: Known Roslyn Bug**

IDE0051 ("Remove unused private members") and IDE0052 ("Remove unread private members") incorrectly flag constants as unused/unread when they are **only used in attribute parameters**. This causes `dotnet format style` to delete actively-used constants.

**✅ Solution 1 - Run formatter with exclusions (REQUIRED):**
```bash
# ALWAYS use this command instead of plain 'dotnet format style'
dotnet format style --exclude-diagnostics IDE0051 IDE0052 IDE0060

# IDE0051 = Remove unused private members (false positive for attribute params)
# IDE0052 = Remove unread private members (false positive for attribute params)
# IDE0060 = Remove unused parameters (optional exclusion for consistency)
```

**✅ Solution 2 - Protect constants with pragma (REQUIRED):**
```csharp
// Endpoint constants used ONLY in [FromQuery(Name = ...)] attributes
#pragma warning disable IDE0051, IDE0052 // Used in attribute parameters
private const string UsernameQueryName = "username";
private const string UserIdQueryName = "id";
private const string ResourceTypeQueryName = "resourceType";
#pragma warning restore IDE0051, IDE0052

// Usage in attributes
app.MapGet("/api/user",
    async ([FromQuery(Name = UsernameQueryName)] string? username,
           [FromQuery(Name = UserIdQueryName)] string? id) => {
        // ...
    });
```

**❌ Bad - Will be deleted by dotnet format:**
```csharp
// NO pragma protection - will be incorrectly deleted!
private const string UsernameQueryName = "username";

app.MapGet("/api/user",
    async ([FromQuery(Name = UsernameQueryName)] string? username) => {
        // After 'dotnet format style' without exclusions, UsernameQueryName will be deleted
        // causing compilation errors!
    });
```

**Why both solutions are needed:**
- **Pragma suppression** prevents IDE warnings and protects individual constants
- **Command-line exclusions** prevent the formatter from deleting constants globally
- Both work together to prevent Roslyn from breaking the code

**When to add pragma suppression:**
- Constants used **exclusively** in attribute parameters (`[FromQuery]`, `[FromHeader]`, `[FromRoute]`, etc.)
- If constant is also used in regular code (if statements, switches), no suppression needed
- Applies to all endpoint files: `*Endpoints.cs` in `ScreepsDotNet.Backend.Http/Endpoints/`

**Examples in codebase:**
- `UserEndpoints.cs` lines 79-83: `UsernameQueryName`, `UserIdQueryName`, `BorderQueryName`
- `MarketEndpoints.cs` lines 18-20: `ResourceTypeQueryName`

### Lock Primitives
**✅ Good:**
```csharp
// Use Lock type (not object)
private readonly Lock _lock = new();

public async Task DoWorkAsync()
{
    lock (_lock)
    {
        // Critical section
    }
}
```

**❌ Bad:**
```csharp
// Don't use object for locks
private readonly object _lock = new object();
```

### Repository Patterns
**✅ Good:**
```csharp
// Always use typed collections with primary constructors
public class UserRepository(IMongoDatabase database)
{
    private readonly IMongoCollection<UserDocument> _users = database.GetCollection<UserDocument>("users");

    public async Task<UserDocument?> GetByIdAsync(string id)
        => await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
}

// POCOs with BsonElement mapping
public class UserDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("cpu")]
    public int Cpu { get; set; }
}
```

**❌ Bad:**
```csharp
// Don't use BsonDocument (except migrations)
public class UserRepository(IMongoDatabase database)
{
    private readonly IMongoCollection<BsonDocument> _users = database.GetCollection<BsonDocument>("users");

    public async Task<BsonDocument?> GetByIdAsync(string id)
        => await _users.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefaultAsync();
}

// Don't use classic constructor syntax
public class UserRepository
{
    private readonly IMongoCollection<UserDocument> _users;

    public UserRepository(IMongoDatabase database)
    {
        _users = database.GetCollection<UserDocument>("users");
    }
}
```

### Testing Patterns
**✅ Good:**
```csharp
// Integration tests: Use Testcontainers with primary constructors
public class UserEndpointTests(IntegrationTestHarness harness)
    : IClassFixture<IntegrationTestHarness>
{
    [Fact]
    public async Task GetUser_ReturnsUser()
    {
        // Arrange - harness provides containerized Mongo/Redis
        var client = harness.CreateClient();

        // Act
        var response = await client.GetAsync("/api/user/profile");

        // Assert
        response.EnsureSuccessStatusCode();
    }
}

// Unit tests: Use fakes (no Docker)
public class UserServiceTests
{
    [Fact]
    public async Task ValidateUser_ValidUser_ReturnsTrue()
    {
        // Arrange
        var fakeRepo = new FakeUserRepository();
        var service = new UserService(fakeRepo);

        // Act & Assert
        var result = await service.ValidateUserAsync("test-user");
        Assert.True(result);
    }
}
```

**❌ Bad:**
```csharp
// Don't rely on local Docker state
[Fact]
public async Task GetUser_ReturnsUser()
{
    // Assumes local MongoDB is running - BRITTLE
    var client = new MongoClient("mongodb://localhost:27017");
    // ...
}

// Don't use Docker in unit tests
[Fact]
public async Task ValidateUser_ValidUser_ReturnsTrue()
{
    var mongoClient = new MongoClient("mongodb://localhost:27017");  // NO!
    var service = new UserService(mongoClient);
    // ...
}
```

## Storage Architecture

### MongoDB Collections (screeps database)

```javascript
// Core collections
users                   // User accounts, auth, badges
users.code              // Code branches (default, sim, tutorial)
users.memory            // RawMemory.get/set data
users.memory.segments   // RawMemory.segments[0-99]
users.notifications     // Notification queue
users.power_creeps      // Power creep definitions

rooms                   // Room metadata (active, status)
rooms.objects           // Game objects (creeps, structures, sources, etc.)
rooms.terrain           // Terrain tiles (wall/plain/swamp)
rooms.history           // Historical snapshots for charts

market.orders           // Market buy/sell orders
market.stats            // Historical market data

// System collections
servers                 // Server metadata (/api/server/info)
```

### Redis Keys

```
roomsQueue              # Room processing queue (driver)
runtimeQueue            # User code execution queue (driver)
```

### Seed Data

**Location:** `src/docker/mongo-init/`

Seeds run **automatically** when `mongo-data` volume is empty:

- **`seed-server-data.js`** - Server metadata, version info
- **`seed-users.js`** - test-user, ally-user, owned rooms, spawns, power creeps, inbox threads

**Test fixtures:** `src/ScreepsDotNet.Backend.Core/Seeding/SeedDataDefaults.cs`
- Mirrors seed scripts for Testcontainers
- Keep in sync when schemas change

### Reset Workflows

```bash
# Full reset (Mongo + Redis)
docker compose -f src/docker-compose.yml down -v
docker compose -f src/docker-compose.yml up -d

# Mongo only (faster)
docker volume rm screepsdotnet_mongo-data
docker compose -f src/docker-compose.yml up -d mongo

# Verify seeds ran
docker compose -f src/docker-compose.yml logs -f mongo
# Look for: "seed-users.js complete" and "seed-server-data.js complete"
```

## Development Workflow

### Daily Development

```bash
# 1. Ensure infrastructure is running
cd ScreepsDotNet
docker compose -f src/docker-compose.yml up -d

# 2. Start the service you're working on

# HTTP backend:
dotnet run --project src/ScreepsDotNet.Backend.Http/ScreepsDotNet.Backend.Http.csproj

# CLI:
dotnet run --project src/ScreepsDotNet.Backend.Cli/ScreepsDotNet.Backend.Cli.csproj -- --help

# 3. Make changes, test

# 4. Before committing
dotnet format style --exclude-diagnostics IDE0051 IDE0052 IDE0060
dotnet test src/ScreepsDotNet.slnx
git status  # Verify ScreepsNodeJs/ is not included
```

### Manual Testing

**HTTP routes:** Use `.http` files in `src/ScreepsDotNet.Backend.Http/`
- `UserEndpoints.http`
- `PowerCreepEndpoints.http`
- `WorldEndpoints.http`
- etc.

**CLI commands:** Use helper scripts
```bash
# Linux/Mac
./src/cli.sh storage list-users

# Windows
pwsh ./src/cli.ps1 storage list-users
```

### Debugging

**HTTP backend:**
1. Set breakpoint in `src/ScreepsDotNet.Backend.Http/Endpoints/`
2. F5 in IDE or `dotnet run` + attach debugger
3. Send request via `.http` file

**CLI:**
1. Set breakpoint in `src/ScreepsDotNet.Backend.Cli/Commands/`
2. F5 with launch args: `storage list-users`

**Tests:**
```bash
# Run all tests
dotnet test src/ScreepsDotNet.slnx

# Run specific test
dotnet test --filter "FullyQualifiedName~UserServiceTests.ValidateUser_ValidUser_ReturnsTrue"

# Run by category
dotnet test --filter "Category=Integration"
```

**Docker logs:**
```bash
# Mongo
docker compose -f src/docker-compose.yml logs -f mongo

# Redis
docker compose -f src/docker-compose.yml logs -f redis
```

## Common Tasks

### Add a New HTTP Endpoint

```bash
# 1. Add route handler
# Location: src/ScreepsDotNet.Backend.Http/Endpoints/<Area>Endpoints.cs
```

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

```bash
# 2. Add service logic
# Location: src/ScreepsDotNet.Backend.Core/Services/
```

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

```bash
# 3. Add repository method if needed
# Location: src/ScreepsDotNet.Storage.MongoRedis/Repositories/

# 4. Create .http file for manual testing
# Location: src/ScreepsDotNet.Backend.Http/UserEndpoints.http
```

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

```bash
# 5. Add integration test
# Location: src/ScreepsDotNet.Backend.Tests/Endpoints/UserEndpointTests.cs
```

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

```bash
# 6. Update documentation
# - docs/http-endpoints.md (route reference)
# - docs/backend.md (feature coverage list)
```

### Add a New CLI Command

```bash
# 1. Add command class
# Location: src/ScreepsDotNet.Backend.Cli/Commands/<Area>/<CommandName>Command.cs
```

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

```bash
# 2. Register in CLI app
# Location: src/ScreepsDotNet.Backend.Cli/Program.cs
```

```csharp
app.Configure(config =>
{
    config.AddBranch("user", user =>
    {
        user.AddCommand<ListUsersCommand>("list");
    });
});
```

```bash
# 3. Update documentation
# - docs/cli.md (command reference)

# 4. Test
dotnet run --project src/ScreepsDotNet.Backend.Cli/ScreepsDotNet.Backend.Cli.csproj -- user list --format table
```

### Add a New Storage Collection

```bash
# 1. Define POCO
# Location: src/ScreepsDotNet.Storage.MongoRedis/Repositories/Documents/
```

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

```bash
# 2. Add repository interface + implementation
# Location: src/ScreepsDotNet.Storage.MongoRedis/Repositories/
```

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

```bash
# 3. Register in DI
# Location: src/ScreepsDotNet.Storage.MongoRedis/ServiceCollectionExtensions.cs
```

```csharp
services.AddSingleton<IAchievementRepository, MongoAchievementRepository>();
```

```bash
# 4. Add seed data
# Location: src/docker/mongo-init/seed-users.js (or new file)
```

```javascript
db.achievements.insertMany([
    {
        userId: testUserId,
        type: "first_spawn",
        unlocked: new Date()
    }
]);
```

```bash
# 5. Add to test fixtures
# Location: src/ScreepsDotNet.Backend.Core/Seeding/SeedDataDefaults.cs
```

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

### Update Seed Data

```bash
# 1. Modify seed scripts
# Location: src/docker/mongo-init/*.js

# 2. Update SeedDataDefaults
# Location: src/ScreepsDotNet.Backend.Core/Seeding/SeedDataDefaults.cs

# 3. Reset volumes
docker volume rm screepsdotnet_mongo-data
docker compose -f src/docker-compose.yml up -d mongo

# 4. Verify
docker compose -f src/docker-compose.yml logs -f mongo
dotnet test --filter "Category=Integration"
```

### Troubleshoot Common Issues

**Issue: DLL is locked during build**
```bash
# Stop all dotnet run instances
pkill -f "dotnet run"

# Then rebuild
dotnet build src/ScreepsDotNet.slnx
```

**Issue: Tests fail with "cannot connect to MongoDB"**
```bash
# Ensure Docker is running
docker ps

# Testcontainers should auto-start containers
# Check test output for container startup logs

# If still failing, try running Docker compose first
docker compose -f src/docker-compose.yml up -d
```

**Issue: Seed data not loading**
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

**Issue: Git shows ScreepsNodeJs/ changes**
```bash
# You're in the wrong directory
cd ..  # Go to repo root
cd ScreepsDotNet  # Enter solution directory
git status  # Should NOT show ScreepsNodeJs/
```

**Issue: Implicit usings not working**
```bash
# Rebuild solution to refresh SDK settings
dotnet clean src/ScreepsDotNet.slnx
dotnet build src/ScreepsDotNet.slnx

# Check Directory.Build.props for ImplicitUsings setting
```

## Subsystem Navigation

This file provides **solution-wide** context. For subsystem-specific details:

### Driver (Runtime Coordination)
**File:** `src/ScreepsDotNet.Driver/CLAUDE.md` ✅

**When to read:**
- Adding processor logic (intent handlers)
- Modifying runtime execution (V8/ClearScript)
- Queue/scheduler changes
- Pathfinder integration
- Telemetry/observability
- Bulk mutation patterns

**Key topics:**
- D1-D10 roadmap (D10 in progress)
- Code patterns (✅/❌ bulk writers, telemetry, DI)
- Common tasks (add processor handler, wire telemetry, debug runtime)
- Integration contracts (Engine consumes driver abstractions)

### Engine (Simulation Kernel)
**File:** `src/ScreepsDotNet.Engine/CLAUDE.md` ✅

**When to read:**
- Adding intent handlers (creep, structure, controller, combat)
- Porting Node.js engine mechanics
- Testing parity with legacy engine
- Working with game simulation logic
- Understanding Engine↔Driver data flow

**Key topics:**
- E1-E8 roadmap (E2 in progress - handler backlog)
- 🚨 CRITICAL: NEVER access Mongo/Redis directly (use Driver abstractions)
- Code patterns (✅ IRoomStateProvider vs ❌ IMongoDatabase)
- Common tasks (add intent handler, test parity, debug mutations)
- Active work: E2.3 handler backlog (controller, resource I/O, labs, power)

### Native Pathfinder (C++ P/Invoke)
**File:** `src/native/pathfinder/CLAUDE.md` ✅

**When to read:**
- Rebuilding native binaries (Linux/Windows/macOS × x64/arm64)
- Updating parity baselines (Node.js vs .NET comparison)
- Adding regression fixtures
- Debugging cross-platform build failures
- Verifying GitHub release binaries

**Key topics:**
- Build instructions per platform (./build.sh, build.ps1)
- Parity testing workflow (100% match with Node.js pathfinder)
- Baseline refresh process (dotnet test /p:RefreshPathfinderBaselines=true)
- CI/CD pipeline (GitHub Actions builds all RIDs)
- P/Invoke patterns (C ABI ↔ C# marshaling)

## Current Focus (High-Level)

1. **Backend HTTP/CLI** - Shard-aware write APIs, intent/bot tooling parity
2. **Driver (D6-D10)** - See `src/ScreepsDotNet.Driver/CLAUDE.md` for details
3. **Engine (E2-E8)** - See `src/ScreepsDotNet.Engine/CLAUDE.md` for details
4. **Documentation** - Keep docs in sync with feature changes

**Detailed roadmaps live in subsystem CLAUDE.md files.**

## Anti-Patterns to Avoid

❌ **Don't:**
- Use explicit types instead of `var` (e.g., `int count = 0;` instead of `var count = 0;`)
- Use `new List<T>()` instead of collection expressions `[]`
- Use classic constructor syntax instead of primary constructors
- Use block-scoped namespaces (use file-scoped: `namespace Foo;`)
- Omit "Async" suffix from async methods
- Omit accessibility modifiers (public/private/internal/etc)
- Use wrong modifier order (must be: public, private, protected, internal, static, extern, new, virtual, abstract, sealed, override, readonly, unsafe, volatile, async)
- Use BCL type names (`String`, `Int32`) instead of keywords (`string`, `int`)
- Use `== null` or `!= null` (use `is null` or `is not null`)
- Use `as` + null check instead of pattern matching (`if (obj is User user)`)
- Use `this.` or static class qualifiers for members in same class
- Wrap lines under 185 characters (keep on one line if under limit)
- Omit trailing commas in multi-line collections/arrays
- Add trailing commas in single-line collections/arrays
- Omit braces for multi-line control flow statements
- Declare variables before `out` parameters (use inline: `out var value`)
- Repeat type when evident (`UserService service = new UserService()` use `new()`)
- Use negated conditions in ternary operators (flip condition and swap values: `success ? a : b` not `!success ? b : a`)
- Return ternary expressions directly - applies to ALL ternaries (simple, multi-line, nested, complex) - always assign to variable first: `var x = a ? b : c; return x;` not `return a ? b : c;`
- Add `using System;` or other implicit usings manually
- Use `object` for locks (use `Lock`)
- Use `BsonDocument` in repositories (use typed POCOs)
- Put `=>` on same line for methods (put on new line)
- Access Mongo/Redis directly in Driver/Engine (use abstractions)
- Run tests against local Docker (use Testcontainers)
- Mix `ScreepsNodeJs/` changes with `ScreepsDotNet/` changes
- Build while `dotnet run` is active
- Create TODO comments instead of tracking in roadmaps
- Duplicate documentation between files
- Mix constant types randomly in endpoint classes (follow organization pattern: value constants → messages → endpoint names → query params → defaults → numeric arrays → limits → complex objects)
- Forget to add `#pragma warning disable IDE0051, IDE0052` for constants used ONLY in attribute parameters (see "IDE0051/IDE0052 False Positives" section)
- Run `dotnet format style` without exclusions (always use `--exclude-diagnostics IDE0051 IDE0052 IDE0060`)

✅ **Do:**
- Use `var` for ALL variable declarations
- Use collection expressions `[]` for collections
- Use primary constructors for all classes with dependencies
- Use file-scoped namespaces (`namespace Foo;`)
- Suffix all async methods with "Async"
- Specify all accessibility modifiers explicitly
- Use correct modifier order
- Use language keywords (`string`, `int`, `object`, `bool`)
- Use `is null` and `is not null` for null checks
- Use pattern matching (`if (obj is User user)`)
- Avoid `this.` and static qualifiers in same class
- Keep lines under 185 chars without wrapping (only wrap if exceeds limit)
- Add trailing commas in multi-line collections
- Use braces for multi-line control flow
- Declare variables inline with `out` parameters
- Use target-typed `new()` when type is evident
- Use expression-bodied members with `=>` on new line
- Use positive conditions in ternary operators (not negated)
- Assign ALL ternary expressions (simple, multi-line, nested, complex) to variables before returning them
- Use Context7 MCP for library documentation proactively
- Run `dotnet format style --exclude-diagnostics IDE0051 IDE0052 IDE0060` before committing
- Use Testcontainers for integration tests
- Update docs when changing functionality
- Check `git status` from `ScreepsDotNet/` directory
- Follow repository patterns shown above
- Stop `dotnet run` before `dotnet build`
- Keep configuration in sync (appsettings.json, appsettings.Development.json)
- Organize constants in endpoint classes by type (see "Constant Organization in Endpoint Classes" section)
- Protect constants used ONLY in attribute parameters with `#pragma warning disable IDE0051, IDE0052` (see "IDE0051/IDE0052 False Positives" section)
- Run `dotnet format style --exclude-diagnostics IDE0051 IDE0052 IDE0060` to avoid deleting attribute parameter constants

## Documentation Map

### For AI Context
- **This file** - Solution-wide patterns and workflows
- `src/ScreepsDotNet.Driver/CLAUDE.md` - Driver subsystem
- `src/ScreepsDotNet.Engine/CLAUDE.md` - Engine subsystem
- `src/native/pathfinder/CLAUDE.md` - Pathfinder subsystem

### For Human Readers
- `README.md` - Project overview
- `docs/getting-started.md` - Setup tutorial
- `docs/backend.md` - HTTP API coverage
- `docs/http-endpoints.md` - Route reference
- `docs/cli.md` - CLI command reference
- `docs/driver.md` - Driver design overview
- `docs/README.md` - Documentation ownership map

## When Stuck

1. Check subsystem CLAUDE.md (Driver, Engine, Pathfinder)
2. Check `docs/` for detailed design documentation
3. Use Context7 MCP for library/API documentation
4. Search codebase for similar patterns (`rg "pattern" -n src/`)
5. Check test files for usage examples
6. Ask user for clarification

## Maintenance

**Update this file when:**
- Solution-wide coding standards change
- New projects added to solution
- Storage schema changes (major)
- Development workflow changes
- Critical rules change

**Keep it focused:**
- This is for working context, not tutorials
- Tutorials belong in `docs/`
- Subsystem details belong in subsystem CLAUDE.md
- Keep under 500 lines total

**Last Updated:** 2026-01-17
