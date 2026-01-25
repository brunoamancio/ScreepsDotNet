# ScreepsDotNet.Engine - Claude Context

## Purpose

Rebuild the legacy Screeps simulation kernel (processor) in managed .NET. Ports all game mechanics (creep actions, structure logic, combat, market, etc.) from the Node.js engine while maintaining API compatibility with the Driver layer. The Engine consumes Driver abstractions and NEVER accesses Mongo/Redis directly.

**Parity Status:** ✅ 114/114 tests passing (100% core gameplay) - See [`tools/parity-harness/docs/parity-analysis.md`](../../../tools/parity-harness/docs/parity-analysis.md) for comprehensive comparison with Node.js engine.

## Dependencies

### This Subsystem Depends On
- `ScreepsDotNet.Driver` - **ALL data access goes through Driver abstractions**
  - `IRoomStateProvider` - Read room state snapshots
  - `IGlobalStateProvider` - Read global/inter-room state
  - `IRoomMutationWriterFactory` - Write room mutations
  - `IUserMemorySink` - Write user memory
- `ScreepsDotNet.Backend.Core` - Shared DTOs, constants
- External: None (Engine is isolated from external dependencies)

### These Depend On This Subsystem
- Future: Main/runner/processor loops will invoke Engine instead of Node.js engine
- Tests: `ScreepsDotNet.Engine.Tests` validates parity with legacy engine

## Critical Rules

- 🚨 **NEVER EVER call Mongo/Redis directly** - This is the #1 rule for Engine code
- 🚨 **NEVER import MongoDB.Driver or StackExchange.Redis** - If you see these imports in Engine code, it's a bug
- ✅ **ALWAYS consume data via Driver providers** (`IRoomStateProvider`, `IGlobalStateProvider`)
- ✅ **ALWAYS write mutations via Driver writers** (`IRoomMutationWriterFactory`, `IUserMemorySink`)
- ✅ **ALWAYS emit stats/actions through `RoomStatsSink`** for telemetry
- ✅ **ALWAYS follow solution-wide conventions** (implicit usings, primary constructors, `Lock` type, `[]` collections)
- ✅ **ALWAYS test parity against Node.js engine** (E7 validation suite)
- ✅ **ALWAYS update `docs/engine/` when changing mechanics** (data-model.md, e2.md, etc.)

## Code Structure

```
src/ScreepsDotNet.Engine/
├── CLAUDE.md                                    # This file (coding patterns)
├── ../../docs/engine/                           # Plan documents (moved from here)
│   ├── roadmap.md                               # E1-E8 milestones
│   ├── e2.md                             # Handler backlog tracking
│   ├── e5.md                               # E5 blockers & implementation
│   ├── data-model.md                            # Engine data contracts (E2)
│   └── e1.md                        # Node engine API inventory (E1)
├── Services/
│   ├── RoomStateProvider.cs                    # Read room snapshots (wraps Driver)
│   ├── GlobalStateProvider.cs                  # Read global state (wraps Driver)
│   ├── RoomMutationWriterFactory.cs            # Write room mutations (wraps Driver)
│   ├── UserMemorySink.cs                       # Write user memory (wraps Driver)
│   └── Processors/                             # Intent handlers
│       ├── CreepProcessor.cs                   # Creep action handlers
│       ├── StructureProcessor.cs               # Structure logic
│       ├── ControllerProcessor.cs              # Controller upgrade/attack/reserve
│       └── ...
├── Models/
│   ├── RoomSnapshot.cs                         # In-memory room state
│   ├── GlobalSnapshot.cs                       # In-memory global state
│   └── ...
└── ScreepsDotNet.Engine.Tests/
    ├── Parity/                                 # Node.js engine comparison tests
    └── Processors/                             # Intent handler unit tests
```

## Coding Patterns

### Data Access (NEVER Direct DB)

**✅ CORRECT - Always use Driver abstractions:**
```csharp
// Engine service consuming Driver provider
public class CreepProcessor(IRoomStateProvider stateProvider, IRoomMutationWriterFactory mutationFactory)
{
    public async Task ProcessMoveIntentAsync(string roomId, MoveIntent intent)
    {
        // ✅ Read state through Driver
        var roomState = await stateProvider.GetRoomStateAsync(roomId);

        // Process intent using in-memory state
        var creep = roomState.Objects[intent.CreepId];
        var newPosition = CalculateNewPosition(creep.Position, intent.Direction);

        // ✅ Write mutations through Driver
        var writer = mutationFactory.Create(roomId);
        writer.UpdateCreepPosition(intent.CreepId, newPosition);
        await writer.FlushAsync();
    }
}
```

