# ScreepsDotNet.Driver - Claude Context

## Purpose

Port of the legacy Screeps Node.js driver into managed .NET. Provides queue infrastructure, JavaScript runtime coordination (ClearScript/V8), bulk writers, pathfinding, and loop orchestration (main/runner/processor) for executing user code and applying game simulation logic.

## Dependencies

### This Subsystem Depends On
- `ScreepsDotNet.Storage.MongoRedis` - Data access (repositories, bulk writers)
- `ScreepsDotNet.Backend.Core` - Shared contracts, DTOs, abstractions
- `src/native/pathfinder/` - C++ pathfinder P/Invoke bindings
- External: ClearScript (V8 JavaScript runtime), MongoDB.Driver, StackExchange.Redis

### These Depend On This Subsystem
- `ScreepsDotNet.Engine` - Consumes driver abstractions (snapshot providers, mutation writers)
- `ScreepsDotNet.Backend.Cli` - May invoke driver operations (future)
- Tests: `ScreepsDotNet.Driver.Tests`, `ScreepsDotNet.Engine.Tests`

## Critical Rules

- ❌ **NEVER** call Mongo/Redis repositories directly - always use `IBulkWriterFactory` or driver services
- ❌ **NEVER** let loops (main/runner/processor) call services directly - always go through `IDriverLoopHooks`
- ❌ **NEVER** add `using System;` or other implicit usings (solution-wide setting)
- ❌ **NEVER** use `object` for locks (use `Lock` type)
- ❌ **NEVER** use old collection syntax for empty collections (use `[]`)
- ✅ **ALWAYS** use primary constructors for simple dependency injection
- ✅ **ALWAYS** put new constants under `ScreepsDotNet.Driver.Abstractions.Shared.Constants`
- ✅ **ALWAYS** wire bulk mutations through `IBulkWriterFactory`
- ✅ **ALWAYS** emit telemetry events through `IDriverLoopHooks`, not directly to sinks
- ✅ **ALWAYS** update relevant doc under `src/ScreepsDotNet.Driver/docs/` when changing behavior

## Code Structure

```
src/ScreepsDotNet.Driver/
├── CLAUDE.md                                    # This file
├── docs/                                        # Design documentation
│   ├── DriverApi.md                            # IScreepsDriver surface
│   ├── QueueAndScheduler.md                    # Queue/worker patterns
│   ├── RuntimeLifecycle.md                     # V8 runtime coordination
│   ├── BulkWriters.md                          # Bulk mutation patterns
│   ├── Pathfinder.md                           # Native pathfinder integration
│   ├── ConfigAndEvents.md                      # Config emitter design
│   ├── HistoryAndNotifications.md              # History/notification pipeline
│   └── EngineContracts.md                      # Engine data contracts
├── Services/
│   ├── Queue/                                  # Redis queue service
│   ├── Runtime/                                # ClearScript/V8 coordination
│   ├── Pathfinding/                            # Native pathfinder wrapper
│   ├── History/                                # Room history pipeline
│   ├── Notification/                           # Notification delivery
│   └── Processor/                              # Intent application logic
├── Abstractions/
│   ├── IScreepsDriver.cs                       # Top-level driver interface
│   ├── IDriverLoopHooks.cs                     # Telemetry/event integration point
│   └── Shared/Constants/                       # Intent keys, object types, etc.
└── ScreepsDotNet.Driver.Tests/
    ├── Pathfinding/PathfinderNativeIntegrationTests.cs
    ├── Runtime/V8RuntimeSandboxTests.cs
    └── ...
```

## Coding Patterns

### Dependency Injection (Primary Constructors)
**✅ Good:**
```csharp
// Use primary constructors for simple DI
public class UserService(IUserRepository userRepository, ILogger<UserService> logger)
{
    public async Task<User?> GetUserAsync(string id) =>
        await userRepository.GetByIdAsync(id);
}
```

**❌ Bad:**
```csharp
// Don't use old constructor syntax when primary constructor works
public class UserService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository userRepository, ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<User?> GetUserAsync(string id) =>
        await _userRepository.GetByIdAsync(id);
}
```

