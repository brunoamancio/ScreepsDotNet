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
- ✅ `path_finder_t::search` now funnels through a native-only helper that emits POD results (`search_result_native`). The Nan/V8 wrapper simply adapts to JS while `ScreepsPathfinder_Search` calls the same helper directly.
- ✅ `pathfinder_exports.cpp` now returns real paths/costs, converts room names, and supports native room callbacks via `ScreepsPathfinder_SetRoomCallback`.
- ⚠️ No build tooling yet; CMake stub exists but doesn’t link against V8 or emit usable binaries.
- ⚠️ Managed driver still uses the C# A* fallback.

## Next Steps
1. Finalize the CMake (or equivalent) build so we emit `libscreepspathfinder` for every RID (linux-x64/arm64, win-x64, osx-x64/arm64). Add scripts to drop binaries under `ScreepsDotNet.Driver/runtimes/<rid>/native`.
2. Add managed bindings (`PathfinderNative` in Driver) that P/Invoke `ScreepsPathfinder_LoadTerrain/Search/FreeResult/SetRoomCallback`, retaining the managed A* fallback behind a feature flag.
3. Integrate the native service with processor movement + runtime loops, then add regression tests comparing native vs. legacy results (multi-room, flee, portal cases) to prove parity.

Track progress here so other agents can pick up where you leave off.
