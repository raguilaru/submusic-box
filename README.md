# SubMusic Box

Native Xbox Series X/S music streaming app for Subsonic/OpenSubsonic servers.
UWP + XAML + C#, controller-first 10-foot UX, background playback.

> **Display name:** "SubMusic Box". The solution, projects, and namespaces keep
> the original **BoxSubMusic** code name (the rename is display-layer only).

## How this repo works

Development is **requirement-driven** and built incrementally with an AI coding
agent. The requirements docs are the source of truth — read them before changing
anything:

- [Final requirements](docs/requirements/10-final-requirements.md) — normative product/platform contract
- [Acceptance criteria](docs/requirements/50-acceptance-criteria.md) — observable pass/fail checks per feature
- [Implementation slices](docs/requirements/60-implementation-slices.md) — ordered, isolated work increments
- [Project structure](docs/architecture/20-project-structure.md) — solution layout and dependency rules
- [Playback architecture](docs/architecture/30-playback-architecture.md) — queue/now-playing design
- [Current status](docs/STATUS.md) — running log of what is implemented and verified

`CLAUDE.md` at the repo root is the agent contract and imports the requirement docs.

## Solution layout

- `src/BoxSubMusic.App` — UWP app (XAML views, controls, platform services)
- `src/BoxSubMusic.Core` — models, queue/playback logic, setup/persistence helpers (unit-testable, no UWP host)
- `src/BoxSubMusic.SubsonicClient` — typed OpenSubsonic/Subsonic API client (XDocument transport, no reflection serializers)
- `tests/` — xUnit tests for Core and SubsonicClient

## Development environment

- Code lives in this repo, edited from **macOS**.
- Builds, packaging, and deployment require **Visual Studio on Windows**
  (Parallels VM) with the UWP workload, plus an Xbox in Developer Mode for
  on-device validation.
- Machine-local paths and commands go in `CLAUDE.local.md` (gitignored), not here.

## Status

Planned v1 scope (slices 00–14) is **complete and console-verified**, followed
by refinement work (Search, retro visual theme, lyrics display, and UI/UX
polish). See [docs/STATUS.md](docs/STATUS.md) for the detailed state and the
on-device test backlog.