### Bulk Mutations (Never Direct Repository Access)
**✅ Good:**
```csharp
// Always use IBulkWriterFactory for mutations
public class ProcessorHandler(IBulkWriterFactory bulkWriterFactory)
{
    public async Task ApplyIntentAsync(Intent intent)
    {
        var bulkWriter = bulkWriterFactory.Create();

        // Queue mutations
        bulkWriter.Objects.Update(
            objectId,
            Builders<RoomObject>.Update.Set(o => o.Energy, newEnergy)
        );

        // Flush all mutations in one batch
        await bulkWriter.FlushAsync();
    }
}
```

**❌ Bad:**
```csharp
// NEVER call repositories directly
public class ProcessorHandler(IRoomObjectRepository repository)
{
    public async Task ApplyIntentAsync(Intent intent)
    {
        // ❌ Direct repository access bypasses bulk batching
        await repository.UpdateAsync(
            objectId,
            Builders<RoomObject>.Update.Set(o => o.Energy, newEnergy)
        );
    }
}
```

### Telemetry (Always Through IDriverLoopHooks)
**✅ Good:**
```csharp
// Emit telemetry through hooks, not directly to sinks
public class RunnerLoopWorker(
    IDriverLoopHooks hooks,
    IQueueService queueService)
{
    public async Task ExecuteTickAsync()
    {
        var userId = await queueService.DequeueAsync("runtimeQueue");

        try
        {
            await ExecuteUserCodeAsync(userId);

            // Emit success event through hooks
            hooks.OnRuntimeExecuted(new RuntimeTelemetryEvent
            {
                UserId = userId,
                CpuUsed = 100,
                Success = true
            });
        }
        catch (Exception ex)
        {
            // Emit error through hooks
            hooks.OnRuntimeError(userId, ex);
        }
    }
}
```

**❌ Bad:**
```csharp
// NEVER emit directly to telemetry sinks
public class RunnerLoopWorker(
    IRuntimeTelemetrySink sink,  // ❌ Don't inject sinks directly
    IQueueService queueService)
{
    public async Task ExecuteTickAsync()
    {
        var userId = await queueService.DequeueAsync("runtimeQueue");

        // ❌ Bypasses hooks abstraction
        await sink.RecordRuntimeEventAsync(new RuntimeTelemetryEvent { ... });
    }
}
```

### Constants (Centralized in Abstractions)
**✅ Good:**
```csharp
// Define constants in Shared.Constants namespace
namespace ScreepsDotNet.Driver.Abstractions.Shared.Constants;

public static class IntentKeys
{
    public const string Move = "move";
    public const string Harvest = "harvest";
    public const string Transfer = "transfer";
}

// Usage
public class ProcessorHandler
{
    public void ApplyIntent(Intent intent)
    {
        if (intent.Type == IntentKeys.Move)
        {
            // ...
        }
    }
}
```

**❌ Bad:**
```csharp
// Don't use magic strings or local constants
public class ProcessorHandler
{
    public void ApplyIntent(Intent intent)
    {
        if (intent.Type == "move")  // ❌ Magic string
        {
            // ...
        }
    }
}
```

### Lock Primitives
**✅ Good:**
```csharp
// Use Lock type (not object)
public class RuntimePool
{
    private readonly Lock _lock = new();
    private readonly Queue<V8Runtime> _available = [];

    public V8Runtime Acquire()
    {
        lock (_lock)
        {
            return _available.Count > 0
                ? _available.Dequeue()
                : CreateNew();
        }
    }
}
```

**❌ Bad:**
```csharp
// Don't use object for locks
public class RuntimePool
{
    private readonly object _lock = new object();  // ❌
    private readonly Queue<V8Runtime> _available = new Queue<V8Runtime>();  // ❌ Old syntax
}
```

### One-Line If Statements
**✅ Good:**
```csharp
// Omit braces for one-liners
if (user == null) return;
if (energy < cost) throw new InsufficientEnergyException();
if (intent.Type == IntentKeys.Move) ProcessMove(intent);
```

**❌ Bad:**
```csharp
// Don't use braces for one-liners
if (user == null)
{
    return;
}

if (energy < cost)
{
    throw new InsufficientEnergyException();
}
```

## Current Status

