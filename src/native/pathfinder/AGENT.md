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
- ✅ CMake build + tooling exists (`build.sh`) to compile for a specific RID and copy the artifact to `src/ScreepsDotNet.Driver/runtimes/<rid>/native/`.
- ✅ GitHub Actions workflow (`native-pathfinder.yml`) rebuilds the library on `ubuntu-latest` (x64 + arm64), `windows-latest` (x64 + arm64), `macos-latest`, and `macos-14` whenever native files change, zips the outputs, and updates the `native-pathfinder-latest` GitHub release with per-RID packages.
- ⚠️ Managed driver still uses the C# A* fallback.
- ⚠️ CI/release currently requires manually invoking `build.sh` on each platform; automation pending.

## Next Steps
1. Wire up the managed bindings (`PathfinderNative` in Driver) that P/Invoke `ScreepsPathfinder_LoadTerrain/Search/FreeResult/SetRoomCallback`, keeping the managed A* fallback behind a feature flag.
2. Integrate the native service with processor movement + runtime loops, then add regression tests comparing native vs. legacy results (multi-room, flee, portal cases) to prove parity.
3. Extend CI/release packaging so every RID (including the newly built linux-arm64/win-arm64) is bundled into release artifacts and consumed by the driver without manual steps.

Track progress here so other agents can pick up where you leave off.
