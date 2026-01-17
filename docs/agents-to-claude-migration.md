# Migration Plan: AGENTS.md → CLAUDE.md

## Executive Summary

**Goal:** Transition from AGENTS.md to CLAUDE.md convention for better Claude Code integration

**Timeline:** 5 phases, can be done incrementally

**Breaking Changes:** None - this is purely organizational

**Benefits:**
- ✅ Follows Claude Code community standard
- ✅ AI-optimized format (more examples, clearer instructions)
- ✅ Better separation: CLAUDE.md for AI, README.md for humans
- ✅ Self-contained context (less reliance on external links)
- ✅ Pattern-heavy approach reduces ambiguity

## Current vs. Proposed

### Current Structure
```
ScreepsDotNet/
├── AGENTS.md                    # Solution-wide (for AI or humans?)
├── README.md                    # Project overview
└── src/
    ├── ScreepsDotNet.Driver/
    │   └── AGENTS.md           # Driver-specific
    ├── ScreepsDotNet.Engine/
    │   └── AGENTS.md           # Engine-specific
    └── native/pathfinder/
        └── AGENTS.md           # Pathfinder-specific
```

### Proposed Structure
```
ScreepsDotNet/
├── CLAUDE.md                    # AI-first solution context
├── README.md                    # Human-facing overview
├── docs/                        # Human-facing detailed docs
└── src/
    ├── ScreepsDotNet.Driver/
    │   ├── CLAUDE.md           # AI-first driver context
    │   └── docs/               # Human-facing design docs
    ├── ScreepsDotNet.Engine/
    │   ├── CLAUDE.md           # AI-first engine context
    │   └── docs/               # Human-facing design docs
    └── native/pathfinder/
        └── CLAUDE.md           # AI-first pathfinder context
```

## Key Differences: AGENTS.md vs CLAUDE.md

| Aspect | AGENTS.md | CLAUDE.md |
|--------|-----------|-----------|
| **Audience** | AI or human (ambiguous) | AI agent (explicit) |
| **Tone** | Reference guide | Imperative instructions |
| **Examples** | Minimal, mostly links | Extensive code examples |
| **Context** | Links to docs | Inline critical context |
| **Structure** | Variable | Standardized template |
| **Anti-patterns** | Rarely mentioned | Explicitly shown (❌ vs ✅) |
| **Completeness** | Can be sparse | Self-contained |
| **Discovery** | Manual | Claude Code looks for it |

## Example Comparison

### AGENTS.md Style (Current)
```markdown
## Coding Standards

- Respect the shared style rules (implicit usings, expression-bodied members,
  collection expressions, `Lock` type for locks).
```

### CLAUDE.md Style (Proposed)
```markdown
## Coding Standards (Enforced)

### Collection Expressions
**✅ Good:**
```csharp
var items = [];  // Use collection expressions
var dict = new Dictionary<string, int>();  // OK for non-empty
```

**❌ Bad:**
```csharp
var items = new List<string>();  // Don't use old syntax for empty
```

### Locks
**✅ Good:**
```csharp
private readonly Lock _lock = new();
```

**❌ Bad:**
```csharp
private readonly object _lock = new object();  // Don't use object
```
```

**Why CLAUDE.md is better:**
- Shows exact code patterns, not just rules
- AI can match patterns more reliably
- Visual distinction (✅/❌) is clear
- No ambiguity about what "respect style rules" means

## Migration Phases

### Phase 1: Create Root CLAUDE.md ✅ Template Provided

**Actions:**
1. Use `docs/claude-md-migration-plan.md` as starting point
2. Migrate content from current `AGENTS.md`
3. Add inline examples for all coding standards
4. Add ✅/❌ pattern examples
5. Expand "Common Tasks" with step-by-step instructions
6. Test: Can Claude understand context without external links?

**Success Criteria:**
- Root CLAUDE.md is self-contained for basic tasks
- All critical rules have code examples
- Storage/seeding context is inline
- No ambiguous instructions

**Estimated Effort:** 2-3 hours

---

### Phase 2: Create Driver CLAUDE.md

**Actions:**
1. Use `docs/claude-md-subsystem-template.md` as base
2. Port content from `src/ScreepsDotNet.Driver/AGENTS.md`
3. Add code examples for:
   - How to add a processor
   - How to wire up telemetry
   - How to test runtime integration
4. Add dependency diagram (ASCII or inline)
5. Clarify "Active Work" vs "Planned" vs "Done"

**Current AGENTS.md Issues to Fix:**
- Mix of plan context and tactical instructions
- Hard to find "how do I do X" quickly
- Roadmap table in docs/driver.md but status in AGENTS.md
- Not clear what conventions are driver-specific vs solution-wide