### ✅ Completed (D1-D9)
- **D1: Driver API Inventory** - `IScreepsDriver`, `IDriverLoopHooks` surface defined
- **D2: Storage Adapters** - Shared Mongo/Redis infrastructure
- **D3: Sandbox** - ClearScript/V8 runtime, module loader, CPU/heap guards, pooling
- **D4: Queues & Scheduler** - Redis-backed queues, worker scheduler, telemetry stages
- **D5: Bulk Writers** - `IBulkWriterFactory` for batched Mongo mutations
- **D6: Pathfinder** - Native solver integration, managed fallback removed (Jan 13, 2026)
- **D7: Config/Events** - Config emitter, environment service, tick knobs
- **D8: Runtime Lifecycle** - Runtime coordinator, sandbox pooling, watchdog, throttling
- **D9: History & Notifications** - Room history pipeline, notification throttling

### 🔄 In Progress (D10)
- **D10: Engine Contracts** - Room/global snapshot providers, mutation dispatcher wiring
  - ✅ `RoomStateProvider` / `GlobalStateProvider` expose read-only snapshots
  - ✅ `RoomMutationWriterFactory` / `UserMemorySink` handle write operations
  - 🔄 Regression tests for parity validation
  - 📋 Final integration with Engine subsystem

### 📋 Next Steps
- Complete D10 engine contract validation
- Expand processor handlers (controller intents, resource I/O, power actions)
- Monitor pathfinder parity baselines across all RIDs
- Keep telemetry/observability documentation current

## Roadmap (D1-D10)

| ID | Status | Summary | Design Doc |
|----|--------|---------|------------|
| D1 | ✅ | Driver API inventory - `IScreepsDriver`, `IDriverLoopHooks` | [DriverApi.md](docs/DriverApi.md) |
| D2 | ✅ | Storage adapters - Mongo/Redis infrastructure | [StorageAdapters.md](docs/StorageAdapters.md) |
| D3 | ✅ | Sandbox - ClearScript/V8 runtime, module loader | [SandboxOptions.md](docs/SandboxOptions.md) |
| D4 | ✅ | Queues & scheduler - Redis queues, worker orchestration | [QueueAndScheduler.md](docs/QueueAndScheduler.md) |
| D5 | ✅ | Bulk writers - `IBulkWriterFactory` batching | [BulkWriters.md](docs/BulkWriters.md) |
| D6 | ✅ | Pathfinder - Native solver, P/Invoke bindings | [Pathfinder.md](docs/Pathfinder.md) |
| D7 | ✅ | Config/events - Config emitter, environment service | [ConfigAndEvents.md](docs/ConfigAndEvents.md) |
| D8 | ✅ | Runtime lifecycle - Runtime coordinator, pooling, watchdog | [RuntimeLifecycle.md](docs/RuntimeLifecycle.md) |
| D9 | ✅ | History & notifications - Room history, notification delivery | [HistoryAndNotifications.md](docs/HistoryAndNotifications.md) |
| D10 | 🔄 | Engine contracts - Snapshot providers, mutation dispatcher | [EngineContracts.md](docs/EngineContracts.md) |

**Full roadmap:** See `docs/driver.md` for detailed status and cross-references.

## Common Tasks

### Add a New Processor Handler

```bash
# 1. Define the handler
# Location: src/ScreepsDotNet.Driver/Services/Processor/Handlers/
```

```csharp
// Example: Creep harvest intent handler
public class HarvestIntentHandler(IBulkWriterFactory bulkWriterFactory)
{
    public async Task ProcessAsync(Intent intent, RoomSnapshot room)
    {
        var creep = room.Objects[intent.ObjectId];
        var target = room.Objects[intent.TargetId];

        // Calculate harvest amount
        var harvestAmount = CalculateHarvest(creep, target);

        // Queue mutations via bulk writer
        var bulkWriter = bulkWriterFactory.Create();

        bulkWriter.Objects.Update(
            target.Id,
            Builders<RoomObject>.Update.Inc(o => o.Energy, -harvestAmount)
        );

        bulkWriter.Objects.Update(
            creep.Id,
            Builders<RoomObject>.Update.Inc(o => o.Store["energy"], harvestAmount)
        );

        // Flush all changes
        await bulkWriter.FlushAsync();
    }

    private int CalculateHarvest(RoomObject creep, RoomObject target)
    {
        // Harvest calculation logic
        return Math.Min(target.Energy, creep.WorkPower * 2);
    }
}
```