**❌ WRONG - NEVER access storage directly:**
```csharp
// ❌❌❌ NEVER DO THIS ❌❌❌
using MongoDB.Driver;  // ❌ Engine should NEVER import Mongo

public class CreepProcessor(IMongoDatabase database)  // ❌ NEVER inject Mongo
{
    public async Task ProcessMoveIntentAsync(string roomId, MoveIntent intent)
    {
        // ❌ Direct DB access bypasses Driver layer
        var collection = database.GetCollection<RoomObject>("rooms.objects");
        var creep = await collection.Find(o => o.Id == intent.CreepId).FirstOrDefaultAsync();

        // ❌ This breaks the abstraction and coupling
        await collection.UpdateOneAsync(
            Builders<RoomObject>.Filter.Eq(o => o.Id, intent.CreepId),
            Builders<RoomObject>.Update.Set(o => o.Position, newPosition)
        );
    }
}
```

### Reading Room State

**✅ CORRECT:**
```csharp
public class HarvestProcessor(IRoomStateProvider stateProvider, IRoomMutationWriterFactory mutationFactory)
{
    public async Task ProcessHarvestAsync(string roomId, HarvestIntent intent)
    {
        // ✅ Get snapshot from Driver
        var state = await stateProvider.GetRoomStateAsync(roomId);

        // Work with in-memory snapshot
        var creep = state.Objects[intent.CreepId];
        var source = state.Objects[intent.SourceId];

        var harvestAmount = CalculateHarvest(creep, source);

        // ✅ Write through Driver
        var writer = mutationFactory.Create(roomId);
        writer.UpdateSourceEnergy(intent.SourceId, source.Energy - harvestAmount);
        writer.UpdateCreepStore(intent.CreepId, "energy", creep.Store["energy"] + harvestAmount);
        await writer.FlushAsync();
    }
}
```

**❌ WRONG:**
```csharp
// ❌ NEVER inject repositories directly
public class HarvestProcessor(IRoomObjectRepository repository)  // ❌ NO!
{
    public async Task ProcessHarvestAsync(string roomId, HarvestIntent intent)
    {
        // ❌ Direct repository access
        var creep = await repository.GetByIdAsync(intent.CreepId);
        var source = await repository.GetByIdAsync(intent.SourceId);

        // ❌ Bypasses Driver mutation batching
        await repository.UpdateAsync(intent.SourceId, ...);
        await repository.UpdateAsync(intent.CreepId, ...);
    }
}
```

### Reading Global State

**✅ CORRECT:**
```csharp
public class MarketProcessor(IGlobalStateProvider globalStateProvider)
{
    public async Task ProcessMarketOrderAsync(MarketOrder order)
    {
        // ✅ Get global snapshot through Driver
        var globalState = await globalStateProvider.GetGlobalStateAsync();

        // Access inter-room data from snapshot
        var buyOrders = globalState.Market.GetBuyOrders(order.ResourceType);
        var seller = globalState.Users[order.SellerId];

        // Process market logic...
    }
}
```

**❌ WRONG:**
```csharp
// ❌ NEVER inject market repository directly
public class MarketProcessor(IMarketOrderRepository marketRepo, IUserRepository userRepo)  // ❌ NO!
{
    public async Task ProcessMarketOrderAsync(MarketOrder order)
    {
        // ❌ Direct DB queries from Engine
        var buyOrders = await marketRepo.GetBuyOrdersAsync(order.ResourceType);
        var seller = await userRepo.GetByIdAsync(order.SellerId);
    }
}
```

### Emitting Stats/Telemetry

**✅ CORRECT:**
```csharp
public class SpawnProcessor(
    IRoomStateProvider stateProvider,
    IRoomMutationWriterFactory mutationFactory,
    RoomStatsSink statsSink)  // ✅ Inject stats sink
{
    public async Task ProcessSpawnAsync(string roomId, SpawnIntent intent)
    {
        var state = await stateProvider.GetRoomStateAsync(roomId);
        var spawn = state.Objects[intent.SpawnId];

        // Create creep...
        var writer = mutationFactory.Create(roomId);
        writer.CreateCreep(newCreep);
        await writer.FlushAsync();

        // ✅ Emit stats through sink
        statsSink.RecordSpawn(roomId, intent.SpawnId, newCreep.Id, intent.Body.Length);
    }
}
```

