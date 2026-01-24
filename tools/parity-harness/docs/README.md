# Parity Harness Documentation

This directory contains comprehensive analysis and documentation of the ScreepsDotNet Engine's parity with the official Node.js Screeps engine.

## Documents

### [parity-analysis.md](parity-analysis.md)
**Comprehensive Engine Parity Analysis** - Complete comparison between Node.js and .NET implementations.

**Contents:**
- Intent coverage analysis (114/114 tests passing)
- Processing order comparison (Node.js vs .NET)
- Lifecycle & decay coverage
- AI & NPC coverage
- Known divergences (intentional optimizations)
- Test coverage summary
- Deferred features roadmap
- Production readiness assessment

**Status:** ✅ 114/114 parity tests passing (100%)
**Coverage:** 95% core gameplay, 85% overall (including deferred features)

### [parity-divergences.md](parity-divergences.md)
**Detailed Divergence Analysis** - Explanation of intentional optimizations and Node.js bug fixes.

**Contents:**
- ActionLog optimization (reduces DB writes ~50%)
- Validation efficiency improvements
- Combat efficiency optimizations
- Node.js type coercion bugs (withdraw/upgrade)

**Referenced By:**
- [`docs/engine/roadmap.md`](../../../docs/engine/roadmap.md) - Engine milestones
- [`docs/engine/e10.md`](../../../docs/engine/e10.md) - E10 details
- [`src/ScreepsDotNet.Engine/CLAUDE.md`](../../../src/ScreepsDotNet.Engine/CLAUDE.md) - Engine coding patterns

## Quick Links

- **Roadmap:** [`docs/engine/roadmap.md`](../../../docs/engine/roadmap.md) - E1-E10 milestones (all complete ✅)
- **E10 Details:** [`docs/engine/e10.md`](../../../docs/engine/e10.md) - Full parity test coverage
- **Parity Tests:** [`src/ScreepsDotNet.Engine.Tests/Parity/Tests/ParityTests.cs`](../../../src/ScreepsDotNet.Engine.Tests/Parity/Tests/ParityTests.cs)

## Production Readiness

✅ **Ready For:**
- Core gameplay testing
- Private server hosting
- Bot development
- Combat testing
- Economy testing (labs, factories, market)

❌ **Not Ready For:**
- MMO room management features (deferred to E11+)
- Observer automation (deferred to E8.1)
- Seasonal content (deposits, power banks, strongholds - deferred to E9.1)

## Last Updated

January 24, 2026 - E10 Complete (114/114 tests passing, 100% core gameplay)
