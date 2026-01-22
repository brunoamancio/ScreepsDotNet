# Backend Parity Harness

**Status:** 📋 Not Yet Implemented

## Purpose

Test behavioral parity between .NET Backend (HTTP API + CLI) and legacy Node.js backend for:
- HTTP endpoint responses
- CLI command outputs
- Error handling and status codes
- Query parameter handling
- Request validation

## Scope

**In Scope:**
- HTTP API responses (JSON structure, field values)
- HTTP status codes (200, 400, 404, 500)
- CLI command outputs (stdout, stderr, exit codes)
- Query parameter parsing
- Error messages

**Out of Scope:**
- Game simulation logic (covered by Engine parity)
- Database operations (covered by Driver parity)
- Constants/formulas (covered by Common parity)

## Estimated Effort

- **Time:** 8-12 hours
- **Priority:** 🟢 LOW (after Engine and Driver parity)
- **Fixtures:** 30-40 test cases

## Fixture Categories

1. **HTTP Endpoints (20 fixtures)**
   - `/api/auth` endpoints (login, register)
   - `/api/user` endpoints (profile, code, memory)
   - `/api/game` endpoints (rooms, objects, time)
   - `/api/leaderboard` endpoints (rankings, stats)
   - Error responses (400, 404, 500)

2. **CLI Commands (10 fixtures)**
   - `storage` commands (list-users, reset-db)
   - `engine` commands (tick, process-room)
   - `diagnostics` commands (health, metrics)
   - Error handling (invalid args, missing deps)

3. **Query Parameters (5 fixtures)**
   - Pagination (limit, offset)
   - Filtering (by user, room, time)
   - Sorting (asc, desc)
   - Optional parameters

4. **Edge Cases (5 fixtures)**
   - Missing required parameters
   - Invalid data types
   - Large payloads
   - Concurrent requests

## Dependencies

**Must Be Complete First:**
- ✅ Backend HTTP/CLI implementation complete
- ✅ E7: Engine parity validation complete

**Infrastructure Required:**
- MongoDB 7 (state storage)
- Redis 7 (caching)
- Node.js 10.13.0+ (legacy backend)
- .NET 9+ (ScreepsDotNet.Backend)

## Implementation Strategy

Different approach from Engine/Driver (no shared fixtures):

1. **HTTP Testing:**
   - Use existing `.http` files in `src/ScreepsDotNet.Backend.Http/`
   - Execute requests against both backends
   - Compare JSON responses field-by-field
   - Compare status codes, headers

2. **CLI Testing:**
   - Define CLI test cases (command, args, expected output)
   - Execute against both CLIs
   - Compare stdout/stderr
   - Compare exit codes

3. **Comparison:**
   - Use JSON diff for HTTP responses
   - Use text diff for CLI outputs
   - Report divergences with context

## Related Documentation

- **Backend Docs:** `docs/backend/overview.md`
- **HTTP Endpoints:** `docs/backend/http-api.md`
- **CLI Commands:** `docs/backend/cli.md`
- **Engine Parity:** `../engine/README.md` (E7)

---

**Created:** 2026-01-22
**Part of:** Multi-Layer Parity Testing Strategy
