# Screeps Repository Version Pinning

## Purpose

Pins official Screeps repositories (engine, driver, common) to specific commit hashes for reproducible parity testing. This ensures:
- ✅ Consistent test results across environments
- ✅ Controlled updates when upstream repos change
- ✅ Ability to bisect issues when divergences appear
- ✅ Stable CI/CD builds

## How It Works

### 1. Clone Scripts Read versions.json

When you run clone scripts:
```bash
# Linux/Mac
cd tools/parity-harness/screeps-modules/scripts
./clone-repos.sh

# Windows
pwsh ./clone-repos.ps1
```

They:
1. Read `pinningEnabled` from versions.json
2. If enabled, use commit hashes from `pins.*`
3. Run `git checkout <commit-hash>` for each repo
4. Install npm dependencies

### 2. Parity Tests Use Pinned Versions

When you run parity tests:
```bash
dotnet test --filter Category=Parity
```

The `ParityTestPrerequisites` fixture:
1. Checks if repos exist in `tools/parity-harness/screeps-modules/`
2. If missing, runs clone scripts (uses pinned commits)
3. Executes tests against pinned versions

**Result:** Tests always run against same Screeps code, ensuring reproducibility.

## Updating Pins

### When to Update

Update pins when:
- ✅ Weekly/monthly maintenance (stay current with upstream)
- ✅ After fixing divergences (validate fixes against latest)
- ✅ When upstream bug fixes are needed
- ⚠️ NOT during active development (keeps environment stable)

### How to Update (Automated)

**Linux/Mac:**
```bash
cd tools/parity-harness/screeps-modules/scripts
./update-pins.sh
```

**Windows:**
```powershell
cd tools/parity-harness/screeps-modules/scripts
pwsh ./update-pins.ps1
```

**What it does:**
1. Fetches latest commit hashes from GitHub
2. Shows current vs. latest commits
3. Prompts for confirmation
4. Updates versions.json with new hashes
5. Updates `lastValidated` date

**Example output:**
```
Current pins:
  engine: 97c9d12385fed686655c13b09f5f2457dd83a2bf
  driver: e691bd3ee843cb12ac4bedc68397b2b92709f622
  common: 4466b9a265400024187e9cc064be27762f8dc426

Latest commits:
  engine: a1b2c3d4e5f6...
  driver: e691bd3ee843... (no change)
  common: 7f8g9h0i1j2k...

✓ engine has updates
✓ common has updates

Update versions.json with latest commits? [y/N] y

✓ Updated versions.json

Next steps:
1. Run parity tests: dotnet test --filter Category=Parity
2. If tests pass, commit versions.json
3. If divergences found, fix .NET Engine, then commit
```

### How to Update (Manual)

If you need to pin to a specific commit (e.g., bisecting an issue):

1. Find commit hash on GitHub:
   - https://github.com/screeps/engine/commits/master
   - https://github.com/screeps/driver/commits/master
   - https://github.com/screeps/common/commits/master

2. Edit `tools/parity-harness/versions.json`:
   ```json
   {
     "engine": {
       "pinningEnabled": true,
       "pins": {
         "engine": "<commit-hash>",
         "driver": "<commit-hash>",
         "common": "<commit-hash>"
       },
       "lastValidated": "2026-01-22",
       "notes": "Reason for manual pin"
     }
   }
   ```

3. Delete screeps-modules to force re-clone:
   ```bash
   rm -rf tools/parity-harness/screeps-modules
   ```

4. Run parity tests:
   ```bash
   dotnet test --filter Category=Parity
   ```

## Workflow Examples

### Example 1: Weekly Maintenance

```bash
# 1. Update pins to latest
cd tools/parity-harness/screeps-modules/scripts
./update-pins.sh

# 2. Run parity tests
cd ../../../..
dotnet test --filter Category=Parity

# 3. If tests pass, commit
git add tools/parity-harness/versions.json
git commit -m "chore: update Screeps repo pins to latest"

# 4. If divergences found, fix .NET Engine first
# ... fix code ...
dotnet test --filter Category=Parity  # Verify fix
git add .
git commit -m "fix(engine): resolve parity divergence in <feature>"
git add tools/parity-harness/versions.json
git commit -m "chore: update Screeps repo pins after parity fix"
```

### Example 2: Bisecting an Issue

```bash
# 1. Determine when divergence appeared
# Check git log for versions.json changes

# 2. Manually pin to known-good commit
# Edit versions.json with older commit hash

# 3. Delete repos to force re-clone
rm -rf tools/parity-harness/screeps-modules

# 4. Test
dotnet test --filter Category=Parity

# 5. Binary search through commits
# Repeat steps 2-4 with different commit hashes

# 6. Once issue found, fix .NET Engine
# ... fix code ...

# 7. Update to latest and verify
./update-pins.sh
dotnet test --filter Category=Parity
```

### Example 3: Disabling Pinning (Not Recommended)

If you want to always use latest (e.g., for development against bleeding edge):

```json
{
  "engine": {
    "pinningEnabled": false,
    "pins": {
      "engine": "master",
      "driver": "master",
      "common": "master"
    }
  }
}
```

**Warning:** This makes tests non-reproducible. CI builds may fail randomly when upstream changes.

## CI/CD Integration (Future)

**When Phase 5 is implemented:**

1. **PR builds:** Use pinned commits (stable, reproducible)
2. **Scheduled weekly job:** Test against latest commits
   - If divergences found, create GitHub issue
   - Assign to engine maintainers
   - Pin stays at last known-good commit
3. **After fixes:** Update pins manually after validation

## Troubleshooting

### "jq: command not found" (update-pins.sh)

**Install jq:**
- Ubuntu/Debian: `sudo apt install jq`
- macOS: `brew install jq`
- Windows: Use PowerShell version (`update-pins.ps1`)

### Repos won't update after changing versions.json

**Solution:** Delete screeps-modules and re-clone:
```bash
rm -rf tools/parity-harness/screeps-modules
dotnet test --filter Category=Parity  # Will auto-clone with new pins
```

### Want to see what commit is currently checked out

```bash
cd tools/parity-harness/screeps-modules/engine
git log -1 --oneline

cd ../driver
git log -1 --oneline

cd ../common
git log -1 --oneline
```

## References

**versions.json schema:**
```json
{
  "engine": {
    "pinningEnabled": true,        // Enable commit pinning
    "pins": {
      "engine": "<commit-hash>",   // Full commit SHA
      "driver": "<commit-hash>",
      "common": "<commit-hash>"
    },
    "lastValidated": "YYYY-MM-DD",  // Last validation date
    "notes": "Free-form notes"
  }
}
```

**Official Screeps Repositories:**
- Engine: https://github.com/screeps/engine
- Driver: https://github.com/screeps/driver
- Common: https://github.com/screeps/common

**Related Documentation:**
- `tools/parity-harness/CLAUDE.md` - Parity harness AI context
- `docs/engine/e7.md` - E7 parity testing milestone
- `docs/engine/mongodb-parity-setup.md` - Setup guide
