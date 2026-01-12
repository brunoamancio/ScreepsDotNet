# Screeps Native Pathfinder (Work in Progress)

This folder hosts the beginnings of the standalone pathfinder library that will back `IPathfinderService`.

## Current contents

| File | Purpose |
| --- | --- |
| `pf.h`, `pf.cc` | Original Screeps pathfinder implementation (copied from `ScreepsNodeJs/driver/native/src`). |
| `pathfinder_exports.h/.cpp` | Stable C ABI that exposes `ScreepsPathfinder_LoadTerrain`, `Search`, `FreeResult`, and `SetRoomCallback`. |
| `CMakeLists.txt` | Builds the shared library for a specific RID and copies the output to `../ScreepsDotNet.Driver/runtimes/<rid>/native`. |
| `build.sh` | Convenience wrapper that configures + builds the library for a supplied RID (e.g., `linux-x64`) using CMake. |
| `AGENT.md` | Progress log / TODO list for the native pathfinder work. |

## Building

```
cd ScreepsDotNet/native/pathfinder
./build.sh linux-x64        # or win-x64, osx-arm64, etc.
```

The script:
1. Configures CMake with `-DRUNTIME_IDENTIFIER=<RID>` (defaults exist, but explicit values keep artifacts organized).
2. Builds the shared library (`libscreepspathfinder.{so|dylib|dll}`).
3. Copies the result to `ScreepsDotNet.Driver/runtimes/<RID>/native/`.

Run the script once per platform to populate every RID the .NET driver targets (linux-x64/linux-arm64/win-x64/osx-x64/osx-arm64). CI can invoke it on each runner type before packaging releases.

### Continuous Integration

`.github/workflows/native-pathfinder.yml` automatically runs `build.sh` on:
- `ubuntu-latest` (`linux-x64`)
- `ubuntu-latest` (`linux-arm64`, cross-compiled via aarch64 gcc)
- `windows-latest` (`win-x64`)
- `windows-latest` (`win-arm64`, via MSVC cross tools)
- `macos-latest` (`osx-x64`)
- `macos-14` (`osx-arm64`)

It uploads each RID’s native folder as an artifact so other jobs (or developers) can download prebuilt libraries without running CMake locally.

## Status / Next steps

The native search pipeline and exports are now functional. Remaining work lives in `AGENT.md` (managed bindings, CI packaging, and parity tests). Until the managed driver switches over, the C# fallback in `PathfinderService` is still available as a safety valve.
