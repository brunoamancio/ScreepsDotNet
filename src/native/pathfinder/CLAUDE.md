# Native Pathfinder - Claude Context

## Purpose

C++ port of the upstream Screeps pathfinder with P/Invoke bindings for .NET. Provides native performance for pathfinding (multi-room, flee, cost matrices) consumed by Driver's `PathfinderService`. Compiles for all target platforms (Linux, Windows, macOS × x64/arm64) with automated CI/CD releases.

## Dependencies

### This Subsystem Depends On
- Upstream Screeps pathfinder source (`pf.cc/.h`)
- CMake (build system)
- C++ compiler toolchains per platform:
  - Linux: GCC or Clang
  - Windows: MSVC (Visual Studio Build Tools)
  - macOS: Xcode Command Line Tools
- Node.js 12 (for parity baseline generation)

### These Depend On This Subsystem
- `ScreepsDotNet.Driver` - `PathfinderService` P/Invokes into `libscreepspathfinder`
- Tests: `ScreepsDotNet.Driver.Tests/Pathfinding/PathfinderNativeIntegrationTests.cs`

## Critical Rules

- ✅ **ALWAYS run parity tests after changing C++ code** (`dotnet test --filter PathfinderNativeIntegrationTests`)
- ✅ **ALWAYS refresh baselines when changing pathfinder logic** (`dotnet test /p:RefreshPathfinderBaselines=true`)
- ✅ **ALWAYS build for ALL platforms before releasing** (CI does this automatically)
- ✅ **ALWAYS verify 100% parity with Node.js pathfinder** (check `reports/legacy-regressions.md`)
- ❌ **NEVER modify upstream pathfinder source without documenting differences** (maintain upgrade path)
- ❌ **NEVER skip CI checks** (native binaries must be reproducible)
- ❌ **NEVER commit binaries to git** (use GitHub releases for distribution)

## Code Structure

```
src/native/pathfinder/
├── CLAUDE.md                                    # This file
├── pf.cc / pf.h                                # Upstream Screeps pathfinder (C++)
├── pathfinder_exports.cpp                      # C ABI wrapper for .NET P/Invoke
├── pathfinder_exports.h                        # C ABI header
├── CMakeLists.txt                              # Build configuration
├── build.sh                                    # Build script (Linux/macOS)
├── build.ps1                                   # Build script (Windows)
├── scripts/
│   ├── run-legacy-regressions.js              # Node.js parity harness
│   └── refresh-baselines.js                   # Baseline update script
├── reports/
│   ├── legacy-regressions.json                # Node pathfinder results
│   └── legacy-regressions.md                  # Human-readable report
└── .github/workflows/native-pathfinder.yml    # CI/CD workflow
```

**Driver integration:**
```
src/ScreepsDotNet.Driver/
├── Services/Pathfinding/
│   ├── PathfinderService.cs                   # P/Invoke wrapper
│   └── NativePathfinderLoader.cs              # RID-specific loader
└── runtimes/                                  # Native binaries (auto-downloaded)
    ├── linux-x64/native/libscreepspathfinder.so
    ├── linux-arm64/native/libscreepspathfinder.so
    ├── win-x64/native/screepspathfinder.dll
    ├── win-arm64/native/screepspathfinder.dll
    ├── osx-x64/native/libscreepspathfinder.dylib
    └── osx-arm64/native/libscreepspathfinder.dylib
```

## Coding Patterns

### C++ Export Pattern

**✅ Good - C ABI for P/Invoke compatibility:**
```cpp
// pathfinder_exports.h
#ifdef __cplusplus
extern "C" {
#endif

typedef struct {
    int x;
    int y;
    const char* room;
} PathPosition;

typedef struct {
    PathPosition* positions;
    int count;
    int cost;
    int ops;
} SearchResult;

// Export with C linkage
EXPORT_API SearchResult* ScreepsPathfinder_Search(
    PathPosition* origin,
    PathPosition* goal,
    int maxRooms
);

EXPORT_API void ScreepsPathfinder_FreeResult(SearchResult* result);

#ifdef __cplusplus
}
#endif
```

