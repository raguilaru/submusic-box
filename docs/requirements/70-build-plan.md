# Build Plan

Execution plan for BoxSubMusic, built on the slice order in
[60-implementation-slices.md](60-implementation-slices.md). This file adds
sequencing, ownership, checkpoints, and the on-device testing protocol.

## Working protocol

- **Checkpoint rule:** before every significant step (each numbered step below),
  Claude states what is about to happen and waits for Rafael's go-ahead.
  No slice is started, no deploy is attempted, and nothing is committed to git
  without an explicit go.
- **Ownership tags:** `[CLAUDE]` = done by the agent from the Mac (code, SSH
  builds, docs). `[RAFAEL]` = requires the human (VS GUI, console, router,
  physical controller). `[JOINT]` = Claude drives, Rafael acts/verifies.
- Every slice ends with the acceptance-criteria check from
  [50-acceptance-criteria.md](50-acceptance-criteria.md) before it is called done.
- Pre-slice questions (resume scope, songs row action, playlist editing, search
  paging) are asked at the marked points, never guessed.

## Confirmed environment facts (2026-07-07)

- Mac→VM SSH build loop verified (see CLAUDE.local.md for commands/paths).
- Partner Center dev account exists; Xbox Series X **not yet in Dev Mode**.
- A Subsonic-compatible server is available on the LAN (details go in
  CLAUDE.local.md when Slice 03 starts — never committed). It runs **Navidrome**;
  the compatibility target is any OpenSubsonic server via capability detection.
  Both LAN and internet-hosted servers must work (manifest declares
  `internetClient` + `privateNetworkClientServer`).
- An Xbox controller can be attached to the VM for early focus-navigation testing.

---

## Phase 0 — Prerequisites (parallel with Phase 1)

- **P0.1 [RAFAEL]** DHCP reservations on the router: VM (10.0.0.29) and,
  once known, the Xbox's IP.
- **P0.2 [RAFAEL, guided]** Xbox Dev Mode activation — needed before step 1.5.
  Walkthrough in "On-device testing protocol" below.
- **P0.3 [JOINT]** Record test-server URL + account in CLAUDE.local.md
  (gitignored); Claude verifies reachability with a ping request. Needed
  before step 2.1.

## Phase 1 — Foundation and spikes (Slices 00–02)

