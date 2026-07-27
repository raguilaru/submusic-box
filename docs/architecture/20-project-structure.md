# Project Structure

Decided 2026-07-07: solution split into app + libraries (see requirements Section 2 and 16).

## Toolchain decision (2026-07-07): legacy .NET Native, NOT modern .NET + AOT

The app was first built on the VS 2026 default modern stack (`UseUwp` +
`PublishAot`, net10.0). It works in the VM but **does not activate on Xbox**:
the signed AOT package registers on the console yet fails in the loader with
`0x80070002` (no crash dump — fails before managed code runs), and never
appears in the Device Portal games list. Modern AOT UWP has no documented Xbox
support (Desktop only). We therefore use the **legacy .NET Native** UWP
toolchain for `BoxSubMusic.App`, which is the proven path for C# UWP on Xbox.

Implications:
- `BoxSubMusic.App` = legacy .NET Native UWP project (old-style csproj).
- Libraries target **netstandard2.0** (legacy .NET Native's reference ceiling).
- Xbox deploy = VS Remote Machine F5 (supported by .NET Native).
- The Mac→VM→console plumbing (SSH, signing, Device Portal, path mapping) is
  unchanged and reused.

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
- Local persistence (decided 2026-07-09): ApplicationData.LocalSettings for
  simple values; structured data (recent servers, resume state) as files in
  LocalFolder, serialized with the same reflection-free approach as the API
  client (see below)

### BoxSubMusic.Core (.NET Standard class library)
- Domain models (Album, Artist, Song, Playlist, Queue, ServerCapabilities)
- View-model logic and playback/queue orchestration contracts (interfaces)
- Browsing rules and display wording shared by more than one page —
  `Browsing/SongSorter` (the §7.3 orderings and their labels),
  `Browsing/DurationText` (track and total duration wording),
  `Browsing/ArtistTotals` (per-artist sums folded from album pages, §7.1).
  Anything two surfaces must agree on — or any rule worth pinning with tests
  rather than eyeballing in a page — belongs here rather than in a view.
- No UWP or Windows-specific APIs — must be unit-testable on any host

### BoxSubMusic.SubsonicClient (.NET Standard class library)
- Typed client for OpenSubsonic with legacy Subsonic fallback
- Capability detection on connect
- Typed response models; no ad hoc JSON/dynamic parsing
- Transport format (decided 2026-07-09): the Subsonic API's native **XML**
  responses, parsed with **XDocument into typed models via explicit mapping
  code**. Reflection-based serializers (System.Text.Json, Newtonsoft.Json) are
  banned app-wide: they are a runtime risk under legacy .NET Native, and the
  daily x64 Debug console loop would not catch failures (only Release|x64 and
  the VM's ARM64 builds compile with .NET Native).
- Accepts an injected HttpMessageHandler/HttpClient so tests can fake transport
  and the app can adjust platform behavior without the client knowing
- No UI or platform dependencies — must be unit-testable on any host

## Dependency rule

App → Core → SubsonicClient. Never the reverse. Platform-specific behavior
(secure storage, media playback) lives behind Core interfaces implemented in App.