```bash
# 2. Register in DI
# Location: src/ScreepsDotNet.Driver/ServiceCollectionExtensions.cs
```

```csharp
services.AddSingleton<HarvestIntentHandler>();
```

```bash
# 3. Wire into processor loop
# Location: src/ScreepsDotNet.Driver/Services/Processor/ProcessorLoopWorker.cs
```

```csharp
public class ProcessorLoopWorker(
    HarvestIntentHandler harvestHandler,
    // ... other handlers
    )
{
    public async Task ProcessIntentsAsync(List<Intent> intents, RoomSnapshot room)
    {
        foreach (var intent in intents)
        {
            switch (intent.Type)
            {
                case IntentKeys.Harvest:
                    await harvestHandler.ProcessAsync(intent, room);
                    break;
                // ... other cases
            }
        }
    }
}
```

```bash
# 4. Add tests
# Location: src/ScreepsDotNet.Driver.Tests/Processors/HarvestIntentHandlerTests.cs
```

```csharp
public class HarvestIntentHandlerTests
{
    [Fact]
    public async Task ProcessAsync_CreepHarvestsSource_UpdatesEnergy()
    {
        // Arrange
        var bulkWriterFactory = new FakeBulkWriterFactory();
        var handler = new HarvestIntentHandler(bulkWriterFactory);

        var intent = new Intent
        {
            Type = IntentKeys.Harvest,
            ObjectId = "creep1",
            TargetId = "source1"
        };

        var room = new RoomSnapshot
        {
            Objects = new Dictionary<string, RoomObject>
            {
                ["creep1"] = new() { Id = "creep1", WorkPower = 5, Store = new() },
                ["source1"] = new() { Id = "source1", Energy = 1000 }
            }
        };

        // Act
        await handler.ProcessAsync(intent, room);

        // Assert
        var bulkWriter = bulkWriterFactory.LastCreated;
        Assert.Equal(2, bulkWriter.Objects.Updates.Count);  // Source and creep updated
    }
}
```

```bash
# 5. Update documentation
# - Add to docs/driver.md if it's a major feature
# - Update this CLAUDE.md if it changes patterns
```

### Add Telemetry to a Worker Loop

```bash
# 1. Define event in IDriverLoopHooks
# Location: src/ScreepsDotNet.Driver/Abstractions/IDriverLoopHooks.cs
```

```csharp
public interface IDriverLoopHooks
{
    // Existing events...

    // Add new event
    void OnCustomEvent(string message, Dictionary<string, object> metadata);
}
```

```bash
# 2. Emit from worker
# Location: src/ScreepsDotNet.Driver/Services/YourWorker.cs
```

```csharp
public class YourWorker(IDriverLoopHooks hooks)
{
    public async Task DoWorkAsync()
    {
        try
        {
            // Do work...

            // Emit success event
            hooks.OnCustomEvent("Work completed", new Dictionary<string, object>
            {
                ["duration"] = stopwatch.ElapsedMilliseconds,
                ["itemsProcessed"] = count
            });
        }
        catch (Exception ex)
        {
            hooks.OnError("CustomWorker", ex);
        }
    }
}
```

```bash
# 3. Handle in telemetry listener
# Location: src/ScreepsDotNet.Driver/Services/Telemetry/ObservabilityTelemetryListener.cs
```

```csharp
public class ObservabilityTelemetryListener : IDriverLoopHooks
{
    public void OnCustomEvent(string message, Dictionary<string, object> metadata)
    {
        if (!_options.Enabled) return;

        _exporter.Export(new TelemetryEvent
        {
            Type = "CustomEvent",
            Message = message,
            Metadata = metadata,
            Timestamp = DateTime.UtcNow
        });
    }
}
```

```bash
# 4. Update documentation
# Location: src/ScreepsDotNet.Driver/docs/RuntimeLifecycle.md or docs/runtime-telemetry.md
```

### Debug Runtime Execution Issues

**Problem: User code times out or fails**

```bash
# 1. Check runtime telemetry
# Look for timeout/script error flags in IDriverLoopHooks events
```

```csharp
// Check RuntimeTelemetryMonitor logs
// Emitted via hooks.OnRuntimeExecuted(...)
```

**Problem: Sandbox pool exhaustion**

```bash
# 2. Check pool metrics
# Location: RuntimeCoordinator pooling logic
```

