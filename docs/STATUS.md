# Current Status

Last updated: 2026-07-09 (Phase 2 kickoff).

## Where we are

**Phase 1 (foundation + spikes) is DONE, including on-device checkpoint #1.**
The app builds, deploys, launches, navigates with the D-pad, and plays audio
in the background (tone → Xbox button → Dev Home → audio continues, SMTC in
the guide — verified by Rafael on the console 2026-07-09).

**Phase 2 decisions are locked (2026-07-09):** Subsonic transport is XML via
XDocument explicit mapping (no reflection serializers — .NET Native risk);
persistence is LocalSettings + LocalFolder files; resume scope is queue +
index + position + shuffle/repeat + context display metadata, local-only,
resumes paused; manifest gets `privateNetworkClientServer`; primary test
server is Navidrome. Details in requirements §16 and the build plan Phase 2.

Milestones reached:
- Repo + requirement-driven docs (Slice 00).
- Mac → Parallels Windows VM build loop over SSH (see CLAUDE.local.md).
- Solution: `BoxSubMusic.App` (UWP) + `BoxSubMusic.Core` + `BoxSubMusic.SubsonicClient`
  + xUnit test projects.
- Slice 01 (controller focus nav) and Slice 02 (background playback: MediaPlayer
  + SMTC + `backgroundMediaPlayback`) built and verified in the VM.
- **Major pivot:** modern .NET + Native AOT UWP builds but will NOT activate on
  Xbox (registers, then loader error `0x80070002`, no crash; undocumented for
  Xbox). Switched the app to the **legacy .NET Native** toolchain; libraries
  retargeted to **netstandard2.0**. See docs/architecture/20-project-structure.md
  and the [[xbox-uwp-toolchain]] memory.
- The app launches on the console via VS Remote Machine F5 (x64, Universal
  Unencrypted, 10.0.0.28).
- **D-pad fixed (2026-07-09, verified on console):** the "left stick moves a
  mouse cursor, D-pad dead" behavior was Xbox **mouse mode** — the platform
  default for UWP apps on console (never engages on desktop, which is why the
  VM behaved differently). Fixed by setting
  `RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested` in the
  `App` constructor. D-pad focus navigation confirmed working at 10 ft.
- **Local VM test loop restored (2026-07-09):** the .NET Native recreation had
  lost the bin/obj redirect, so Local Machine F5 in the VM failed with DEP0700
  / 0x80073D1F (UWP registration requires a local NTFS volume, not the
  Parallels share). Recreated as `Directory.Build.props` (App project only) +
  `$(LocalBinRoot)`-based OutputPath entries in the csproj. Local run in the
  VM: platform **arm64**, target **Local Machine**, F5. Details and gotchas in
  CLAUDE.local.md.

## Open items (start here next session)

1. ~~Test-server details~~ **P0.3 DONE (2026-07-09):** Navidrome server
   recorded in CLAUDE.local.md; reachability verified from Mac AND VM (LAN +
   external, token auth). One caveat noted there re `.local` resolution on Xbox.
2. **Implement build-plan step 2.1**: SubsonicClient — token+salt auth with
   legacy fallback, ping, capability detection, getAlbumList2, getCoverArt,
   typed XML models via XDocument, unit tests + gated integration tests.
3. Then 2.2 (Slice 03 setup flow, incl. `privateNetworkClientServer` manifest
   capability and App→Core→SubsonicClient references), 2.3 (shell — ask the
   icon-set question first), 2.4 (Home), 2.5 (on-device checkpoint #2, incl.
   a Release|x64 .NET Native validation run).

Resolved 2026-07-09: on-device checkpoint #1 closed; `src/BoxSubMusic.App` is
no longer root-owned on the Mac side (chown done — Mac-side edits work
normally now); resume-scope, persistence, and transport decisions locked.

## How to deploy (quick reference)

- **Console:** VS in the VM → Solution Platform **x64** → BoxSubMusic.App
  startup → run target **Remote Machine** `10.0.0.28`, auth **Universal
  (Unencrypted)** → Xbox on Dev Home → **F5**.
- **Local (VM):** Solution Platform **arm64** → run target **Local Machine** →
  F5. Requires the Directory.Build.props output redirect (in repo). Note: VS
  only picks up Directory.Build.props changes on solution (re)load — restart
  VS after touching it, or builds land on the share and F5 fails with DEP0700.
- Full detail + the abandoned AOT path in CLAUDE.local.md.

## Reused, still-valid infrastructure

Mac↔VM SSH (`ssh winvm`), path mapping (`\\Mac\Repositories\...` over SSH),
Device Portal (https://10.0.0.28:11443), arm64-native MSBuild for command-line
smoke builds (compiles fine; appx packaging always fails with APPX0703 — that
step is VS-only). The Parallels ownership quirk (Windows-created files are
root on the Mac side) is why ported files are staged then copied in — see
CLAUDE.local.md.
