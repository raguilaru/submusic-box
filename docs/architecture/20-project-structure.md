# Project Structure

Decided 2026-07-07: solution split into app + libraries (see requirements Section 2 and 16).

## Planned solution layout

```
xbox-subsonic-app/
├── CLAUDE.md                     # agent contract (always loaded)
├── CLAUDE.local.md               # machine-local notes/overrides (gitignored)
├── docs/
│   ├── requirements/
│   │   ├── 10-final-requirements.md
│   │   ├── 50-acceptance-criteria.md
│   │   └── 60-implementation-slices.md
│   └── architecture/
│       └── 20-project-structure.md
├── src/
│   ├── BoxSubMusic.sln           # created from Visual Studio (Parallels/Windows)
│   ├── BoxSubMusic.App/          # UWP app: XAML views, app lifecycle, platform services
│   ├── BoxSubMusic.Core/         # models, view models, queue/playback orchestration
│   └── BoxSubMusic.SubsonicClient/  # typed OpenSubsonic/Subsonic API client
└── tests/
    ├── BoxSubMusic.Core.Tests/
    └── BoxSubMusic.SubsonicClient.Tests/
```

The solution and project files must be created from Visual Studio on the Windows
side (Parallels) — do not hand-write .csproj/.sln for UWP from macOS. Once created,
they are committed here like any other source.

## Project responsibilities

### BoxSubMusic.App (UWP, XAML + C#)
- All UI: pages, controls, styles, themes, focus/controller handling
- Platform services: Credential Locker access, MediaPlayer/SMTC, ApplicationData
- Composition root: wires Core view models to platform implementations

### BoxSubMusic.Core (.NET Standard class library)
- Domain models (Album, Artist, Song, Playlist, Queue, ServerCapabilities)
- View-model logic and playback/queue orchestration contracts (interfaces)
- No UWP or Windows-specific APIs — must be unit-testable on any host

### BoxSubMusic.SubsonicClient (.NET Standard class library)
- Typed client for OpenSubsonic with legacy Subsonic fallback
- Capability detection on connect
- Typed response models; no ad hoc JSON/dynamic parsing
- No UI or platform dependencies — must be unit-testable on any host

## Dependency rule

App → Core → SubsonicClient. Never the reverse. Platform-specific behavior
(secure storage, media playback) lives behind Core interfaces implemented in App.