**❌ WRONG:**
```csharp
// ❌ Don't skip telemetry
public class SpawnProcessor(IRoomStateProvider stateProvider, IRoomMutationWriterFactory mutationFactory)
{
    public async Task ProcessSpawnAsync(string roomId, SpawnIntent intent)
    {
        // Create creep...
        await writer.FlushAsync();

        // ❌ No stats emitted - observers can't track spawns
    }
}
```

### Notification Sink

**✅ CORRECT:**
```csharp
// Engine step using notification sink
public class CombatResolutionStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        // ... damage calculation

        if (hits > 0 && obj.NotifyWhenAttacked == true && !string.IsNullOrWhiteSpace(obj.UserId))
        {
            context.Notifications.SendAttackedNotification(obj.UserId, obj.Id, obj.RoomName);
        }

        // Notifications flush at end of tick
    }
}
```

**❌ WRONG:**
```csharp
// NEVER access INotificationService directly from Engine
public class CombatResolutionStep(INotificationService notificationService)  // ❌ NO!
{
    public async Task ExecuteAsync(...)
    {
        // ❌ Bypasses sink abstraction
        await notificationService.SendNotificationAsync(...);
    }
}
```

### Dependency Injection (Primary Constructors)

**✅ CORRECT:**
```csharp
// Use primary constructors for DI
public class ControllerProcessor(
    IRoomStateProvider stateProvider,
    IRoomMutationWriterFactory mutationFactory,
    RoomStatsSink statsSink)
{
    public async Task ProcessUpgradeAsync(string roomId, UpgradeIntent intent)
    {
        var state = await stateProvider.GetRoomStateAsync(roomId);
        // ... process upgrade
    }
}
```

**❌ WRONG:**
```csharp
// Don't use old constructor syntax
public class ControllerProcessor
{
    private readonly IRoomStateProvider _stateProvider;
    private readonly IRoomMutationWriterFactory _mutationFactory;

    public ControllerProcessor(
        IRoomStateProvider stateProvider,
        IRoomMutationWriterFactory mutationFactory)
    {
        _stateProvider = stateProvider;
        _mutationFactory = mutationFactory;
    }
}
```

## Current Status

**Engine Complete! 🎉** - 10/10 milestones (E1-E10), 114/114 parity tests passing (100%)

For detailed roadmap and status tracking, see:
- **Roadmap:** `../../docs/engine/roadmap.md` (E1-E10 milestones, all complete ✅)
- **Parity Analysis:** `../../../tools/parity-harness/docs/parity-analysis.md` (comprehensive Node.js vs .NET comparison)
- **Divergence Analysis:** `../../../tools/parity-harness/docs/parity-divergences.md` (documented optimizations)
- **E10 Details:** `../../docs/engine/e10.md` (final parity test coverage)

## Common Tasks

### Add a New Intent Handler

```bash
# 1. Define the handler
# Location: src/ScreepsDotNet.Engine/Services/Processors/
```

```csharp
// Example: Controller upgrade intent
public class ControllerUpgradeHandler(
    IRoomStateProvider stateProvider,
    IRoomMutationWriterFactory mutationFactory,
    RoomStatsSink statsSink)
{
    public async Task ProcessAsync(string roomId, UpgradeIntent intent)
    {
        // ✅ 1. Read state through Driver
        var state = await stateProvider.GetRoomStateAsync(roomId);

        var creep = state.Objects[intent.CreepId];
        var controller = state.Objects[intent.ControllerId];

        // 2. Calculate upgrade amount
        var upgradeAmount = CalculateUpgrade(creep, controller);

        // ✅ 3. Write mutations through Driver
        var writer = mutationFactory.Create(roomId);

        // Deduct energy from creep
        writer.UpdateCreepStore(
            intent.CreepId,
            "energy",
            creep.Store["energy"] - upgradeAmount
        );

        // Add progress to controller
        writer.UpdateControllerProgress(
            intent.ControllerId,
            controller.Progress + upgradeAmount
        );

        await writer.FlushAsync();

        // ✅ 4. Emit stats
        statsSink.RecordControllerUpgrade(roomId, intent.ControllerId, upgradeAmount);
    }

    private int CalculateUpgrade(RoomObject creep, RoomObject controller)
    {
        var workParts = creep.Body.Count(p => p.Type == "work");
        var energyAvailable = creep.Store["energy"];
        return Math.Min(workParts * 1, energyAvailable);  // 1 energy per work part
    }
}
```

