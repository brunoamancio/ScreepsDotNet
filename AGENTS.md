# AGENT GUIDE

This file is the quick hand-off for anyone jumping into the repository. Detailed references now live under `docs/`. Check `docs/README.md` for the full documentation/AGENT ownership map before editing anything.

## Repo orientation

- `ScreepsNodeJs/` – legacy server kept for parity checks (separate git repo; don’t touch unless asked).
- `src/` – .NET solution (backend HTTP host, CLI, storage adapters, driver, native pathfinder). See `docs/backend.md` for architecture details and `docs/getting-started.md` for setup steps.
- `.editorconfig`, `.globalconfig`, `Directory.Build.props`, `ScreepsDotNet.slnx.DotSettings` – shared tooling; never delete these.

## Where to read next

- Environment + workflow: `docs/getting-started.md`, `docs/backend.md`.
- CLI specifics: `docs/cli.md`.
- HTTP route coverage & `.http` samples: `docs/http-endpoints.md`.
- Driver plan/status: `docs/driver.md` (plus `src/ScreepsDotNet.Driver/AGENT.md` for day-to-day tasks).
- Native pathfinder build/release: `src/native/pathfinder/AGENT.md`.
- Engine rewrite roadmap/log: `src/ScreepsDotNet.Engine/AGENT.md`.

## Current focus

1. Finish shard-aware write APIs + intent/bot tooling parity (tracking in `docs/backend.md` and `docs/specs/MarketWorldEndpoints.md`).
2. Continue the driver milestones (D6–D10) per `docs/driver.md`.
3. Keep CLI/HTTP feature docs in sync whenever routes/commands change.

## Expectations for contributors

- Keep new documentation in `docs/` and reference it from AGENT files instead of duplicating text.
- Before opening a PR, run through the update checklist in `docs/README.md` so documentation/AGENT files stay in sync with your changes.
- When editing a subsystem, update both the relevant doc and AGENT (if it has actionable TODOs). Use `docs/README.md` to confirm ownership.
- Run `git status` from `ScreepsDotNet/` (not repo root) to avoid touching the embedded `ScreepsNodeJs` git data.
- Stop any running `dotnet run` before building so DLLs aren’t locked.
- Use Docker-backed Mongo/Redis (see `docs/getting-started.md`) and keep Testcontainers tests passing.
- Respect the shared style rules (implicit usings, expression-bodied members, collection expressions, `Lock` type for locks). Driver-specific conventions are reiterated in `src/ScreepsDotNet.Driver/AGENT.md`.

## Quick tips

- Need to reset seeds? Follow the commands in `docs/backend.md` §Reset Workflow.
- `.http` files live beside the HTTP project; keep them updated whenever endpoints change (details in `docs/http-endpoints.md`).
- The native pathfinder binaries are downloaded automatically during `dotnet build`; if you touch that flow, sync changes with `src/native/pathfinder/AGENT.md` and `docs/driver.md`.

If something feels undocumented, add it to the appropriate `docs/*.md` file and drop a short pointer here so the next agent can find it. Keep this file lean—its job is to point to the real docs, not to duplicate them.
