# Engine Roadmap (E1-E8)

**Last Updated:** January 21, 2026

This document tracks the Engine subsystem roadmap and implementation status. For detailed handler tracking, see `e2.3-plan.md`. For E5 blockers, see `e5-plan.md`.

---

## Milestone Overview

| ID | Status | Title | Exit Criteria | Dependencies |
|----|--------|-------|---------------|--------------|
| E1 | ✅ | Map Legacy Engine Surface | Node engine API inventory documented (`docs/engine/legacy-surface.md`) | Node engine repo, driver notes |
| E2 | ⚠️ 95% | Data & Storage Model | Driver snapshot/mutation contracts in place, Engine consuming them. Handlers for all intent types. | Driver contracts, Screeps schemas |
| E3 | 📋 | Intent Gathering & Validation | `IIntentPipeline` + validators with unit tests mirroring Node fixtures | Driver runtime outputs, constants |
| E4 | 📋 | Simulation Kernel (Room Processor) | Managed processor produces identical room diffs vs. Node baseline | E2, E3, Pathfinder service |
| E5 | 📋 | Global Systems | Market, NPC spawns, shard messaging hooked into processor loop. Global mutations (`IGlobalMutationWriter`), power effect tracking. | E4 foundation |
| E6 | 📋 | Engine Loop Orchestration | `EngineHost` coordinates ticks; main/runner/processor loops call managed engine | Driver queue service, telemetry sink |
| E7 | 📋 | Compatibility & Parity Validation | Lockstep testing vs. Node engine, automated divergence detection | Prior steps, legacy engine repo |
| E8 | 📋 | Observability & Tooling | Engine metrics flow to telemetry, diagnostics commands, operator playbooks | D8 logging stack, scheduler hooks |

---

## E1: Map Legacy Engine Surface ✅

**Status:** Complete

**Deliverables:**
- ✅ Node engine API inventory documented in `docs/engine/legacy-surface.md`
- ✅ All game mechanics catalogued (creep actions, structure logic, combat, market, etc.)

---

## E2: Data & Storage Model ⚠️ 95% Complete

**Status:** Nearly complete, 4 features blocked by E5

**Key Deliverables:**
- ✅ Driver contracts and snapshot providers
- ✅ Engine consumes only driver abstractions (no direct DB access)
- ✅ 11/11 handler families implemented (240/240 tests)
- ✅ Room mutation writers and memory persistence
- ❌ 4 features blocked by E5 global mutations:
  - PWR_GENERATE_OPS power ability
  - User power balance tracking (PowerSpawn)
  - User GCL updates (Controller)
  - Boost effects GCL component (Controller)

**Exit Criteria:**
- All room-level intent handlers complete ✅
- Engine isolated from storage layer ✅
- E5 global mutations implemented ❌ (blocker)
- 4 blocked features implemented (1-2 hours after E5 Phase 1)

**Details:** See `e2.3-plan.md` for handler breakdown, `e5-plan.md` for E5 blockers, `data-model.md` for contracts

---

## E3: Intent Gathering & Validation 📋

**Status:** Not Started

**Planned Deliverables:**
- `IIntentPipeline` interface for intent processing
- Intent schema validation
- Range checks (creep near target?)
- Resource availability checks
- Permission checks (ownership, etc.)
- Unit tests mirroring Node fixtures

**Dependencies:**
- E2 completion (handler infrastructure in place)
- Driver runtime outputs
- Constants for validation rules

---

## E4: Simulation Kernel (Room Processor) 📋

**Status:** Not Started

**Planned Deliverables:**
- Complete room processor implementation
- All game mechanics functional
- Room diffs match Node.js baseline
- Performance optimizations

**Dependencies:**
- E2 (data model)
- E3 (intent validation)
- Pathfinder service

---

## E5: Global Systems 📋

**Status:** Not Started (Blocking 4 E2.3 features)

**Planned Deliverables:**
- Global mutation infrastructure (`IGlobalMutationWriter`)
- User GCL/power balance tracking
- Market operations (global order matching, NPC maintenance)
- NPC spawns (invaders, source keepers)
- Shard messaging

**Impact:** Unblocks 4 E2.3 features (see E2 section above)

**Dependencies:** E4 foundation

**Details:** See `e5-plan.md` for implementation plan and E2.3 blockers

---

## E6: Engine Loop Orchestration 📋

**Status:** Not Started

**Planned Deliverables:**
- `EngineHost` coordinates tick execution
- Main/runner/processor loops call managed engine instead of Node.js
- Tick scheduling and coordination
- Error handling and recovery

**Dependencies:**
- Driver queue service
- Telemetry sink
- E4/E5 completion

---

## E7: Compatibility & Parity Validation 📋

**Status:** Not Started

**Planned Deliverables:**
- Lockstep testing framework (run same fixture through both engines)
- Automated divergence detection
- Parity test suite covering all mechanics
- Regression detection on schema changes

**Prerequisites:**
- All E2.3 features complete (including 4 E5-blocked features)
- All E1-E6 milestones complete
- Legacy Node.js engine repo access

**Details:** See `e2.3-plan.md` for parity-critical feature status

---

## E8: Observability & Tooling 📋

**Status:** Not Started

**Planned Deliverables:**
- Engine metrics flow to telemetry
- Diagnostics commands (inspect room state, intent queue, etc.)
- Operator playbooks for debugging
- Performance profiling tools

**Dependencies:**
- D8 logging stack
- Scheduler hooks
- E6 orchestration

---

## Summary

**Overall Engine Progress:** E1 complete, E2 95% complete (4 features blocked by E5), E3-E8 pending

**Critical Path:**
1. Complete E5 Phase 1 (Global Mutations) → unblocks E2.3 remaining 5%
2. Complete E2.3 → enables E3 (Intent Validation)
3. Complete E3 → enables E4 (Simulation Kernel)
4. Complete E4/E5 → enables E6 (Orchestration)
5. Complete E6 → enables E7 (Parity Validation)
6. E8 can proceed in parallel with E6/E7

**Next Milestone:** E5 Phase 1 (Global Mutations) to unblock E2.3

**Reference Documents:**
- Detailed E2.3 handler tracking: `e2.3-plan.md`
- E5 blockers and implementation plan: `e5-plan.md`
- Data model design: `data-model.md`
- Coding patterns: `src/ScreepsDotNet.Engine/CLAUDE.md`
