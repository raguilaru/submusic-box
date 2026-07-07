# Current Status

Last updated: 2026-07-07 (end of session).

## Where we are

**Phase 1 (foundation + spikes) is essentially DONE.** The app now builds,
deploys, and **launches on the real Xbox Series X**.

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
- On the .NET Native build, the app **launches successfully on the console** via
  VS Remote Machine F5 (x64, Universal Unencrypted, 10.0.0.28).

## Open items (start here tomorrow)

1. **D-pad / controller navigation needs adjustment on the real console.**
   Rafael observed issues with D-pad control when running on Xbox (behaviour
   differs from the VM). This is the immediate next task — refine focus
   navigation on hardware, then re-verify the full Slice 01 checklist at 10 ft.
   (Details of the exact glitches to be gathered at the start of the session.)
2. Re-run the Slice 02 on-device checks once nav is fixed: Play → tone on TV,
   Xbox button → Dev Home → audio continues, SMTC shows in the guide.
3. After checkpoint #1 fully passes, proceed to **Phase 2** (Slices 03–05:
   SubsonicClient against Rafael's real LAN server, setup flow with Credential
   Locker, nav shell, Home). Ask the resume-persistence-scope question before
   Slice 05.

## How to deploy (quick reference)

VS in the VM → Solution Platform **x64** → BoxSubMusic.App startup → run target
**Remote Machine** `10.0.0.28`, auth **Universal (Unencrypted)** → Xbox on Dev
Home → **F5**. Full detail + the abandoned AOT path in CLAUDE.local.md.

## Reused, still-valid infrastructure

Mac↔VM SSH (`ssh winvm`), path mapping (`\\Mac\Repositories\...` over SSH),
Device Portal (https://10.0.0.28:11443), arm64-native MSBuild for command-line
smoke builds. The Parallels ownership quirk (Windows-created files are root on
the Mac side) is why ported files are staged then copied in — see CLAUDE.local.md.
