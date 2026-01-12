# Native Pathfinder Status

## Purpose
Track progress toward replacing the managed A* fallback with the upstream Screeps pathfinder (`pf.cc`), so `IPathfinderService` can expose multi-room/flee parity via P/Invoke.

## Plan Snapshot
1. Extract Screeps solver sources and expose a C ABI (`Pathfinder_LoadTerrain`, `Pathfinder_Search`, etc.) with POD structs.
2. Build platform-specific binaries (`libscreepspathfinder`) for every RID the driver targets.
3. Add managed `[DllImport]` bindings + native loader in `PathfinderService`.
4. Wire processor movement + tests to the native solver and document the new dependency.

## Current Status (January 12, 2026)
- ✅ Solver sources (`pf.cc/.h`) copied into this directory.
- ✅ Terrain loading no longer requires V8/NAN: `path_finder_t::load_terrain` now accepts a POD array (`terrain_room_plain`), and the C wrapper (`pathfinder_exports.cpp`) parses room names and forwards data via the new API.
- ⚠️ Search still depends on V8/NAN callbacks, so `Pathfinder_Search` remains a stub.
- ⚠️ No build tooling yet; CMake stub exists but doesn’t link against V8 or emit usable binaries.
- ⚠️ Managed driver still uses the C# A* fallback.

## Next Steps
1. Refactor the search pipeline (`path_finder_t::search`, callbacks, cost matrices) so it no longer depends on V8/NAN. Native terrain loading + callback stubs exist, but search still uses V8 objects.
2. Implement the real exports in `pathfinder_exports.cpp`: instantiate `path_finder_t`, marshal terrain/cost matrices, and copy results back into `ScreepsPathfinderResultNative`.
3. Finalize the CMake project (or equivalent) to produce `libscreepspathfinder` for all target RIDs, plus scripts to drop them under `ScreepsDotNet.Driver/runtimes/<rid>/native`.
4. Update the managed `PathfinderService` to P/Invoke the new library, keeping the managed fallback as a contingency until the native path passes tests.

Track progress here so other agents can pick up where you leave off.