**New CLAUDE.md Structure:**
```markdown
# Driver - Claude Context

## Purpose
[Clear 1-sentence mission]

## Critical Rules
- Never call Mongo/Redis directly (use driver abstractions)
- [Other driver-specific rules]

## Dependencies
- Uses: Storage abstractions, ClearScript runtime
- Consumed by: Engine, CLI commands

## Coding Patterns
[Show 3-5 common patterns with ✅/❌ examples]

## Current Status
- ✅ Done: Queue infrastructure, runtime pooling, telemetry
- 🔄 Active: D7 processor handlers, pathfinder integration
- 📋 Planned: D9-D10 (see roadmap)

## Roadmap
[Table or bullets with status indicators]

## Common Tasks
### Add a New Processor
1. Create handler in `Processors/Handlers/`
2. Register in DI
3. Add tests in `Tests/Processors/`
[More tasks...]

## Reference
- Design: `docs/driver.md`
- Related: `src/ScreepsDotNet.Engine/CLAUDE.md`
```

**Success Criteria:**
- Any common driver task has step-by-step instructions
- Clear what's driver-specific vs solution-wide
- Roadmap status is current and accurate

**Estimated Effort:** 2-3 hours

---

### Phase 3: Create Engine CLAUDE.md

**Actions:**
1. Use subsystem template
2. Port E1-E8 roadmap from current AGENTS.md
3. Add patterns for:
   - How engine consumes driver abstractions
   - How to add a new intent handler
   - How to test parity with legacy engine
4. Clarify "no direct DB access" rule with examples

**Current AGENTS.md Strengths to Keep:**
- The E1-E8 table is excellent
- Execution plan is clear
- Progress tracking is detailed

**Current Issues to Fix:**
- Mixing roadmap with tactical guidance
- E2.3 handler backlog buried in prose
- Not clear what "immediate next steps" are

**New CLAUDE.md Structure:**
```markdown
# Engine - Claude Context

## Purpose
Rebuild the Screeps simulation kernel in .NET with API compatibility

## Critical Rules
- ❌ NEVER call Mongo/Redis directly (use driver layer)
- ✅ ALWAYS consume data via `RoomStateProvider`/`GlobalStateProvider`
- ✅ ALWAYS emit mutations via `RoomMutationWriterFactory`

## Dependencies
[Clear dependency flow diagram]

## Coding Patterns
### Consuming Room State
**✅ Good:**
```csharp
var roomState = await _roomStateProvider.GetRoomStateAsync(roomId);
```
**❌ Bad:**
```csharp
var room = await _mongoClient.GetDatabase("screeps")
    .GetCollection<Room>("rooms").Find(...)  // Never do this
```

## Current Status
- ✅ E1: Complete - Legacy surface mapped
- 🔄 E2: In Progress - Data model (see roadmap)
- 📋 E3-E8: Planned

## Roadmap
[Keep the excellent E1-E8 table]

## Active Work (Immediate)
1. Finish E2.3 handler backlog:
   - Controller intents (upgrade/reserve/attack)
   - Resource I/O (transfer/withdraw/pickup/drop)
   - Lab boosts/reactions
   [Specific, actionable items]

## Common Tasks
### Add a New Intent Handler
[Step by step]

### Test Engine vs Legacy Parity
[Step by step]

## Reference
- Design: `docs/engine/data-model.md`
- Related: `src/ScreepsDotNet.Driver/CLAUDE.md`
```

**Success Criteria:**
- Roadmap is scannable (table with clear status)
- Active work is explicit (no hunting)
- Patterns prevent direct DB access mistakes

**Estimated Effort:** 1-2 hours (roadmap is already good)

---

### Phase 4: Create Pathfinder CLAUDE.md

**Actions:**
1. Use subsystem template
2. Port status from current AGENTS.md
3. Add patterns for:
   - How to rebuild binaries locally
   - How to update baselines
   - How to verify GitHub release
4. Clarify relationship to Driver

**Current AGENTS.md Strengths:**
- Detailed progress log
- Clear "what's done" vs "what's next"

**Current Issues:**
- Mostly status updates, not tactical guidance
- Not clear how to actually work with pathfinder
- Relationship to Driver is implicit

**New CLAUDE.md Structure:**
```markdown
# Native Pathfinder - Claude Context

## Purpose
P/Invoke wrapper for upstream Screeps C++ pathfinder

## Critical Rules
- Managed fallback removed (native required)
- Binaries auto-downloaded during build (hash-verified)
- Never modify pathfinder source without updating baselines

## Dependencies
- Consumed by: Driver's `PathfinderService`
- Build tools: CMake, C++ compiler per platform

## Current Status
- ✅ Native library compiles for all RIDs
- ✅ P/Invoke bindings working
- ✅ Parity tests passing (100% match with Node)
- 🔄 Ongoing: Baseline refresh automation

## Common Tasks
### Rebuild Binaries Locally
```bash
cd src/native/pathfinder
./build.sh linux-x64  # or win-x64, osx-arm64, etc.
```

### Update Parity Baselines
```bash
cd src/native/pathfinder
node scripts/run-legacy-regressions.js
# Review reports/legacy-regressions.md
dotnet test /p:RefreshPathfinderBaselines=true
```

### Verify GitHub Release
[Step by step]

## Reference
- Design: `docs/driver.md` (D6 milestone)
- Related: `src/ScreepsDotNet.Driver/CLAUDE.md`
```

**Success Criteria:**
- Clear how to rebuild/test locally
- Baseline refresh process is documented
- Relationship to Driver is explicit