```csharp
// Look for pool size vs demand in telemetry
// Check if watchdog is forcing cold restarts
```

**Problem: Memory not persisting**

```bash
# 3. Verify mutation pipeline
# Check that user code mutations flow through RuntimeData["memory"]
```

```csharp
// RuntimeCoordinator should call persistence hooks
// Verify IBulkWriterFactory.Users.UpdateMemory is being called
```

**Common fixes:**
- Increase sandbox pool size in config
- Check CPU/heap limits in `IDriverConfig`
- Verify module loader is resolving dependencies
- Check for watchdog throttling (consecutive failures)

### Test Driver Integration

```bash
# Run all driver tests
dotnet test --filter "FullyQualifiedName~ScreepsDotNet.Driver.Tests"

# Run specific subsystem
dotnet test --filter "FullyQualifiedName~PathfinderNativeIntegrationTests"
dotnet test --filter "FullyQualifiedName~V8RuntimeSandboxTests"

# Run with verbose output
dotnet test --filter "FullyQualifiedName~ScreepsDotNet.Driver.Tests" --logger "console;verbosity=detailed"
```

### Update Native Pathfinder

```bash
# See src/native/pathfinder/CLAUDE.md for detailed instructions

# Quick rebuild (for current platform)
cd src/native/pathfinder
./build.sh linux-x64  # or win-x64, osx-arm64, etc.

# Copy to driver runtime directory
cp libscreepspathfinder.so ../../ScreepsDotNet.Driver/runtimes/linux-x64/native/

# Run regression tests
dotnet test --filter "FullyQualifiedName~PathfinderNativeIntegrationTests"
```

## Configuration

### Driver Config (appsettings.json)

```json
{
  "Driver": {
    "RuntimePoolSize": 10,
    "CpuLimit": 100,
    "HeapLimit": 524288000,
    "ModuleCachePath": "./cache/modules",
    "EnableTelemetry": true
  }
}
```

### Environment Variables

- `NativePathfinderSkipDownload` - Skip auto-download of pathfinder binaries (use local)
- `DRIVER_LOG_LEVEL` - Set log verbosity (Debug, Info, Warning, Error)

## Integration Points

### Consuming Storage Layer

```csharp
// Driver services consume storage abstractions, not repositories directly
public class RoomService(
    IRoomRepository roomRepository,
    IRoomObjectRepository objectRepository)
{
    public async Task<RoomSnapshot> GetRoomSnapshotAsync(string roomId)
    {
        var room = await roomRepository.GetByIdAsync(roomId);
        var objects = await objectRepository.GetByRoomIdAsync(roomId);

        return new RoomSnapshot
        {
            RoomId = roomId,
            Objects = objects.ToDictionary(o => o.Id)
        };
    }
}
```

### Providing Data to Engine

```csharp
// Engine consumes driver abstractions, never Mongo/Redis directly
public interface IRoomStateProvider
{
    Task<RoomSnapshot> GetRoomStateAsync(string roomId);
}

public interface IGlobalStateProvider
{
    Task<GlobalSnapshot> GetGlobalStateAsync();
}

// Engine usage (see src/ScreepsDotNet.Engine/CLAUDE.md)
public class EngineProcessor(IRoomStateProvider stateProvider)
{
    public async Task ProcessRoomAsync(string roomId)
    {
        var state = await stateProvider.GetRoomStateAsync(roomId);
        // Process intents using state...
    }
}
```

## Testing Strategy

### Unit Tests
- **Location:** `src/ScreepsDotNet.Driver.Tests/`
- **Pattern:** Use fakes for dependencies, no external services
- **Coverage:** Processor handlers, telemetry listeners, queue logic

### Integration Tests
- **Location:** `src/ScreepsDotNet.Driver.Tests/`
- **Dependencies:** Testcontainers for Mongo/Redis
- **Coverage:** Runtime execution, pathfinder parity, bulk writer batching

**Example:**
```csharp
public class RuntimeExecutionIntegrationTests : IClassFixture<DriverTestHarness>
{
    private readonly DriverTestHarness _harness;

    public RuntimeExecutionIntegrationTests(DriverTestHarness harness)
    {
        _harness = harness;
    }

    [Fact]
    public async Task ExecuteUserCode_ValidScript_ReturnsSuccess()
    {
        // Arrange
        var runtime = _harness.CreateRuntime();
        var userCode = "module.exports.loop = function() { console.log('tick'); }";

        // Act
        var result = await runtime.ExecuteAsync(userCode);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("tick", result.ConsoleOutput);
    }
}
```