```bash
# 2. Register in DI
# Location: src/ScreepsDotNet.Engine/ServiceCollectionExtensions.cs
```

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEngineCore(this IServiceCollection services)
    {
        // ... existing registrations

        // Add new handler
        services.AddSingleton<ControllerUpgradeHandler>();

        return services;
    }
}
```

```bash
# 3. Add unit tests
# Location: src/ScreepsDotNet.Engine.Tests/Processors/ControllerUpgradeHandlerTests.cs
```

```csharp
public class ControllerUpgradeHandlerTests
{
    [Fact]
    public async Task ProcessAsync_CreepUpgradesController_UpdatesProgress()
    {
        // Arrange
        var stateProvider = new FakeRoomStateProvider();
        var mutationFactory = new FakeRoomMutationWriterFactory();
        var statsSink = new FakeRoomStatsSink();

        stateProvider.SetRoomState("W1N1", new RoomSnapshot
        {
            Objects = new Dictionary<string, RoomObject>
            {
                ["creep1"] = new()
                {
                    Id = "creep1",
                    Body = [new() { Type = "work" }, new() { Type = "work" }],
                    Store = new() { ["energy"] = 10 }
                },
                ["controller1"] = new()
                {
                    Id = "controller1",
                    Progress = 100
                }
            }
        });

        var handler = new ControllerUpgradeHandler(stateProvider, mutationFactory, statsSink);

        var intent = new UpgradeIntent
        {
            CreepId = "creep1",
            ControllerId = "controller1"
        };

        // Act
        await handler.ProcessAsync("W1N1", intent);

        // Assert
        var writer = mutationFactory.LastWriter;
        Assert.Equal(2, writer.Mutations.Count);  // Creep energy + controller progress
        Assert.Equal(1, statsSink.UpgradeEvents.Count);  // Stats emitted
    }
}
```

```bash
# 4. Add parity test (E7)
# Location: src/ScreepsDotNet.Engine.Tests/Parity/ControllerUpgradeParityTests.cs
```

```csharp
public class ControllerUpgradeParityTests
{
    [Fact]
    public async Task ControllerUpgrade_MatchesNodeEngine()
    {
        // Arrange - same fixture for both engines
        var fixture = new RoomFixture
        {
            Objects = [/* creep + controller */]
        };

        // Act - run Node engine
        var nodeResult = await LegacyEngineRunner.RunTickAsync(fixture);

        // Act - run .NET engine
        var dotnetResult = await ManagedEngineRunner.RunTickAsync(fixture);

        // Assert - outputs match
        Assert.Equal(nodeResult.ControllerProgress, dotnetResult.ControllerProgress);
        Assert.Equal(nodeResult.CreepEnergy, dotnetResult.CreepEnergy);
    }
}
```

```bash
# 5. Update tracking
# - Update docs/engine/e2.md (mark handler as complete)
# - Update this CLAUDE.md if pattern changes
```

### Test Parity with Node.js Engine

```bash
# 1. Set up Node.js engine fixture
# Location: scripts/engine-parity/ (future)

# 2. Run same fixture through both engines
# .NET engine:
dotnet test --filter "FullyQualifiedName~ParityTests"

# Node engine (future):
node scripts/engine-parity/run-fixture.js <fixture-name>

# 3. Compare outputs
# - Room object states (positions, energy, store)
# - Global state changes (market, NPCs)
# - Stats/telemetry (CPU, actions)
# - User memory mutations

# 4. Fix divergences
# - Debug .NET handler logic
# - Verify Driver abstraction parity
# - Check calculation formulas vs. Node
```

### Debug Engine Data Flow

**Problem: Mutations not persisting**

```bash
# 1. Check mutation writer is created
var writer = mutationFactory.Create(roomId);  // ✅ Should return writer

# 2. Verify mutations are queued
writer.UpdateCreepPosition(creepId, newPos);  // ✅ Should queue mutation

