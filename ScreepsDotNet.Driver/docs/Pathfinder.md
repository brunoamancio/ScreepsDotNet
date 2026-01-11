# Pathfinder Integration Plan
_Last updated: January 11, 2026_

D6 focuses on reproducing `driver.pathFinder`, which today wraps a native C++ module (`native/build/Release/native.node`) seeded with terrain data.

## Requirements
- Provide `pathFinder.create(nativeModule)` / `pathFinder.init(nativeModule, rooms)` equivalents so the processor can call `driver.pathFinder.make({ RoomPosition })` before movement logic.
- Support the same search API as the Node driver: `search(origin, goalOrGoals, opts)` returning `{path, ops, cost, incomplete}`.
- Load static terrain data once per process and cache it for all pathfinder instances.
- Keep behavior identical to the legacy engine (movement costs, flee logic, multi-room restrictions).

## Approach
### Option Chosen: Embed the existing C++ solver via P/Invoke
- Build the native pathfinder as a standalone library (libScreepsPathfinder) sourced from the original repo.
- Expose a C-friendly API (`extern "C"`) that takes serialized terrain grids + search requests.
- Use `DllImport` in `ScreepsDotNet.Driver.Pathfinding` to call into the library from C#.

### Components
1. **Native Layer (`/native/pathfinder`)**
   - Adapt the current Node addon to compile as a shared library (.so/.dll/.dylib) with exported functions:
     - `void InitTerrain(const TerrainTile* tiles, int roomCount);`
     - `SearchResult Search(const SearchRequest* request);`
   - Provide a simple C struct representation of positions, goals, and options.

2. **Managed Wrapper (`IPathfinderService`)**
   - Responsibilities:
     - Initialize native module with terrain data loaded from Mongo/Redis (reuse D2’s env adapter).
     - Adapt Screeps options (maxOps, plainCost, swampCost, flee) into native structs.
     - Convert results into managed objects (list of `RoomPosition`s plus metadata).
   - API sample:
```csharp
public interface IPathfinderService
{
    Task InitializeAsync(IEnumerable<RoomTerrainDocument> rooms, CancellationToken token = default);
    PathfinderResult Search(RoomPosition origin, PathfinderGoal goal, PathfinderOptions options);
}
```

3. **`RoomPosition` Compatibility**
   - Provide a lightweight struct similar to the JS version (x, y, room name) plus helpers (`GetRangeTo`, `DirectionTo`).
   - Movement code (processor) can share this struct to avoid duplication.

### Data Flow
1. On driver startup (`connect('processor')` equivalent), load terrain data via `IRedisKeyValueStore` (if cached) or Mongo fallback.
2. Pass the terrain blob to `IPathfinderService.InitializeAsync`.
3. Processor calls `pathFinder.make(new RoomPositionFactory())`, which returns an object with the same methods legacy code expects (likely via adapter/shim if we need compatibility).
4. Movement intents call `Search` via the adapter; results feed into the existing movement algorithm.

### Testing
- Unit tests for managed wrapper to ensure options map correctly.
- Integration tests comparing pathfinding results between the .NET wrapper and the Node engine for a set of rooms/targets.
- Performance tests to verify the P/Invoke overhead stays acceptable.

## Alternate Plan (if native port stalls)
- Temporarily host the Node pathfinder via an IPC bridge (e.g., small Node worker) while the native port is underway. This keeps the pipeline moving even if C++ work takes longer.

## Next Steps
- Extract and build the C++ pathfinder as a reusable library.
- Define the managed `IPathfinderService` interface + DTOs.
- Implement initialization + search mapping code in C#.
- Update `AGENT.md` (D6) to “Plan completed (implementation pending)” until the native work is done.