## Performance Considerations

- **Sandbox pooling:** Reuse V8 isolates to avoid startup cost (20-50ms per isolate)
- **Bundle caching:** Cache compiled modules to reduce parse/compile overhead
- **Bulk batching:** Always use `IBulkWriterFactory` to batch Mongo writes (10x faster)
- **Telemetry throttling:** Notification throttling prevents spam (max 20/minute)
- **Watchdog restart:** Cold sandbox restart on repeated failures clears memory leaks

## Known Issues & Workarounds

### Issue: Native pathfinder missing on fresh clone
**Symptom:** `DllNotFoundException: libscreepspathfinder`
**Cause:** Auto-download disabled or network issue
**Workaround:**
```bash
# Manual download (if auto-download fails)
cd src/native/pathfinder
./build.sh linux-x64
# Or download from GitHub release
```

### Issue: V8 isolate leak causing memory growth
**Symptom:** Memory usage grows over time, doesn't recover
**Cause:** Sandbox not properly disposed
**Workaround:** Ensure `RuntimeCoordinator.ReleaseAsync()` is called after execution

### Issue: Queue backlog grows indefinitely
**Symptom:** `runtimeQueue` or `roomsQueue` length keeps increasing
**Cause:** Worker not consuming fast enough, or workers crashed
**Fix:** Check worker health, increase worker count, or tune batch sizes

## Reference Documentation

### Design Docs (Detailed)
- `docs/DriverApi.md` - Driver surface contract
- `docs/QueueAndScheduler.md` - Queue/worker patterns
- `docs/RuntimeLifecycle.md` - V8 runtime coordination
- `docs/BulkWriters.md` - Bulk mutation patterns
- `docs/Pathfinder.md` - Native pathfinder integration
- `docs/ConfigAndEvents.md` - Config emitter design
- `docs/HistoryAndNotifications.md` - History/notification pipeline
- `docs/EngineContracts.md` - Engine data contracts

### Related Subsystems
- `../../CLAUDE.md` - Solution-wide patterns
- `../ScreepsDotNet.Engine/CLAUDE.md` - Engine subsystem (consumes driver)
- `../native/pathfinder/CLAUDE.md` - Pathfinder build/release

### External Dependencies
- [ClearScript](https://github.com/microsoft/ClearScript) - V8 JavaScript host
- [MongoDB.Driver](https://www.mongodb.com/docs/drivers/csharp/) - Mongo client
- [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/) - Redis client

## Debugging Tips

**Problem: Can't find where intent is processed**
- **Check:** `Services/Processor/ProcessorLoopWorker.cs` intent switch statement
- **Grep:** `rg "IntentKeys.YourIntent" -n src/`

**Problem: Telemetry not showing up**
- **Check:** `ObservabilityOptions.Enabled` is true
- **Check:** `IDriverLoopHooks` implementation is registered in DI
- **Check:** Exporter is configured (`IObservabilityExporter`)

**Problem: Bulk writer not flushing**
- **Check:** `await bulkWriter.FlushAsync()` is called
- **Check:** No exceptions swallowed during flush
- **Verify:** Mongo is reachable, connection string is correct

**Problem: Module loader can't find dependency**
- **Check:** `RuntimeData["modules"]` contains the module
- **Check:** Module path resolution in `SandboxOptions.md`
- **Enable:** Debug logging to see resolution attempts

## Maintenance

**Update this file when:**
- Adding new processor handlers (update Common Tasks)
- Changing integration contracts (update Integration Points)
- Discovering new patterns/anti-patterns (update Coding Patterns)
- Roadmap milestones shift (update Current Status)
- Performance characteristics change (update Performance Considerations)

**Keep it focused:**
- This is for tactical driver work, not engine design
- Engine-specific details belong in `../ScreepsDotNet.Engine/CLAUDE.md`
- Solution-wide patterns belong in `../../CLAUDE.md`
- Detailed design belongs in `docs/*.md`

**Last Updated:** 2026-01-17
