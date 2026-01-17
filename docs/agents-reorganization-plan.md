# AGENTS.md Reorganization Plan

## Current State Analysis

### Existing Files
1. **`ScreepsDotNet/AGENTS.md`** - Root solution-level guide
2. **`src/ScreepsDotNet.Driver/AGENTS.md`** - Driver day-to-day tasks
3. **`src/ScreepsDotNet.Engine/AGENTS.md`** - Engine rewrite roadmap
4. **`src/native/pathfinder/AGENTS.md`** - Native pathfinder status

### Current Issues
- Inconsistent structure across files (some use tables, some use bullet lists)
- No clear template/pattern for what goes in each AGENTS.md
- Mix of "what to do" (tasks) vs "how to do it" (conventions) vs "what's done" (status)
- Some duplication between root AGENTS.md and subsystem files
- Hard to quickly find actionable items vs context/reference material

## Proposed Structure

### 1. Root AGENTS.md Template

**Purpose:** Solution-wide onboarding, conventions, and navigation hub

**Structure:**
```markdown
# AGENT GUIDE

[Brief mission statement - 2-3 sentences]

## Quick Start
- Environment setup (link to docs/getting-started.md)
- First commands to run
- Authentication basics
- Where to find help

## Repo Orientation
- High-level directory structure
- What each major component does
- Git workflow notes (ScreepsNodeJs separation, etc.)

## Solution Layout
- Detailed project-by-project breakdown
- Dependencies between projects
- External dependencies (Docker, Node, etc.)

## Development Workflow
- Standard development cycle
- Running services locally
- Testing approach
- Debugging tips

## Coding Standards (Solution-wide)
- Language/framework conventions
- Analyzer rules (.editorconfig, etc.)
- Code formatting
- Repository patterns
- Testing patterns

## Storage & Infrastructure
- MongoDB schema overview
- Redis usage
- Seeding approach
- Reset workflows

## Where to Read Next
- Links to subsystem AGENTS.md files
- Links to key docs/ files
- Links to specs

## Current Focus
- Active work streams (high-level only)
- Links to detailed roadmaps in subsystem files

## Repository Conventions
- Git best practices
- Documentation update policy
- PR checklist
- Common pitfalls
```

**What NOT to include:**
- Subsystem-specific implementation details
- Detailed roadmaps (link to subsystem files)
- API/endpoint documentation (belongs in docs/)
- Completed work logs (use git history)

---

### 2. Subsystem AGENTS.md Template

**Purpose:** Tactical guide for working within a specific subsystem

**Structure:**
```markdown
# [Subsystem Name] AGENT

## Purpose
[1-2 sentence mission for this subsystem]

## Quick Context
- Where this fits in the overall solution
- Key dependencies (what it depends on, what depends on it)
- Link to detailed design docs

## Ground Rules
- Subsystem-specific coding conventions (if different from solution-wide)
- Testing requirements
- Performance considerations
- Breaking change policy

## Current Status
- What's working
- What's in progress
- What's blocked

## Active Work
- Current sprint/milestone
- Immediate next steps
- Known issues to avoid

## Roadmap
[Use a table for complex roadmaps like Engine]

| ID | Status | Title | Summary & Exit Criteria | Dependencies |
|----|--------|-------|-------------------------|--------------|
| X1 | Done   | ...   | ...                     | ...          |
| X2 | Active | ...   | ...                     | ...          |
| X3 | Pending| ...   | ...                     | ...          |

[Or use bullet lists for simpler roadmaps]

## Common Tasks
- How to: [frequent operation 1]
- How to: [frequent operation 2]
- Troubleshooting common issues

## Hand-offs & References
- Links to related subsystems
- Links to design docs
- Links to external dependencies
```

**What NOT to include:**
- Solution-wide standards (defer to root AGENTS.md)
- Detailed API documentation (belongs in docs/)
- Implementation tutorials (belongs in docs/)
- Historical notes about completed work (use git history)

---

## Reorganization Steps

### Phase 1: Root AGENTS.md Cleanup
1. ✅ **DONE:** Move solution-wide content from docs/backend.md to root AGENTS.md
2. ✅ **DONE:** Add "Coding Standards" section
3. ✅ **DONE:** Add "Storage & Seeding" section
4. ✅ **DONE:** Add "Development Workflow" section
5. **TODO:** Add "Quick Start" section at the top for immediate onboarding
6. **TODO:** Reorganize "Current Focus" to be high-level only (detailed roadmaps stay in subsystem files)
7. **TODO:** Add "Debugging Tips" section
8. **TODO:** Review and streamline - ensure nothing duplicates subsystem files

