# MongoDB Parity Testing Setup

This guide explains how to run parity tests that compare .NET Engine against the official Node.js Screeps engine.

## ✨ Zero-Config Setup

Parity tests **automatically check and install** prerequisites:
- ✅ **Node.js 10.13.0 to 12.x ONLY** (nvm-aware: auto-detects and activates compatible version)
  - ⚠️ **Node 13+ NOT supported** - Screeps engine does NOT work with Node 13+, 14+, 16+, 18+, 20+
  - 💡 **Recommended:** Node 12.22.12 (last LTS version of Node 12)
- ✅ **Docker** (Testcontainers starts MongoDB automatically)
- ✅ **Official Screeps repos** (clones if missing: engine, driver, common)
- ✅ **npm dependencies** (runs `npm install` if `node_modules` missing)

### nvm Support

If you use [nvm](https://github.com/nvm-sh/nvm) (Linux/Mac) or [nvm-windows](https://github.com/coreybutler/nvm-windows):
- ✅ Tests automatically detect nvm
- ✅ Tests find the highest installed Node.js version in range **10.13.0 to 12.x**
- ✅ Tests automatically activate that version (`nvm use`)
- ❌ If no suitable version found, error tells you to run `nvm install 12.22.12`
- ❌ If Node 13+ is installed, tests will reject it and ask for compatible version

## Quick Start

### Prerequisites (Manual Install Only)

Install these once on your system:
- **Node.js 10.13.0 to 12.x** - Download Node 12.x LTS from https://nodejs.org/dist/latest-v12.x/
  - ⚠️ **Do NOT use Node 13+** - Screeps engine is incompatible
  - 💡 Recommended: Use nvm to manage Node versions (see below)
- **Docker** - Download from https://www.docker.com/get-started

### Run Parity Tests

```bash
# That's it! Just run the tests with the Parity category
dotnet test --filter Category=Parity

# First run will:
# 1. Check Node.js version (must be >= 10.13.0)
# 2. Check Docker is running
# 3. Clone official Screeps repos (30-60 seconds)
# 4. Run npm install (20-30 seconds)
# 5. Start MongoDB via Testcontainers
# 6. Run parity tests comparing .NET vs Node.js

# Subsequent runs are fast (repos/dependencies already installed)
dotnet test --filter Category=Parity
```

### What Gets Checked Automatically

| Check | What Happens | Error if Missing |
|-------|--------------|------------------|
| **nvm** | Checks if nvm is available (Linux/Mac/Windows) | Falls back to direct Node.js check |
| **Node.js (via nvm)** | Finds highest version in range 10.13.0-12.x, runs `nvm use` | "Run: nvm install 12.22.12" |
| **Node.js (direct)** | Runs `node --version`, validates 10.13.0-12.x range | "Install Node 12.x from nodejs.org" |
| **Node.js (too new)** | Rejects Node 13+ | "Node 13+ NOT compatible with Screeps" |
| **Docker** | Runs `docker info` | "Start Docker Desktop or dockerd" |
| **Screeps Repos** | Checks `tools/parity-harness/engine/repos/` | Clones repos automatically |
| **npm Deps** | Checks `tools/parity-harness/engine/node_modules/` | Runs `npm install` automatically |
| **MongoDB** | Testcontainers starts container | Docker connection error |

## Test Workflow

### How It Works

1. **Prerequisites Check** - `ParityTestPrerequisites` fixture runs before tests
   - Checks Node.js version >= 10.13.0
   - Checks Docker is running
   - Clones Screeps repos if missing
   - Runs `npm install` if `node_modules` missing
2. **MongoDB Start** - Testcontainers automatically starts MongoDB 7 container
3. **JSON Fixture** - Test scenario defined in `Parity/Fixtures/*.json`
4. **.NET Execution** - `ParityTestRunner` runs fixture through .NET Engine
5. **Node.js Execution** - `NodeJsHarnessRunner` runs fixture through Node.js engine
6. **Comparison** - `ParityComparator` compares mutations field-by-field
7. **Report** - `DivergenceReporter` formats differences if found
8. **Cleanup** - Testcontainers automatically stops and removes MongoDB container

### Example: Harvest Parity Test

```csharp
[Fact]
[Trait("Category", "Parity")]
public async Task Harvest_Basic_MatchesNodeJsEngine()
{
    // Prerequisites already checked by ParityTestPrerequisites fixture
    // MongoDB already started by MongoDbParityFixture

    // Load fixture
    var fixturePath = Path.Combine("Parity", "Fixtures", "harvest_basic.json");
    var state = await JsonFixtureLoader.LoadFromFileAsync(fixturePath, TestContext.Current.CancellationToken);

    // Execute both engines
    var dotnetOutput = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);
    var nodejsOutput = await NodeJsHarnessRunner.RunFixtureAsync(fixturePath, TestContext.Current.CancellationToken);

    // Compare field-by-field
    var comparison = ParityComparator.Compare(dotnetOutput, nodejsOutput);
    if (comparison.HasDivergences)
    {
        Assert.Fail(DivergenceReporter.FormatReport(comparison, "harvest_basic.json"));
    }
}
```

### Available Parity Tests

| Test | Description | Fixture |
|------|-------------|---------|
| `Harvest_Basic_MatchesNodeJsEngine` | Creep harvests energy from source | `harvest_basic.json` |
| `Transfer_Basic_MatchesNodeJsEngine` | Creep transfers energy to spawn | `transfer_basic.json` |
| `ControllerUpgrade_MatchesNodeJsEngine` | Creep upgrades controller | `controller_upgrade.json` |
| `LinkTransfer_MatchesNodeJsEngine` | Link transfers energy to another link | `link_transfer.json` |

Run all: `dotnet test --filter Category=Parity`
Run one: `dotnet test --filter "FullyQualifiedName~Harvest_Basic_MatchesNodeJsEngine"`

## Troubleshooting

### Node.js Not Found

**Error:** `Node.js not found. Please install Node.js 10.13.0 to 12.x`

**Fix (with nvm - RECOMMENDED):**
```bash
# Install nvm (Linux/Mac)
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.0/install.sh | bash
# Or install nvm-windows from: https://github.com/coreybutler/nvm-windows/releases

# Install Node.js 12.22.12 (last LTS of Node 12)
nvm install 12.22.12
nvm use 12.22.12

# Verify
node --version  # Should show v12.22.12
```

**Fix (without nvm):**
```bash
# Download Node 12.x LTS from: https://nodejs.org/dist/latest-v12.x/
# Choose the installer for your OS
# Verify installation
node --version  # Should show v12.x.x
```

### nvm Found But No Compatible Version

**Error:** `nvm is installed but no compatible Node.js version found. Required: Node.js 10.13.0 to 12.x`

**Fix:**
```bash
# Install Node 12.22.12 via nvm
nvm install 12.22.12

# Verify it's installed
nvm list  # Should show v12.22.12

# Tests will auto-activate it next run
dotnet test --filter Category=Parity
```

### Node.js Version Too New

**Error:** `Node.js version 18.0.0 is too new for Screeps engine (does NOT work with Node 13+)`

**Fix:**
```bash
# Install compatible Node 12.x version
nvm install 12.22.12
nvm use 12.22.12

# Verify
node --version  # Should show v12.22.12

# Run tests (will use Node 12.x automatically)
dotnet test --filter Category=Parity
```

### Docker Not Running

**Error:** `Docker is not running. Please start Docker Desktop or dockerd.`

**Fix:**
```bash
# Check Docker status
docker info

# Start Docker Desktop (Mac/Windows)
# Or start dockerd (Linux)
sudo systemctl start docker
```

### Official Screeps Repos Clone Failed

**Error:** `Failed to clone official Screeps repositories`

**Fix:**
```bash
# Check git is installed
git --version

# Manually clone repos
cd tools/parity-harness/engine
bash scripts/clone-repos.sh  # Or use PowerShell on Windows

# Check repos cloned successfully
ls -la repos/  # Should see: screeps, @screeps/driver, @screeps/common
```

### npm install Failed

**Error:** `Failed to install Node.js dependencies`

**Fix:**
```bash
# Check npm is available
npm --version

# Manually install
cd tools/parity-harness/engine
npm install

# Check node_modules exists
ls -la node_modules/
```

### Testcontainers Docker Connection Failed

**Error:** `Failed to start MongoDB container` or `Docker connection failed`

**Fix:**
```bash
# Ensure Docker is running
docker info

# On Linux, ensure user has Docker permissions
sudo usermod -aG docker $USER
newgrp docker

# Restart Docker service (Linux)
sudo systemctl restart docker
```

### Test Divergences Detected

If parity tests fail with divergences:

1. **Review the divergence report** - Shows which fields differ between .NET and Node.js
2. **Check if it's a known difference** - See `docs/engine/e7.md` for deferred features
3. **Debug .NET Engine** - Add breakpoints in `src/ScreepsDotNet.Engine/Processors/`
4. **Debug Node.js harness** - Add `console.log()` in `tools/parity-harness/engine/test-runner/`
5. **Report the issue** - Create GitHub issue with full divergence report

### Manual Node.js Harness Execution

```bash
# Run Node.js harness manually (useful for debugging)
cd tools/parity-harness/engine
node test-runner/run-fixture.js \
  ../../../src/ScreepsDotNet.Engine.Tests/Parity/Fixtures/harvest_basic.json

# Output will be JSON with mutations, stats, actionLogs, etc.
```

## Cleanup

**MongoDB Cleanup** - Not needed! Testcontainers automatically stops and removes the MongoDB container after tests complete.

**Repos Cleanup** - If you want to remove cloned repos:
```bash
rm -rf tools/parity-harness/engine/repos
rm -rf tools/parity-harness/engine/node_modules

# Next test run will re-clone and re-install automatically
```

## Developer Workflow

### Regular Development (Skip Parity Tests)
```bash
# Fast - runs 533 tests, skips 7 parity tests (~250ms)
dotnet test

# Or explicitly exclude parity tests
dotnet test --filter "Category!=Parity"
```

### Before Committing Changes (Include Parity)
```bash
# Run all tests including parity (~2-3 minutes first run, ~30s subsequent)
dotnet test --filter Category=Parity
```

### CI/CD Integration

Add to `.github/workflows/test.yml`:
```yaml
- name: Run Parity Tests
  run: dotnet test --filter Category=Parity
```

Prerequisites (Node.js, Docker) are already available in GitHub Actions runners.

## Reference

### Test Infrastructure
- **EndToEndParityTests.cs** - Parity test suite (7 tests)
- **ParityTestPrerequisites.cs** - Automatic prerequisite checking (Node.js, Docker, repos, npm)
- **MongoDbParityFixture.cs** - Testcontainers MongoDB fixture
- **ParityComparator.cs** - Field-by-field comparison logic
- **DivergenceReporter.cs** - Formats divergence reports
- **NodeJsHarnessRunner.cs** - Node.js process executor
- **JsonFixtureLoader.cs** - JSON → RoomState loader

### Node.js Harness
- **tools/parity-harness/engine/** - Node.js harness implementation
- **tools/parity-harness/CLAUDE.md** - Node.js harness documentation
- **tools/parity-harness/engine/scripts/clone-repos.sh** - Clones official Screeps repos

### Documentation
- **docs/engine/e7.md** - E7 milestone (parity testing infrastructure)
- **docs/engine/roadmap.md** - Engine roadmap with parity testing status