# 3. Ensure flush is called
await writer.FlushAsync();  // ✅ CRITICAL - without this, nothing persists

# 4. Check Driver layer received mutations
# Look at Driver logs for bulk writer flush events
```

**Problem: State reads return stale data**

```bash
# 1. Verify state provider is called
var state = await stateProvider.GetRoomStateAsync(roomId);

# 2. Check if Driver cache is invalidated
# Driver should invalidate cache after mutations flush

# 3. Force fresh read
# (Implementation detail - Driver may cache snapshots per tick)
```

**Problem: Stats not showing up**

```bash
# 1. Check stats sink is injected
public class Handler(... RoomStatsSink statsSink)  // ✅ Must inject

# 2. Verify stats are emitted
statsSink.RecordHarvest(roomId, sourceId, amount);  // ✅ Call sink

# 3. Check stats pipeline configuration
# Ensure RoomStatsPipeline is wired in DI
```

### Run Engine Tests

```bash
# All engine tests
dotnet test --filter "FullyQualifiedName~ScreepsDotNet.Engine.Tests"

# Specific handler
dotnet test --filter "FullyQualifiedName~ControllerUpgradeHandlerTests"

# Parity tests only (future)
dotnet test --filter "Category=Parity"

# With verbose output
dotnet test --filter "FullyQualifiedName~ScreepsDotNet.Engine.Tests" --logger "console;verbosity=detailed"
```

## Integration Points

### Consuming Driver Layer

**Engine MUST consume Driver abstractions, NEVER storage directly:**

```csharp
// ✅ CORRECT - Engine service registration
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEngineCore(this IServiceCollection services)
    {
        // Engine services
        services.AddSingleton<RoomStateProvider>();      // Wraps Driver's IRoomSnapshotProvider
        services.AddSingleton<GlobalStateProvider>();    // Wraps Driver's IGlobalSnapshotProvider
        services.AddSingleton<RoomMutationWriterFactory>(); // Wraps Driver's mutation dispatcher
        services.AddSingleton<UserMemorySink>();         // Wraps Driver's memory writer

        // Intent handlers
        services.AddSingleton<CreepMoveHandler>();
        services.AddSingleton<CreepHarvestHandler>();
        // ... more handlers

        // Stats/telemetry
        services.AddSingleton<RoomStatsSink>();
        services.AddSingleton<RoomStatsPipeline>();

        return services;
    }
}
```

**Engine wrappers delegate to Driver:**

```csharp
// ✅ Engine wrapper around Driver provider
public class RoomStateProvider(IDriverRoomSnapshotProvider driverProvider)
{
    public async Task<RoomSnapshot> GetRoomStateAsync(string roomId)
    {
        // Delegate to Driver, convert to Engine's in-memory model
        var driverSnapshot = await driverProvider.GetSnapshotAsync(roomId);
        return ConvertToEngineModel(driverSnapshot);
    }

    private RoomSnapshot ConvertToEngineModel(DriverRoomSnapshot driverSnapshot)
    {
        // Map Driver DTOs → Engine in-memory objects
        return new RoomSnapshot
        {
            RoomId = driverSnapshot.RoomId,
            Objects = driverSnapshot.Objects.ToDictionary(
                o => o.Id,
                o => MapToEngineObject(o)
            )
        };
    }
}
```

### Providing Parity Data (Future E7)

```csharp
// E7: Engine will expose parity validation hooks
public interface IEngineParityHooks
{
    Task<RoomDiff> GetRoomDiffAsync(string roomId);  // Compare states
    Task<List<ActionLog>> GetActionLogsAsync(string roomId);  // Compare actions
}
```

## Testing Strategy

### Unit Tests
- **Location:** `src/ScreepsDotNet.Engine.Tests/Processors/`
- **Pattern:** Use fake providers/writers, no Driver/storage dependencies
- **Coverage:** All intent handlers, calculation logic, edge cases

**Example:**
```csharp
public class FakeRoomStateProvider : IRoomStateProvider
{
    private readonly Dictionary<string, RoomSnapshot> _states = [];

    public void SetRoomState(string roomId, RoomSnapshot state)
    {
        _states[roomId] = state;
    }

