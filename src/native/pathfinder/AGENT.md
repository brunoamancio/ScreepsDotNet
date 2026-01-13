# Native Pathfinder Status

## Purpose
Track progress toward replacing the managed A* fallback with the upstream Screeps pathfinder (`pf.cc`), so `IPathfinderService` can expose multi-room/flee parity via P/Invoke. This is the hands-on log for D6 in `docs/driver.md`; consult that document for the overall driver roadmap.

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
- ✅ GitHub Actions workflow (`native-pathfinder.yml`) rebuilds the library on `ubuntu-latest` (linux-x64, linux-x86, linux-arm64), `windows-latest` (win-x64, win-x86, win-arm64), `macos-latest` (osx-x64), and `macos-14` (osx-arm64) whenever native files change, zips the outputs, and updates the `native-pathfinder-latest` GitHub release with per-RID packages.
- ✅ `PathfinderService` now calls into the native library (feature-flagged via `PathfinderServiceOptions.EnableNative`) and falls back to the managed A* only when no binary is available.
- ⚠️ Room callbacks (`roomCallback`), multi-goal arrays, and regression tests against the legacy driver are still missing, so the managed fallback stays in the codebase for now.

## Next Steps
1. Finish the `roomCallback`/cost-matrix plumbing so processor code can provide custom matrices exactly like the Node driver (requires managed delegates + `ScreepsPathfinder_SetRoomCallback`).
2. Add regression tests comparing native results to the legacy driver (multi-room goals, flee, portals) and gate the native feature flag on those baselines.
3. Expand the managed surface (goal arrays, flee helpers, diagnostics) and document rebuild instructions + feature toggles in `docs/driver.md` before removing the managed fallback.

Track progress here so other agents can pick up where you leave off.