**❌ Bad - C++ classes/templates not P/Invoke compatible:**
```cpp
// ❌ Can't P/Invoke into C++ classes
class PathfinderAPI {
public:
    std::vector<Position> Search(Position origin, Position goal);  // ❌ No C ABI
};

// ❌ Templates don't export cleanly
template<typename T>
T* AllocateResult();  // ❌ No stable ABI
```

### P/Invoke Pattern (C# side)

**✅ Good - Matching C struct layout:**
```csharp
// PathfinderService.cs (Driver)
[StructLayout(LayoutKind.Sequential)]
public struct PathPosition
{
    public int X;
    public int Y;
    [MarshalAs(UnmanagedType.LPStr)]
    public string Room;
}

[StructLayout(LayoutKind.Sequential)]
public struct SearchResult
{
    public IntPtr Positions;  // PathPosition*
    public int Count;
    public int Cost;
    public int Ops;
}

[DllImport("libscreepspathfinder", CallingConvention = CallingConvention.Cdecl)]
private static extern IntPtr ScreepsPathfinder_Search(
    ref PathPosition origin,
    ref PathPosition goal,
    int maxRooms
);

[DllImport("libscreepspathfinder", CallingConvention = CallingConvention.Cdecl)]
private static extern void ScreepsPathfinder_FreeResult(IntPtr result);
```

**❌ Bad - Mismatched layout:**
```csharp
// ❌ Wrong layout - won't marshal correctly
public class PathPosition  // ❌ Should be struct
{
    public int X;
    public int Y;
    public string Room;  // ❌ Missing MarshalAs
}

// ❌ Missing calling convention
[DllImport("libscreepspathfinder")]  // ❌ No CallingConvention
private static extern IntPtr ScreepsPathfinder_Search(...);
```

## Current Status

### ✅ Completed (January 13, 2026)
- **Native library implementation** - C++ pathfinder with C ABI exports
- **Cross-platform builds** - Linux (x64/x86/arm64), Windows (x64/x86/arm64), macOS (x64/arm64)
- **CI/CD pipeline** - GitHub Actions builds all platforms, publishes to releases
- **P/Invoke integration** - Driver's `PathfinderService` calls native library
- **Managed fallback removed** - Native binaries required (fail fast if missing)
- **Parity testing** - 100% match with Node.js pathfinder (see `reports/legacy-regressions.md`)
- **Regression fixtures** - 15+ test cases covering:
  - Multi-room pathfinding
  - Flee mode
  - Cost matrices (room callbacks)
  - Portal chains
  - Tight corridor navigation
  - Controller/tower/keeper obstacle avoidance

### 🔄 Maintenance (Ongoing)
- Monitor parity across all RIDs
- Add new regression fixtures as edge cases discovered
- Keep baselines in sync with upstream changes

### 📋 Future Improvements
- Automate baseline refresh in CI (on pathfinder source changes)
- Document Node 12 toolchain requirements
- Create troubleshooting guide for build failures per platform

## Common Tasks

### Rebuild Native Binaries Locally

**Linux/macOS:**
```bash
cd src/native/pathfinder

# Build for current platform
./build.sh linux-x64     # or linux-arm64, osx-x64, osx-arm64

# Binaries copied to: src/ScreepsDotNet.Driver/runtimes/<rid>/native/

# Test
cd ../../..
dotnet test --filter "FullyQualifiedName~PathfinderNativeIntegrationTests"
```

**Windows:**
```powershell
cd src/native/pathfinder

# Build for current platform (requires MSVC)
.\build.ps1 win-x64      # or win-x86, win-arm64

# Binaries copied to: src\ScreepsDotNet.Driver\runtimes\<rid>\native\

# Test
cd ..\..\..
dotnet test --filter "FullyQualifiedName~PathfinderNativeIntegrationTests"
```

**Build requirements:**
- **Linux:** `sudo apt-get install build-essential cmake`
- **Windows:** Visual Studio Build Tools 2022+ with C++ workload
- **macOS:** `xcode-select --install`

### Update Parity Baselines

When pathfinder logic changes, regenerate baselines from Node.js pathfinder:

```bash
cd src/native/pathfinder

# 1. Run Node.js parity harness (requires Node 12)
node scripts/run-legacy-regressions.js

# Output: reports/legacy-regressions.json + .md

# 2. Review report
cat reports/legacy-regressions.md
# Check: All fixtures should show "PASS" with expected cost/ops

# 3. Copy Node results to managed baseline
node scripts/refresh-baselines.js

# Or via dotnet test:
dotnet test --filter "FullyQualifiedName~PathfinderNativeIntegrationTests" \
    /p:RefreshPathfinderBaselines=true

# 4. Commit updated baseline
git add src/ScreepsDotNet.Driver.Tests/Pathfinding/Baselines/legacy-regressions.json
git commit -m "chore: update pathfinder baselines"
```

### Add a New Regression Fixture

```bash
# 1. Add fixture to Node.js harness
# Location: src/native/pathfinder/scripts/run-legacy-regressions.js
```

```javascript
// Example: new "diagonal-obstacle" fixture
const fixtures = [
    // ... existing fixtures

    {
        name: "diagonal-obstacle",
        origin: { x: 10, y: 10, roomName: "W0N0" },
        goal: { x: 40, y: 40, roomName: "W0N0" },
        opts: {
            maxRooms: 1,
            roomCallback: (roomName) => {
                const costs = new PathFinder.CostMatrix();
                // Diagonal wall from (15,15) to (35,35)
                for (let i = 0; i < 20; i++) {
                    costs.set(15 + i, 15 + i, 255);
                }
                return costs;
            }
        }
    }
];
```

```bash
# 2. Run harness to generate expected result
node scripts/run-legacy-regressions.js

# 3. Refresh baseline
node scripts/refresh-baselines.js

# 4. Add managed test
# Location: src/ScreepsDotNet.Driver.Tests/Pathfinding/PathfinderNativeIntegrationTests.cs
```

```csharp
[Fact]
public async Task DiagonalObstacle_MatchesNodePathfinder()
{
    // Arrange
    var baseline = LoadBaseline("diagonal-obstacle");

    var origin = new Position(10, 10, "W0N0");
    var goal = new Position(40, 40, "W0N0");

    var costMatrix = new CostMatrix();
    for (int i = 0; i < 20; i++)
    {
        costMatrix.Set(15 + i, 15 + i, 255);
    }

    // Act
    var result = _pathfinder.Search(origin, goal, new SearchOptions
    {
        MaxRooms = 1,
        RoomCallback = (room) => costMatrix
    });

    // Assert
    Assert.Equal(baseline.Cost, result.Cost);
    Assert.Equal(baseline.Ops, result.Ops);
    Assert.Equal(baseline.Path.Count, result.Path.Count);
}
```

```bash
# 5. Run test
dotnet test --filter "FullyQualifiedName~DiagonalObstacle_MatchesNodePathfinder"

# 6. Commit
git add scripts/run-legacy-regressions.js
git add src/ScreepsDotNet.Driver.Tests/Pathfinding/Baselines/legacy-regressions.json
git add src/ScreepsDotNet.Driver.Tests/Pathfinding/PathfinderNativeIntegrationTests.cs
git commit -m "test: add diagonal-obstacle pathfinder regression"
```

### Verify GitHub Release Binaries

After CI publishes binaries to GitHub releases:

```bash
# 1. Check release exists
# https://github.com/<your-org>/screeps-rewrite/releases/tag/native-pathfinder-latest

# 2. Download per-RID packages
curl -L https://github.com/<org>/screeps-rewrite/releases/download/native-pathfinder-latest/linux-x64.zip -o linux-x64.zip

# 3. Verify contents
unzip -l linux-x64.zip
# Should contain: libscreepspathfinder.so

# 4. Test with Driver
# Driver auto-downloads during build (unless NativePathfinderSkipDownload=true)
dotnet build src/ScreepsDotNet.Driver/ScreepsDotNet.Driver.csproj

# 5. Verify auto-download worked
ls src/ScreepsDotNet.Driver/runtimes/*/native/
# Should show binaries for all RIDs

# 6. Run parity tests
dotnet test --filter "FullyQualifiedName~PathfinderNativeIntegrationTests"
```

