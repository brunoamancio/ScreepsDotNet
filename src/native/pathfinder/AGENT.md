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
- ✅ `PathfinderService` now calls into the native library (feature-flagged via `PathfinderServiceOptions.EnableNative`, which is on by default; disable it only for troubleshooting).
- ✅ Room callbacks (`roomCallback`), multi-goal arrays, flee helpers, and the `BlockRoom` semantic now flow through the native interop layer (`ScreepsPathfinder_SetRoomCallback`). Regression baselines (multi-room, flee, portal/callback) live in `ScreepsDotNet.Driver.Tests/Pathfinding/PathfinderNativeIntegrationTests.cs`.

## Next Steps
1. Run the legacy Node driver against the recorded regression fixtures to confirm parity (especially `roomCallback` block scenarios) and capture any remaining differences.
2. Document the rebuild instructions + feature toggles (`PathfinderServiceOptions`) in `docs/driver.md` now that native is the default, and keep the managed solver only as a troubleshooting flag (`EnableNative = false`).
3. Once Node parity is verified, prune any vestigial managed-only code paths and expand the regression suite with additional legacy captures as new intent handlers migrate.

Track progress here so other agents can pick up where you leave off.