- **1.1 [RAFAEL, guided]** Bootstrap the solution in Visual Studio (VM, GUI):
  Blank App (UWP, C#) `BoxSubMusic.App`, .NET Standard 2.0 libraries
  `BoxSubMusic.Core` and `BoxSubMusic.SubsonicClient`, xUnit test projects,
  under `src/`/`tests/` per docs/architecture/20-project-structure.md.
  Claude provides exact click-by-click settings (target SDK, min version,
  save path via X: drive). Commit after it builds in VS.
- **1.2 [CLAUDE]** First remote build: run MSBuild over SSH against the UNC
  path, x64 Debug. Resolve any shared-folder/ARM64-host issues (fallback:
  VM-local clone). Lock the verified build command into CLAUDE.md/CLAUDE.local.md.
- **1.3 [CLAUDE→RAFAEL]** Slice 01 — controller focus spike. Claude writes the
  minimal shell with focusable nav; Rafael runs it in the VM with the Xbox
  controller attached and confirms D-pad/stick navigation and focus visibility.
- **1.4 [CLAUDE→RAFAEL]** Slice 02 — background playback spike. MediaPlayer +
  SMTC + `backgroundMediaPlayback` manifest capability, playing a bundled test
  tone/file; Rafael verifies audio continues when the app is minimized in the VM.
- **1.5 [JOINT]** **On-device checkpoint #1** — activate Dev Mode (P0.2),
  pair, deploy the spike app to the Xbox, verify: app launches, controller
  navigation works on the real console, audio keeps playing when switching
  to Dev Home. This validates the entire pipeline (ARM64-VM x64 cross-build
  included) before real feature work.

## Phase 2 — Connected core (Slices 03–05)

- **2.1 [CLAUDE]** `BoxSubMusic.SubsonicClient`: auth (token+salt), ping,
  capability detection (OpenSubsonic extensions), getAlbumList2, getCoverArt
  (artwork URL construction — Home carousels need it), typed models, explicit
  legacy fallbacks. Transport is XML parsed with XDocument (decision
  2026-07-09; see requirements §16 and the architecture doc) — no
  reflection-based serializers anywhere. Unit tests run via `dotnet test` over
  SSH; live verification against the real server (P0.3) via integration tests
  gated on a gitignored local config file (skipped when absent).
- **2.2 [CLAUDE]** Slice 03 — setup flow: URL sanitization, masked password,
  inline errors, Credential Locker storage, recent-servers list. Includes
  manifest update: add `privateNetworkClientServer` (LAN servers are
  unreachable with `internetClient` alone) and wire App→Core→SubsonicClient
  project references.
- **2.3 [CLAUDE]** Slice 04 — navigation shell: collapsible nav, fixed order,
  bottom account area, theme-aware icons. **Ask first:** which open-source
  icon set (requirements §13.3).
- **2.4 [CLAUDE]** Slice 05 — Home: resume card + three album carousels.
  Resume scope is decided (2026-07-09, §6.1): queue + index + position +
  shuffle/repeat + context display metadata, local-only, resumes paused. Note:
  nothing populates resume state until real playback exists (Slice 06+), so
  this slice ships the persistence contract, the card, and its empty state,
  verified with seeded fake data.
- **2.5 [JOINT]** **On-device checkpoint #2** — full connect-and-browse-home
  flow on the console against the real server (text entry with controller,
  10-foot readability, carousel focus behavior). Include one **Release|x64**
  F5 run to validate the .NET Native x64 build end-to-end (the daily x64
  Debug loop does not exercise .NET Native; the VM's ARM64 loop does).

## Phase 3 — Library browsing (Slices 06–09)

- **3.1 [CLAUDE]** Slice 06 — Artists browse + artist detail (favorite,
  play/shuffle queue-replace, oldest-album-first ordering).
- **3.2 [CLAUDE]** Slice 07 — Albums browse + album detail (ordered track list,
  track jump, current-track highlight).
- **3.3 [CLAUDE]** Slice 08 — Songs list. **Ask first:** row primary action.
- **3.4 [CLAUDE]** Slice 09 — Playlists + client-side composite artwork.
  **Ask first:** playlist editing scope for v1.
- **3.5 [JOINT]** **On-device checkpoint #3** — browse the real library at
  10 feet; grid performance and artwork loading under real data volumes.

## Phase 4 — Playback experience (Slices 10–11)

- **4.1 [CLAUDE]** Slice 10 — persistent now-playing bar (full metadata,
  transport controls, queue-expand affordance).
- **4.2 [CLAUDE]** Slice 11 — expanded two-pane queue, slide animation,
  jump-to-track, Y-button mapping (or documented fallback).
- **4.3 [JOINT]** **On-device checkpoint #4** — the core promise: start music,
  launch a game, verify background playback + SMTC from the Xbox guide;
  queue interactions from the couch.

## Phase 5 — Settings, search, hardening (Slices 12–14)

- **5.1 [CLAUDE]** Slice 12 — settings: theme (Automatic/Dark/Light, persisted),
  nav visibility toggles.
- **5.2 [CLAUDE]** Slice 13 — search. **Ask first:** endpoints/scopes and
  paging/virtualization policy.
- **5.3 [CLAUDE]** Slice 14 — resilience: loading/empty/retry/unsupported
  states, theming + focus audits, memory-pressure behavior.
- **5.4 [JOINT]** **Final on-device validation** — long soak session: hours of
  playback, server-unreachable recovery, background memory pressure while
  gaming, both themes.

---

## On-device testing protocol (Xbox Series X)

Used at every `[JOINT]` checkpoint. One-time setup first (step 1.5 / P0.2):

### One-time: Dev Mode activation
1. On the console (retail mode), install **Xbox Dev Mode** from the Microsoft
   Store and launch it.
2. Sign in / enter the activation code at the shown URL; the console gets tied
   to the existing Partner Center dev account.
3. Activate and restart into Dev Mode via **Manage devices → Switch and restart**.
4. In Dev Home: note the console's IP (then add the DHCP reservation, P0.1),
   and under **Remote Access Settings** enable **Xbox Device Portal**
   (require authentication; set a username/password — record them in
   CLAUDE.local.md only).
5. Switching back to retail: Dev Home → "Leave Dev Mode". Dev and retail
   environments are separate; game saves are untouched.

### Per-checkpoint deploy loop
1. Build the app package in the VM (MSBuild over SSH; x64).
2. Deploy from the VM: `WinAppDeployCmd install -file <appx/msix> -ip <xbox-ip>`
   (first time pairs with a PIN shown in Dev Home), or via Visual Studio
   remote-machine deploy when the on-console debugger is useful.
3. Launch from Dev Home, run the checkpoint's verification list, note failures
   as findings against the slice's acceptance criteria.
4. Keep the console in Dev Mode during active development; return to retail
   between sessions as desired.

## Risk register

- **x64 cross-build from ARM64 VM** — validated at step 1.2/1.5 before feature work.
- **MSBuild over Parallels UNC share** — fallback is a VM-local clone (documented
  in CLAUDE.local.md).
- **UWP background-audio memory limits on Xbox** — spike at 1.4, soak at 5.4.
- **Modern .NET/UWP tooling drift** — if the current VS 18 UWP template chain
  fights the ARM64 host, pin to the known-good template configuration before
  writing app code.