**Estimated Effort:** 1 hour

---

### Phase 5: Deprecate AGENTS.md

**Actions:**
1. Rename all AGENTS.md → AGENTS.md.deprecated
2. Add redirect files:
   ```markdown
   # This file has moved
   See CLAUDE.md in this directory.
   ```
3. Update all references in docs/README.md
4. Update all cross-references in other files
5. Update startup hook if it mentions AGENTS.md
6. Add note in README.md about CLAUDE.md convention

**Success Criteria:**
- No file references AGENTS.md
- All links point to CLAUDE.md
- Startup hook shows CLAUDE.md content
- Contributors understand CLAUDE.md is the source of truth

**Estimated Effort:** 30 minutes

---

## Migration Checklist

### Pre-Migration
- [ ] Review `docs/claude-md-migration-plan.md` (root template)
- [ ] Review `docs/claude-md-subsystem-template.md`
- [ ] Get approval on approach
- [ ] Back up current AGENTS.md files

### Phase 1: Root
- [ ] Create `ScreepsDotNet/CLAUDE.md`
- [ ] Port content from current `AGENTS.md`
- [ ] Add ✅/❌ code examples for all standards
- [ ] Inline storage/seeding context
- [ ] Add "Common Tasks" section
- [ ] Test: Claude can understand without links
- [ ] Update `README.md` to reference CLAUDE.md

### Phase 2: Driver
- [ ] Create `src/ScreepsDotNet.Driver/CLAUDE.md`
- [ ] Port from current `AGENTS.md`
- [ ] Add coding pattern examples
- [ ] Clarify active work vs planned
- [ ] Add "Common Tasks" (add processor, wire telemetry)
- [ ] Update cross-references

### Phase 3: Engine
- [ ] Create `src/ScreepsDotNet.Engine/CLAUDE.md`
- [ ] Port E1-E8 roadmap
- [ ] Add coding pattern examples (never direct DB)
- [ ] Extract immediate next steps from prose
- [ ] Add "Common Tasks" (add handler, test parity)
- [ ] Update cross-references

### Phase 4: Pathfinder
- [ ] Create `src/native/pathfinder/CLAUDE.md`
- [ ] Port status from current `AGENTS.md`
- [ ] Add rebuild/test instructions
- [ ] Add baseline refresh process
- [ ] Clarify Driver relationship
- [ ] Update cross-references

### Phase 5: Cleanup
- [ ] Rename all `AGENTS.md → AGENTS.md.deprecated`
- [ ] Add redirect notices
- [ ] Update `docs/README.md` ownership table
- [ ] Update all cross-references
- [ ] Update startup hook
- [ ] Update `.serena/memories/` if needed
- [ ] Final review: search for "AGENT" (not "AGENTS")

### Post-Migration
- [ ] Test Claude Code with new CLAUDE.md files
- [ ] Verify navigation flows work
- [ ] Update PR checklist in `docs/README.md`
- [ ] Monitor for 1-2 weeks, adjust as needed
- [ ] Delete AGENTS.md.deprecated files after confidence

---

## Rollback Plan

If CLAUDE.md doesn't work well:

1. Rename `CLAUDE.md → CLAUDE.md.experimental`
2. Rename `AGENTS.md.deprecated → AGENTS.md`
3. Restore old references
4. Document lessons learned
5. Iterate on approach

**Risk:** Low - this is just file organization

---

## Open Questions

1. **Should we keep AGENTS.md as a human-readable summary?**
   - Pro: Humans might prefer lighter-weight reference
   - Con: Duplication risk
   - **Recommendation:** No - use README.md for humans, CLAUDE.md for AI

2. **Should subsystem docs/ folders have CLAUDE.md too?**
   - Example: `docs/engine/CLAUDE.md` for engine-specific design context?
   - Pro: Could provide design context to AI
   - Con: Mixing docs/ (human) with CLAUDE.md (AI)
   - **Recommendation:** No - keep CLAUDE.md at project root only

3. **What about .serena/memories/?**
   - Should these reference CLAUDE.md?
   - **Recommendation:** Yes - update memories to point to CLAUDE.md

---

## Success Metrics

After migration, measure:

1. **Discoverability:** Can Claude find context in <5 seconds?
2. **Completeness:** Can common tasks be done without external links?
3. **Clarity:** Are ambiguous instructions eliminated?
4. **Maintenance:** Are CLAUDE.md files staying current?
5. **Adoption:** Are contributors updating CLAUDE.md naturally?

**Target:** All metrics should be subjectively "better" than AGENTS.md

---

## Next Steps

1. **Review this plan** - Get feedback from human user
2. **Execute Phase 1** - Create root CLAUDE.md
3. **Test** - Use Claude Code with new file, validate approach
4. **Iterate** - Adjust based on learnings
5. **Execute Phases 2-5** - Complete migration
6. **Monitor** - Watch for issues, improve templates

---

## Templates

Templates available in:
- `docs/claude-md-migration-plan.md` - Root CLAUDE.md template
- `docs/claude-md-subsystem-template.md` - Subsystem CLAUDE.md template

Use these as starting points, adapt as needed.
