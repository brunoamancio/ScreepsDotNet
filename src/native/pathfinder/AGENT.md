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
- ✅ Added a “wall-gap” fixture (column wall with a defined opening) to both the Node harness and the managed baseline, and fixed the native terrain packing (`TryPackTerrain`/`UnpackPackedTerrain`) to match the Node module’s column-major bit order. With the packing fix, the native solver now mirrors the legacy path (cost/ops) and the regression is enabled again.
- ✅ Added a “controller-corridor” fixture that forces the path through a narrow choke around a controller box; expectations now live in both the Node harness and the managed regression suite.
- ✅ Added a “tower-cost” fixture using `roomCallback` cost matrices to emulate tower avoidance (large high-cost zone with a diagonal safe corridor). Native + managed tests now assert the same multi-turn detour path (cost 50, ops 46).
- ✅ Added a “flee-multi-room” fixture so the flee logic is forced to cross room boundaries (origin W0N0 → W2N0 with `maxRooms=3`). Both the harness and managed tests now assert the Node path (cost 5, ops 4).
- ✅ Added a “dense-corridor” fixture that snakes through a maze-like choke carved into wall terrain. Legacy ops/cost (59/691) are captured via the Node harness, and the baseline JSON + managed integration test now assert the exact path so native/managed stay in sync.
- ✅ Added “controller-upgrade-creeps” and “tower-power-choke” fixtures so cost-matrix-driven obstacles (upgrade creep walls plus tower/power overlap zones) are covered. Expectations are recorded via the Node harness and consumed by the managed regression suite.
- ✅ Added “keeper-lair-corridor”, “portal-chain”, “power-creep-flee”, “controller-tight-limit”, and “tower-keeper-hybrid” fixtures. These fill the remaining parity gaps (keeper aggro paths, multi-portal chains, flee + power-node matrices, tight maxRooms corridors, and overlapping tower/keeper danger zones). Each case is generated via `run-legacy-regressions.js`, copied into `Pathfinding/Baselines/legacy-regressions.json`, and enforced by `PathfinderNativeIntegrationTests`.

## Next Steps
1. **Formalize native-only mode** – Remove the managed fallback once we’ve monitored a few more runs across all RIDs. Keep the flag only for emergency troubleshooting and document the process in `docs/driver.md`.
2. **Automate baseline refreshes** – Add a CI job (or scripted `dotnet test … /p:RefreshPathfinderBaselines=true`) that runs when `src/native/pathfinder` changes so we always capture Node parity before merging.
3. **Document rebuild/download flow** – Expand `docs/driver.md` (or a dedicated `PathfinderLifecycle.md`) with: required Node 12 toolchain for the harness, how to regenerate baselines, and steps for verifying the GitHub-release binaries. This will help other agents confidently delete the managed solver later.

Track progress here so other agents can pick up where you leave off.
