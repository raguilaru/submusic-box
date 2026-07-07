# BoxSubMusic

Native Xbox Series X/S music streaming app for Subsonic/OpenSubsonic servers.
UWP + XAML + C#, controller-first 10-foot UX, background playback.

## How this repo works

Development is **requirement-driven** and built incrementally with an AI coding
agent. The requirements docs are the source of truth — read them before changing
anything:

- [Final requirements](docs/requirements/10-final-requirements.md) — normative product/platform contract
- [Acceptance criteria](docs/requirements/50-acceptance-criteria.md) — observable pass/fail checks per feature
- [Implementation slices](docs/requirements/60-implementation-slices.md) — ordered, isolated work increments
- [Project structure](docs/architecture/20-project-structure.md) — solution layout and dependency rules

`CLAUDE.md` at the repo root is the agent contract and imports the requirement docs.

## Development environment

- Code lives in this repo, edited from macOS or Windows.
- Builds, packaging, and deployment require **Visual Studio on Windows**
  (Parallels VM) with the UWP workload, plus an Xbox in Developer Mode for
  on-device validation.
- Machine-local paths and commands go in `CLAUDE.local.md` (gitignored), not here.

## Status

Slice 00 (repository bootstrap) complete. Next: Slice 01 — controller focus
navigation spike. Build/test/package commands are TODO until the Visual Studio
solution exists.
