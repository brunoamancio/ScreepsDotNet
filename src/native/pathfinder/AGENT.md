# Native Pathfinder Status

## Purpose
Track progress toward replacing the managed A* fallback with the upstream Screeps pathfinder (`pf.cc`), so `IPathfinderService` can expose multi-room/flee parity via P/Invoke. This is the hands-on log for D6 in `docs/driver.md`; consult that document for the overall driver roadmap.

## Plan Snapshot
1. Extract Screeps solver sources and expose a C ABI (`Pathfinder_LoadTerrain`, `Pathfinder_Search`, etc.) with POD structs.
2. Build platform-specific binaries (`libscreepspathfinder`) for every RID the driver targets.
3. Add managed `[DllImport]` bindings + native loader in `PathfinderService`.
4. Wire processor movement + tests to the native solver and document the new dependency.

## Current Status (January 13, 2026)
- ✅ Solver sources (`pf.cc/.h`) copied into this directory.
- ✅ Terrain loading no longer requires V8/NAN: `path_finder_t::load_terrain` now accepts a POD array (`terrain_room_plain`), and the C wrapper (`pathfinder_exports.cpp`) parses room names and forwards data via the new API.
- ✅ `path_finder_t::search` now funnels through a native-only helper that emits POD results (`search_result_native`). The Nan/V8 wrapper simply adapts to JS while `ScreepsPathfinder_Search` calls the same helper directly.
- ✅ `pathfinder_exports.cpp` now returns real paths/costs, converts room names, and supports native room callbacks via `ScreepsPathfinder_SetRoomCallback`.
- ✅ CMake build + tooling exists (`build.sh`) to compile for a specific RID and copy the artifact to `src/ScreepsDotNet.Driver/runtimes/<rid>/native/`.
- ✅ GitHub Actions workflow (`native-pathfinder.yml`) rebuilds the library on `ubuntu-latest` (linux-x64, linux-x86, linux-arm64), `windows-latest` (win-x64, win-x86, win-arm64), `macos-latest` (osx-x64), and `macos-14` (osx-arm64) whenever native files change, zips the outputs, and updates the `native-pathfinder-latest` GitHub release with per-RID packages.
- ✅ `PathfinderService` now calls into the native library (feature-flagged via `PathfinderServiceOptions.EnableNative`, which is on by default; disable it only for troubleshooting).
- ✅ Room callbacks (`roomCallback`), multi-goal arrays, flee helpers, and the `BlockRoom` semantic now flow through the native interop layer (`ScreepsPathfinder_SetRoomCallback`). Regression baselines (multi-room, flee, portal/callback) live in `ScreepsDotNet.Driver.Tests/Pathfinding/PathfinderNativeIntegrationTests.cs`.
- ✅ Legacy parity harness: `scripts/run-legacy-regressions.js` loads the native `@screeps/driver` pathfinder (Node 12) and replays the same regression fixtures, writing `reports/legacy-regressions.json` + `.md`. Pass `--baseline <path>`, run `node scripts/refresh-baselines.js`, or call `dotnet test ... /p:RefreshPathfinderBaselines=true` to copy the canonical results into `src/ScreepsDotNet.Driver.Tests/Pathfinding/Baselines/legacy-regressions.json`. The January 13, 2026 run shows 100% parity (see `reports/legacy-regressions.md`).

## Next Steps
1. Keep expanding the regression dataset (e.g., movement intents for creeps/towers/power creeps) so `run-legacy-regressions.js` covers more real-world layouts.
2. Prune the managed fallback once we are comfortable that the native solver + download flow is stable (leave `PathfinderServiceOptions.EnableNative = false` solely for troubleshooting).
3. Consider wiring the Node parity script into CI (behind a matrix that has Node 12 + native module available) to catch accidental divergence automatically.

Track progress here so other agents can pick up where you leave off.
