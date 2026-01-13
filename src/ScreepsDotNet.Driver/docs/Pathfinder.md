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

### Current Status (January 12, 2026)
- `PathfinderService` now bootstraps the native solver via `PathfinderNative` whenever binaries are available for the current RID. The feature is controlled by `PathfinderServiceOptions.EnableNative` (default `true`) and automatically falls back to the managed A* implementation if the native library fails to load.
- Terrain ingestion accepts legacy 2 500-byte strings or packed 625-byte buffers; the service repacks the former before passing them to the native API.
- Native binaries are published per RID and downloaded during `dotnet build` (hash verified); setting `NativePathfinderSkipDownload=true` keeps the old behavior for local experiments.
- Limitations:
  - Room callbacks (`roomCallback`), multi-goal arrays, and flee/road tuning are still handled only by the legacy Node setup; the managed surface currently exposes a single-goal search without callback hooks.
  - Processor tests still exercise the managed fallback; we need regression data that proves native vs. legacy parity before flipping the default permanently.

- Next steps:
  1. Expose room callback plumbing and multi-goal helpers so `roomCallback`/`goals[]` in processor code behave like the legacy driver.
  2. Add regression tests comparing native output to the Node driver (multi-room, flee, portal cases) and cover hash/download errors.
  3. Document the feature flag + troubleshooting steps in `docs/driver.md`/`AGENT.md`, then remove the managed fallback once parity is proven.