    public Task<RoomSnapshot> GetRoomStateAsync(string roomId)
    {
        return Task.FromResult(_states[roomId]);
    }
}
```

### Parity Tests (E7 - Future)
- **Location:** `src/ScreepsDotNet.Engine.Tests/Parity/`
- **Dependencies:** Node.js engine runner, shared fixtures
- **Coverage:** All mechanics vs. Node baseline

### Integration Tests (E6 - Future)
- **Location:** `src/ScreepsDotNet.Engine.Tests/Integration/`
- **Dependencies:** Full Driver + Engine stack, Testcontainers
- **Coverage:** End-to-end tick execution

## Performance Considerations

- **Snapshot caching:** Driver may cache room snapshots per tick (avoid redundant DB reads)
- **Mutation batching:** Driver batches all mutations, flushes once per tick
- **In-memory processing:** Engine works with in-memory snapshots, not DB queries
- **Stats aggregation:** `RoomStatsSink` batches stats before writing to Mongo

## Known Issues & Workarounds

### Issue: "Cannot access database from Engine"
**Symptom:** Trying to inject `IMongoDatabase` or repository in Engine service
**Cause:** Engine must not access storage directly
**Fix:** Use Driver abstractions (`IRoomStateProvider`, etc.)

### Issue: Mutations not visible in next tick
**Symptom:** Updates made in tick N don't appear in tick N+1
**Cause:** `writer.FlushAsync()` not called
**Fix:** Always call `await writer.FlushAsync()` after queueing mutations

### Issue: Stats missing from history
**Symptom:** Room stats not appearing in Mongo history
**Cause:** `RoomStatsSink` not emitting events
**Fix:** Ensure all handlers inject and call `statsSink.Record*()`

## Reference Documentation

### Plan Documents (Engine-Specific)
- `../../docs/engine/roadmap.md` - E1-E8 milestones and progress tracking
- `../../docs/engine/e2.md` - Handler backlog tracking (detailed)
- `../../docs/engine/e5.md` - E5 blockers & implementation plan
- `../../docs/engine/data-model.md` - Engine data contracts (E2)
- `../../docs/engine/e1.md` - Node engine API inventory (E1)

### Related Subsystems
- `../ScreepsDotNet.Driver/CLAUDE.md` - Driver abstractions Engine consumes
- `../../CLAUDE.md` - Solution-wide patterns

### External References
- [Screeps Game Constants](https://docs.screeps.com/api/#Game) - Official API reference
- Legacy Node engine source - For parity validation

## Debugging Tips

**Problem: Can't find intent handler for type X**
- **Check:** Intent handler registration in `ServiceCollectionExtensions.AddEngineCore()`
- **Check:** Intent type constant matches (`IntentKeys.Upgrade` vs `"upgrade"`)

**Problem: Handler throws "key not found" on state access**
- **Check:** Object ID exists in room snapshot
- **Check:** Driver snapshot provider is returning correct data
- **Verify:** Room state has been seeded/initialized

**Problem: Calculation differs from Node engine**
- **Check:** Game constants (work power, energy costs, etc.)
- **Compare:** Node implementation side-by-side
- **Add:** Parity test for this specific calculation

**Problem: Stats/telemetry not flowing**
- **Check:** `RoomStatsSink` is injected in handler constructor
- **Check:** `statsSink.Record*()` is called after successful operation
- **Verify:** `RoomStatsPipeline` is registered in DI


## Maintenance

**Update this file when:**
- Changing data access patterns (update Coding Patterns)
- Discovering parity issues (update Known Issues)
- Integration contracts change (update Integration Points)
- Adding new debugging tips or common tasks

**Don't put in this file:**
- ❌ Roadmap tracking → Use `../../docs/engine/roadmap.md`
- ❌ Handler progress → Use `../../docs/engine/e2.md`
- ❌ E5 blockers → Use `../../docs/engine/e5.md`
- ❌ Data model design → Use `../../docs/engine/data-model.md`

**Keep it focused:**
- ✅ Coding patterns and best practices
- ✅ Common tasks (how to add handler, debug, test)
- ✅ Integration points with Driver
- ✅ Known issues and workarounds

**Cross-references:**
- Driver patterns belong in `../ScreepsDotNet.Driver/CLAUDE.md`
- Solution-wide patterns belong in `../../CLAUDE.md`
- All plan tracking belongs in `../../docs/engine/*.md`

**Last Updated:** 2026-01-21
