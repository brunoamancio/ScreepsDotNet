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
- ⚠️ `pathfinder_exports.cpp` is a placeholder. `path_finder_t` still depends on V8/NAN callbacks, so the exports currently return `-1`.
- ⚠️ No build tooling yet; CMake stub exists but doesn’t link against V8 or emit usable binaries.
- ⚠️ Managed driver still uses the C# A* fallback.

## Next Steps
1. Refactor `pf.cc` to remove direct NAN/V8 usage (or wrap it behind neutral adapters) so the solver can run without Node.
2. Implement the real exports in `pathfinder_exports.cpp`: instantiate `path_finder_t`, marshal terrain/cost matrices, and copy results back into `ScreepsPathfinderResultNative`.
3. Finalize the CMake project (or equivalent) to produce `libscreepspathfinder` for all target RIDs, plus scripts to drop them under `ScreepsDotNet.Driver/runtimes/<rid>/native`.
4. Update the managed `PathfinderService` to P/Invoke the new library, keeping the managed fallback as a contingency until the native path passes tests.

Track progress here so other agents can pick up where you leave off.
