# Engine Roadmap (E1-E8)

**Last Updated:** January 21, 2026

This document tracks the Engine subsystem roadmap and implementation status. For detailed handler tracking, see `e2.md`. For E5 blockers, see `e5.md`.

---

## Milestone Overview

| ID | Status | Title | Exit Criteria | Dependencies |
|----|--------|-------|---------------|--------------|
| E1 | ✅ | Map Legacy Engine Surface | Node engine API inventory documented (`e1.md`) | Node engine repo, driver notes |
| E2 | ⚠️ 95% | Data & Storage Model | Driver snapshot/mutation contracts in place, Engine consuming them. Handlers for all intent types. | Driver contracts, Screeps schemas |
| E3 | ⚠️ 60% | Intent Gathering & Validation | `IIntentPipeline` + validators with unit tests mirroring Node fixtures | Driver runtime outputs, constants |
| E4 | 📋 | Simulation Kernel (Room Processor) | Managed processor produces identical room diffs vs. Node baseline | E2, E3, Pathfinder service |
| E5 | 📋 | Global Systems | Market, NPC spawns, shard messaging hooked into processor loop. Global mutations (`IGlobalMutationWriter`), power effect tracking. | E4 foundation |
| E6 | 📋 | Engine Loop Orchestration | `EngineHost` coordinates ticks; main/runner/processor loops call managed engine | Driver queue service, telemetry sink |
| E7 | 📋 | Compatibility & Parity Validation | Lockstep testing vs. Node engine, automated divergence detection | Prior steps, legacy engine repo |
| E8 | 📋 | Observability & Tooling | Engine metrics flow to telemetry, diagnostics commands, operator playbooks | D8 logging stack, scheduler hooks |

---

## E1: Map Legacy Engine Surface ✅

**Status:** Complete

**Deliverables:**
- ✅ Node engine API inventory documented in `e1.md`
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

**Details:** See `e2.md` for handler breakdown and deferred features, `e5.md` for E5 blockers, `data-model.md` for contracts

---

## E3: Intent Gathering & Validation ⚠️ 60% Complete

**Status:** E3.1 ✅ Complete | E3.2 ✅ Complete | E3.3 📋 Pending | E3.4 📋 Pending

**Completed Deliverables:**
- ✅ `IIntentValidator` and `IIntentPipeline` interfaces
- ✅ 5 validator implementations: Range (28 tests), Resource (18 tests), Permission (20 tests), State (15 tests), Schema (15 tests)
- ✅ Validation constants (ValidationRanges, ValidationErrorCode, ResourceRequirements, PermissionRules, StateRequirements)
- ✅ 96/96 validator tests passing
- ✅ DI registration infrastructure

**Pending Deliverables:**
- 📋 `IntentValidationPipeline` implementation (E3.3)
- 📋 Integration with Engine processors - remove inline validation (E3.3)
- 📋 Observability metrics (E3.4)
- 📋 Parity validation against Node.js (deferred to E7)

**Dependencies:**
- E2 95% complete (handler infrastructure in place) ✅
- Driver runtime outputs ✅
- Constants for validation rules ✅

**Exit Criteria:**
- ✅ All 5 validators implemented and tested (96 tests)
- ✅ All validation constants defined
- 📋 IntentValidationPipeline orchestrates validators (E3.3)
- 📋 All E2 tests continue passing after integration (E3.3)
- 📋 Parity with Node.js validation (E7)
- 📋 Validation overhead <5ms per room (measure in E3.3)

**Details:** See `e3.md` for detailed implementation plan, `e3.1.md` and `e3.2.md` for completed work

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

**Details:** See `e5.md` for implementation plan and E2 blockers

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
- E3 validator parity tests (deferred from E3.2)

**Prerequisites:**
- All E2 features complete (including 4 E5-blocked features)
- All E1-E6 milestones complete
- E3 validators implemented (✅ E3.2 complete)
- Legacy Node.js engine repo access

**Details:** See `e2.md` for parity-critical feature status, `e3.md` for deferred E3 parity validation

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

**Overall Engine Progress:** E1 complete, E2 95% complete (4 features blocked by E5), E3 60% complete (validators done, integration pending), E4-E8 pending

**Critical Path:**
1. Complete E3.3 (Pipeline Integration) → enables E4 simulation kernel work
2. Complete E5 Phase 1 (Global Mutations) → unblocks E2.3 remaining 5%
3. Complete E2.3 → full E2 completion
4. Complete E3.4 (Observability) → E3 fully complete
5. Complete E4/E5 → enables E6 (Orchestration)
6. Complete E6 → enables E7 (Parity Validation)
7. E8 can proceed in parallel with E6/E7

**Next Milestone:** E3.3 (Pipeline Integration) OR E5 Phase 1 (Global Mutations) - can proceed in parallel

**Reference Documents:**
- E1 (Legacy surface mapping): `e1.md`
- E2 (Handlers and deferred features): `e2.md`
- E3 (Intent validation overview): `e3.md`
  - E3.1 (Validation infrastructure): `e3.1.md` ✅
  - E3.2 (Validator implementation): `e3.2.md` ✅
- E5 (Global systems blockers): `e5.md`
- Data model design: `data-model.md`
- Coding patterns: `../../src/ScreepsDotNet.Engine/CLAUDE.md`