### Phase 2: Driver AGENTS.md Restructure
1. **TODO:** Adopt the subsystem template structure
2. **TODO:** Add "Purpose" section at top
3. **TODO:** Add "Quick Context" linking to driver.md
4. **TODO:** Move detailed conventions currently in root to here (if driver-specific)
5. **TODO:** Ensure "Active Work" section is actionable and current
6. **TODO:** Add "Common Tasks" section (e.g., "How to add a new processor", "How to debug runtime issues")
7. **TODO:** Remove any solution-wide content that belongs in root

### Phase 3: Engine AGENTS.md Restructure
1. **TODO:** Keep the table-based roadmap (it works well)
2. **TODO:** Add "Purpose" and "Quick Context" sections
3. **TODO:** Add "Common Tasks" section once implementation starts
4. **TODO:** Consider splitting E2 (Data Model) progress notes into a separate tracking doc
5. **TODO:** Move "Immediate Next Steps" to "Active Work" section
6. **TODO:** Archive completed milestones or move to a "Completed" section at bottom

### Phase 4: Native Pathfinder AGENTS.md Restructure
1. **TODO:** Adopt subsystem template
2. **TODO:** Move "Current Status" bullets into structured sections (Done, Active, Pending)
3. **TODO:** Add "Common Tasks" section (e.g., "How to rebuild binaries", "How to update baselines")
4. **TODO:** Consider renaming to focus on build/release rather than status
5. **TODO:** Clarify relationship to Driver (dependency flow)

### Phase 5: Cross-File Consistency
1. **TODO:** Ensure all AGENTS.md files link back to root
2. **TODO:** Ensure all AGENTS.md files use consistent markdown formatting
3. **TODO:** Ensure all files updated in last 30 days have "Last Updated" timestamp
4. **TODO:** Add a footer template to all files pointing to docs/README.md for ownership map
5. **TODO:** Create a checklist in docs/README.md for "When to update AGENTS.md files"

---

## Content Ownership Principles

### Root AGENTS.md Owns:
- ✅ Solution-wide coding standards
- ✅ Repository conventions (git workflow, Docker usage)
- ✅ Solution layout (project descriptions)
- ✅ Storage/seeding approach
- ✅ Development workflow (common to all subsystems)
- ✅ Links to all subsystem files

### Subsystem AGENTS.md Owns:
- ✅ Subsystem-specific conventions
- ✅ Roadmap/milestone tracking
- ✅ Active work and blockers
- ✅ Common operations specific to that subsystem
- ✅ Dependency notes (what depends on this, what this depends on)

### docs/ Owns:
- ✅ Detailed design documentation
- ✅ API/endpoint specifications
- ✅ Setup tutorials
- ✅ Architecture deep-dives
- ✅ Historical context and decisions

### Code Comments Own:
- Implementation details
- Complex algorithm explanations
- TODOs for small scoped items

---

## Template Files to Create

1. **`.github/AGENTS_TEMPLATE_ROOT.md`** - Template for root-level AGENTS.md
2. **`.github/AGENTS_TEMPLATE_SUBSYSTEM.md`** - Template for subsystem AGENTS.md
3. **`.github/AGENTS_CHECKLIST.md`** - When to update which AGENTS.md file

---

## Success Criteria

- ✅ Any contributor can find coding standards in <30 seconds
- ✅ Any contributor can find active work in a subsystem in <60 seconds
- ✅ No duplicated information between root and subsystem files
- ✅ Clear navigation from root → subsystem → detailed docs
- ✅ Consistent structure makes files scannable
- ✅ Files stay current (not a dumping ground for stale TODOs)

---

## Migration Notes

- Don't delete content hastily - move it to appropriate locations
- Keep git history by using `git mv` when renaming/restructuring
- Update docs/README.md ownership table as you reorganize
- Test navigation paths: can you get from root → subsystem → implementation in 3 clicks?
- Consider creating a "decisions.md" or "ADR/" directory for historical context currently cluttering AGENTS files

---

## Next Steps

1. Get feedback on this plan
2. Execute Phase 1 (Root cleanup)
3. Execute Phases 2-4 in parallel (subsystem restructures)
4. Execute Phase 5 (consistency pass)
5. Create templates for future use
6. Update docs/README.md with new ownership rules
