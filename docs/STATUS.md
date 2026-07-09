# Current Status

Last updated: 2026-07-09 (end of session).

## Where we are

**Phase 1 (foundation + spikes) is DONE.** The app builds, deploys, launches,
and now **navigates correctly with the D-pad on the real Xbox Series X**.

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

1. **Quick re-check of Slice 02 on device** (unless already done informally):
   Play → tone on TV, Xbox button → Dev Home → audio continues, SMTC shows in
   the guide. D-pad nav is verified; this closes on-device checkpoint #1.
2. Proceed to **Phase 2** (Slices 03–05): SubsonicClient against Rafael's real
   LAN server, setup flow with Credential Locker, nav shell, Home. Ask the
   resume-persistence-scope question before Slice 05.
3. Housekeeping: `src/BoxSubMusic.App` is still root-owned on the Mac side
   (created from Windows during the pivot). Mac-side edits need the
   stage-then-copy workaround until Rafael runs
   `sudo chown -R rafael:staff src/BoxSubMusic.App`.

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
