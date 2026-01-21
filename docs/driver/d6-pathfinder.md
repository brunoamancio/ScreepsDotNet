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

### Current Status (January 2026)
  - `PathfinderService` now bootstraps the native solver via `PathfinderNative` whenever binaries are available for the current RID. The managed A* fallback was removed on January 13, 2026, so initialization fails fast if the native binary is missing.
  - Terrain ingestion accepts legacy 2 500-byte strings or packed 625-byte buffers; the service repacks the former before passing them to the native API.
  - Native binaries are published per RID and downloaded during `dotnet build` (hash verified); setting `NativePathfinderSkipDownload=true` keeps the old behavior for local experiments.
  - `roomCallback`, multi-goal arrays, flee logic, and `BlockRoom` semantics are all handled natively. Regression fixtures (multi-room, flee, portal/callback) live in `PathfinderNativeIntegrationTests`.
  - `src/native/pathfinder/scripts/run-legacy-regressions.js` now replays those fixtures against the Node driver (Node 12 + native addon) and drops reports into `src/native/pathfinder/reports/`. The January 13, 2026 run matched on ops/cost/path for every case (see `legacy-regressions.{json,md}`).
  - Limitations / remaining items:
    - Continue growing the Node + managed fixture set (keeper corridors, portal chains, power-creep flee, tight maxRooms corridors, tower/keeper hybrids already captured). Keep running the Node harness whenever native code changes.
    - Automate the Node comparison in CI so regressions are caught before release.

### Legacy Regression Harness

Run the Node parity check whenever you update the native bindings or tweak `PathfinderService`:

```
cd ScreepsDotNet
source ~/.nvm/nvm.sh
nvm use 12
npx node-gyp rebuild -C ../ScreepsNodeJs/driver/native   # if native.node is missing
node src/native/pathfinder/scripts/run-legacy-regressions.js
```

The script:
1. Loads `ScreepsNodeJs/driver/lib/path-finder` + `native/build/Release/native.node`.
2. Replays the regression fixtures (multi-room, flee, portal/callback), normalizing Node’s origin→target paths to the managed target→origin ordering before diffing.
3. Writes `reports/legacy-regressions.json` (machine-readable) plus `reports/legacy-regressions.md` (human summary).

If a case fails, inspect the JSON diff to see whether the issue is cost/ops/path ordering, then update `PathfinderNativeIntegrationTests` once the native fix lands.

- Updating the managed fixture: run `node src/native/pathfinder/scripts/refresh-baselines.js` (with Node 12 active) **or** `dotnet test src/ScreepsDotNet.Driver.Tests/ScreepsDotNet.Driver.Tests.csproj /p:RefreshPathfinderBaselines=true`. Both routes invoke the harness with `--baseline` and copy the canonical results into `Pathfinding/Baselines/legacy-regressions.json`.

- Next steps:
  1. Expand the regression coverage as additional intent handlers (movement/controller/power) migrate to the .NET processor; capture fresh fixtures with the Node script above.
  2. Since the managed solver is gone, focus on ensuring the download/release automation stays healthy (GitHub artifacts + MSBuild target) and document troubleshooting steps for missing binaries.