### Debug Build Failures

**Problem: CMake not found**
```bash
# Linux/macOS
brew install cmake  # or apt-get install cmake

# Windows
# Install via Visual Studio Installer (CMake component)
# Or download from https://cmake.org/download/
```

**Problem: Compiler not found**
```bash
# Linux
sudo apt-get install build-essential

# macOS
xcode-select --install

# Windows
# Install Visual Studio Build Tools 2022
# Ensure "Desktop development with C++" workload is selected
```

**Problem: Wrong RID specified**
```bash
# Check valid RIDs:
# linux-x64, linux-x86, linux-arm64
# win-x64, win-x86, win-arm64
# osx-x64, osx-arm64

# Example:
./build.sh linux-x64  # ✅ Correct
./build.sh linux      # ❌ Wrong - need specific arch
```

**Problem: Library not found at runtime**
```bash
# Check Driver expects library in correct location
ls src/ScreepsDotNet.Driver/runtimes/linux-x64/native/libscreepspathfinder.so

# If missing, rebuild:
cd src/native/pathfinder
./build.sh linux-x64

# Verify copy worked:
ls ../../ScreepsDotNet.Driver/runtimes/linux-x64/native/
```

### Run Parity Tests

```bash
# All pathfinder integration tests
dotnet test --filter "FullyQualifiedName~PathfinderNativeIntegrationTests"

# Specific fixture
dotnet test --filter "FullyQualifiedName~MultiRoom_MatchesNodePathfinder"

# With verbose output
dotnet test --filter "FullyQualifiedName~PathfinderNativeIntegrationTests" \
    --logger "console;verbosity=detailed"

# Check parity report (after running Node harness)
cat src/native/pathfinder/reports/legacy-regressions.md
```

## CI/CD Workflow

### GitHub Actions Pipeline

**Triggered by:**
- Push to `main` branch affecting `src/native/pathfinder/**`
- Manual workflow dispatch

**Jobs:**
```yaml
# .github/workflows/native-pathfinder.yml
build-linux:
  runs-on: ubuntu-latest
  strategy:
    matrix:
      rid: [linux-x64, linux-x86, linux-arm64]
  steps:
    - Build for ${{ matrix.rid }}
    - Upload artifact

build-windows:
  runs-on: windows-latest
  strategy:
    matrix:
      rid: [win-x64, win-x86, win-arm64]
  steps:
    - Build for ${{ matrix.rid }}
    - Upload artifact

build-macos-x64:
  runs-on: macos-latest  # Intel
  steps:
    - Build for osx-x64
    - Upload artifact

build-macos-arm64:
  runs-on: macos-14  # Apple Silicon
  steps:
    - Build for osx-arm64
    - Upload artifact

publish-release:
  needs: [build-linux, build-windows, build-macos-x64, build-macos-arm64]
  steps:
    - Download all artifacts
    - Zip per RID
    - Update GitHub release "native-pathfinder-latest"
```

### Release Assets

After CI completes, release contains:
```
native-pathfinder-latest/
├── linux-x64.zip        → libscreepspathfinder.so
├── linux-x86.zip        → libscreepspathfinder.so
├── linux-arm64.zip      → libscreepspathfinder.so
├── win-x64.zip          → screepspathfinder.dll
├── win-x86.zip          → screepspathfinder.dll
├── win-arm64.zip        → screepspathfinder.dll
├── osx-x64.zip          → libscreepspathfinder.dylib
└── osx-arm64.zip        → libscreepspathfinder.dylib
```

## Integration with Driver

### Auto-Download on Build

**Driver build process:**
```bash
# When building Driver, MSBuild target downloads native binaries
dotnet build src/ScreepsDotNet.Driver/ScreepsDotNet.Driver.csproj

# Downloads from GitHub release → runtimes/<rid>/native/
# Verifies hash to ensure integrity
# Skipped if NativePathfinderSkipDownload=true
```

**Skip auto-download (use local build):**
```bash
export NativePathfinderSkipDownload=true  # Linux/macOS
# or
$env:NativePathfinderSkipDownload="true"  # Windows PowerShell

dotnet build src/ScreepsDotNet.Driver/ScreepsDotNet.Driver.csproj
```

