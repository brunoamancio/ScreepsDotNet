# Screeps Native Pathfinder (Work in Progress)

This folder hosts the beginnings of the standalone pathfinder library that will back `IPathfinderService`.

## Current contents

| File | Purpose |
| --- | --- |
| `pf.h`, `pf.cc` | Original Screeps pathfinder implementation (copied from `ScreepsNodeJs/driver/native/src`). |
| `pathfinder_exports.h/.cpp` | Placeholder C wrapper that will eventually expose a stable C ABI (`LoadTerrain`, `Search`) for the managed driver. |
| `AGENT.md` | Progress log / TODO list for the native pathfinder work. |

## Next steps

1. **Build system** – wire these sources into a portable build (CMake or similar) that emits `libscreepspathfinder` for all target RIDs.
2. **C bridge** – finish `pathfinder_exports.cpp` by instantiating `path_finder_t`, forwarding searches, and serializing results without V8/NAN dependencies.
3. **Packaging** – publish the compiled binaries under `ScreepsDotNet.Driver/runtimes/<rid>/native` so the managed driver can load them via `NativeLibrary`.

Until those steps are complete, the managed driver continues to use the C# fallback in `PathfinderService`. See `AGENT.md` in this folder for up-to-date progress.