### P/Invoke Loading

**Driver loads library at runtime:**
```csharp
// PathfinderService.cs
public class PathfinderService
{
    static PathfinderService()
    {
        // Load native library for current RID
        NativePathfinderLoader.Load();
    }

    [DllImport("libscreepspathfinder", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr ScreepsPathfinder_Search(...);

    public SearchResult Search(Position origin, Position goal, SearchOptions opts)
    {
        // Marshal managed → native
        var result = ScreepsPathfinder_Search(...);
        // Marshal native → managed
        return ParseResult(result);
    }
}
```

## Performance Characteristics

- **Native vs Managed:** ~10-50x faster for complex multi-room searches
- **Memory:** Native allocations freed explicitly (managed GC doesn't track)
- **Thread safety:** Native library is NOT thread-safe (Driver must serialize calls)

## Known Issues & Workarounds

### Issue: DllNotFoundException on first run
**Symptom:** `DllNotFoundException: Unable to load DLL 'libscreepspathfinder'`
**Cause:** Binaries not downloaded yet
**Fix:**
```bash
# Rebuild Driver (triggers auto-download)
dotnet build src/ScreepsDotNet.Driver/ScreepsDotNet.Driver.csproj

# Or manually build native library
cd src/native/pathfinder
./build.sh <your-rid>
```

### Issue: Parity tests fail after C++ changes
**Symptom:** `PathfinderNativeIntegrationTests` fail, cost/ops don't match baseline
**Cause:** Baseline out of sync with code changes
**Fix:**
```bash
# Refresh baseline from Node.js pathfinder
cd src/native/pathfinder
node scripts/run-legacy-regressions.js
node scripts/refresh-baselines.js

# Re-run tests
dotnet test --filter "PathfinderNativeIntegrationTests"
```

### Issue: Cross-compilation fails on macOS
**Symptom:** Can't build osx-arm64 on Intel Mac (or vice versa)
**Cause:** Cross-compilation requires matching SDK
**Workaround:** Use CI (GitHub Actions has both architectures) or build on native hardware

## Reference Documentation

### Design Docs
- `docs/driver.md` - D6 milestone (pathfinder integration)
- `src/ScreepsDotNet.Driver/docs/Pathfinder.md` - Detailed pathfinder design

### Related Subsystems
- `../ScreepsDotNet.Driver/CLAUDE.md` - Driver uses pathfinder via `PathfinderService`
- `../../CLAUDE.md` - Solution-wide build/test patterns

### External References
- [Screeps pathfinder source](https://github.com/screeps/driver/blob/master/lib/pf.cc) - Upstream implementation
- [CMake documentation](https://cmake.org/documentation/) - Build system reference

## Debugging Tips

**Problem: Binaries not being copied to runtimes/ folder**
- **Check:** Build script output for errors
- **Verify:** CMake completed successfully
- **Check:** Target path exists: `src/ScreepsDotNet.Driver/runtimes/<rid>/native/`

**Problem: Parity divergence on specific fixture**
- **Compare:** Node output vs managed output side-by-side
- **Check:** Terrain packing order (column-major vs row-major)
- **Verify:** Cost matrix values match exactly
- **Debug:** Add logging to native `ScreepsPathfinder_Search`

**Problem: CI build fails for specific platform**
- **Check:** GitHub Actions logs for that job
- **Verify:** Runner has required toolchain installed
- **Test:** Reproduce locally on same OS/arch

## Maintenance

**Update this file when:**
- Adding new regression fixtures (update Common Tasks)
- Changing build process (update Rebuild section)
- Modifying C ABI (update Coding Patterns)
- CI/CD workflow changes (update CI/CD Workflow section)
- Discovering platform-specific build issues (update Debugging Tips)

**Keep it focused:**
- This is for native build/release, not Driver integration details
- Driver integration details belong in `../ScreepsDotNet.Driver/CLAUDE.md`
- Solution-wide patterns belong in `../../CLAUDE.md`

**Last Updated:** 2026-01-17
