# .NET Modernization Plan: legacy .NET Native → modern .NET + Native AOT

Status: **PLANNING ONLY — no migration work started.** Written 2026-07-17 in
response to the Visual Studio deprecation notice for .NET Native with UWP
projects, which links to
[Modernize your UWP app with .NET and Native AOT](https://learn.microsoft.com/en-us/windows/uwp/dotnet-native/modernize-uwp-apps-with-dotnet).

This document is the single source of truth for if/when/how BoxSubMusic moves
off the legacy .NET Native toolchain. It supplements (does not replace)
`20-project-structure.md`, whose 2026-07-07 toolchain decision remains in
force until the gate in §4 is passed.

---

## 1. What is actually being deprecated, and what the modern stack is

**Deprecated:** .NET Native — the legacy AOT compiler/runtime for C# UWP apps
(what `BoxSubMusic.App` builds with today via `UseDotNetNativeToolchain`,
`Microsoft.NETCore.UniversalWindowsPlatform` 6.2.14, and the old-style
csproj). Per Microsoft: it "will continue to receive security and reliability
fixes but will not receive new feature updates." **No end-of-support date has
been announced** — this is indefinite maintenance mode, not a shutdown
([release/support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-native)).
The legacy templates still ship in VS 2026, marked ".NET Native".

**NOT deprecated:** UWP itself, UWP XAML (`Windows.UI.Xaml`), the appx/MSIX
app model, or UWP on Xbox. The deprecation is the *compiler/runtime layer
underneath* the app, not the app model or the UI framework.

**The modern replacement** (GA, and the default C# UWP project type in
VS 2026): the same UWP app model and the same UWP XAML, but running on the
latest .NET (net10.0 today) with **Native AOT** for Release, via an SDK-style
csproj:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>
    <UseUwp>true</UseUwp>                     <!-- Windows.UI.Xaml projections via CsWinRT -->
    <EnableMsixTooling>true</EnableMsixTooling> <!-- single-project MSIX packaging -->
    <PublishAot>true</PublishAot>
    <DisableRuntimeMarshalling>true</DisableRuntimeMarshalling>
  </PropertyGroup>
</Project>
```

What it buys: modern C#/BCL, current NuGet ecosystem, dramatically faster
builds, XAML/C# Hot Reload, Debug/Release behavioral parity (the .NET Native
"works in Debug, dies in Release" class of bug disappears — AOT issues
surface as *build-time* analyzer warnings instead of runtime crashes), and an
actively developed runtime for years to come.

Microsoft also frames this as step 1 of an optional two-step path
(step 2 = WinUI 3 / Win32). **Step 2 is not available to us** — see §3.

## 2. The hard constraint: Xbox

Everything hinges on one fact:

> **Modern .NET UWP is not officially supported on Xbox, and it did not work
> on our console when we tried it.**

The evidence, in order of authority for this project:

1. **Our own result (2026-07-07, recorded in `20-project-structure.md` and
   CLAUDE.local.md):** the app was originally built on the VS 2026 default
   modern stack (`UseUwp` + `PublishAot`, net10.0). It ran fine in the VM,
   registered on the console, and **failed activation with `0x80070002`** —
   no crash dump, dies before managed code runs, never appears in Device
   Portal. A modern *Debug* (CoreCLR, JIT) build fails with `0x8004090a`
   (Xbox forbids JIT). That failure is why the project pivoted to legacy
   .NET Native, which is console-verified through five checkpoints since.
2. **Microsoft's stance** ([microsoft-ui-xaml discussion #9983](https://github.com/microsoft/microsoft-ui-xaml/discussions/9983)):
   a collaborator states Xbox is "not officially supported at the moment
   (though it will work fine on Xbox if you try it)". Our result contradicts
   the parenthetical, at least for a Series X in Dev Mode as of July 2026.
   Multiple developers in that thread (including one building a background
   music player for Xbox — our exact scenario) asked for official support;
   **no timeline was given.**
3. **The [modernization guide](https://learn.microsoft.com/en-us/windows/uwp/dotnet-native/modernize-uwp-apps-with-dotnet)
   (updated 2026-04) does not mention Xbox at all** — neither as supported
   nor unsupported.

Deployment-path caveat on item 1: our failed attempt deployed a **signed
MSIX via WinAppDeployCmd**. We never tried VS **Remote Machine F5** (loose
layout registration — today's proven .NET Native deploy path) or a Device
Portal install with the modern build. `0x80070002` is FILE_NOT_FOUND, which
could plausibly be a missing dependency/framework package in the package
we produced rather than a hard platform block. This is exactly what the
Phase 1 spike (§5) is designed to answer cheaply — but until it passes,
assume Xbox is blocked.

## 3. Options considered

| Option | Verdict | Why |
| --- | --- | --- |
| **A. Stay on legacy .NET Native** | **Default until the gate passes** | Console-verified, maintenance-supported indefinitely, no removal date. Costs: slow builds, no Hot Reload, netstandard2.0 ceiling for libs, Debug/Release divergence risk, and a stack that will never get another feature. |
| **B. Modern .NET UWP (UseUwp + Native AOT)** | **Target — gated on Xbox activation** | Keeps the UWP app model and 100% of our XAML/focus/controller work. This is the only Microsoft-recommended forward path that can even theoretically reach Xbox. |
| C. WinUI 3 / Windows App SDK | **Not viable** | The Windows App SDK targets desktop only — it does not run on Xbox. Microsoft's "step 2" is for desktop apps. If it ever comes to Xbox, migrating from B is the designed on-ramp; migrating from A is not. |
| D. Uno Platform / MAUI / other frameworks | Not viable / out of scope | No real Xbox story (their Windows heads are WinUI 3/desktop); wholesale UI rewrite for zero Xbox benefit. |
| E. Web/PWA/WebView shell | Prohibited | Requirements §2 explicitly bans WebView/HTML UI. |

**Recommendation: Option B, executed in phases, gated on a cheap Xbox
activation spike, with Option A as the fully supported fallback for as long
as needed.** There is no urgency: .NET Native has no sunset date, the app is
sideloaded in Dev Mode (no Store certification clock ticking), and v1 is
mid-flight. The wrong move is a mid-project toolchain swap onto a stack our
own console has already rejected once.

## 4. The gate

Migration (Phase 2+) starts **only** when BOTH are true:

1. **A modern .NET UWP app activates and runs on our Xbox Series X** (the
   Phase 1 spike passes), AND
2. **v1 is functionally complete** (through Slice 14), so we migrate a
   finished, console-verified baseline instead of a moving target.

Condition 2 is a strong default, not an absolute — if Microsoft announces
official Xbox support tomorrow and the spike passes, we may choose to migrate
after Slice 13 instead. Condition 1 is absolute.

**Watch list** (check when closing a session, or roughly monthly):

- [microsoft-ui-xaml discussion #9983](https://github.com/microsoft/microsoft-ui-xaml/discussions/9983) — Xbox support announcements
- The [modernization guide](https://learn.microsoft.com/en-us/windows/uwp/dotnet-native/modernize-uwp-apps-with-dotnet) — appearance of Xbox/device-family language
- VS 2026 / Windows SDK / [CsWinRT](https://github.com/microsoft/CsWinRT/releases) release notes
- Re-run the Phase 1 spike after any major VS/SDK update (it is ~an hour once set up)

**Action item (Rafael):** comment on discussion #9983 describing our use case
and the concrete `0x80070002` repro (Series X, Dev Mode, VS 2026 default
template, signed MSIX via WinAppDeployCmd). Microsoft explicitly asked for
Xbox use cases there; a background-audio music player with a reproducible
activation failure is exactly the signal they requested. Claude can draft
this comment. This is our only lever to influence the timeline.

## 5. Phased plan

### Phase 0 — now (costs nothing, blocks nothing)

- Finish v1 on .NET Native exactly as planned (Slices 13–14 + console
  checkpoints). No migration work on `main`.
- Keep this document current; monitor the watch list.
- Rafael posts the GitHub comment (Claude drafts it).
- Keep the codebase migration-clean (§6 shows it already is): no new
  `{Binding}` (x:Bind only — already the house style), no reflection-based
  serialization (already banned), no new rd.xml directives, libraries stay
  free of app/platform dependencies.

### Phase 1 — feasibility spike (cheap, repeatable, anytime)

Goal: answer "can ANY modern .NET UWP app activate on our console?" without
touching BoxSubMusic code.

1. In the VM, create a **blank** VS 2026 modern UWP template app (the
   "Blank UWP App" template, net10.0, `UseUwp` + `PublishAot`). Keep it
   outside this repo (it's throwaway; e.g. `%USERPROFILE%\spikes\`).
2. Verify it runs on the VM (arm64 Local Machine F5) — expected to pass.
3. Deploy matrix to the console (x64 Release/AOT), in this order:
   - **VS Remote Machine F5** — the path our failed attempt never tried,
     and today's proven .NET Native deploy path.
   - **Device Portal install** of the generated MSIX.
   - **WinAppDeployCmd** install (the path that failed in July) — for parity.
4. If activation fails: capture Device Portal state, event viewer output if
   reachable, and the exact error per path. Update §2 with findings; file or
   update the GitHub comment with the repro. **Stop — gate stays closed.**
5. If the blank app runs: add a second spike page exercising our risk APIs —
   `MediaPlayer` + `MediaPlaybackList` + SMTC background audio,
   `PasswordVault`, `ApplicationData`, gamepad focus — and console-verify.
   Then the gate's condition 1 is met.

Rerun step 3 after major toolchain updates until it passes. The spike is
Rafael-driven at the console/VS end; Claude prepares checklists and any
command-line pieces.

### Phase 2 — parallel app project (gated; `main` stays shippable)

Do NOT convert `BoxSubMusic.App.csproj` in place first. Instead:

1. Branch `modern-dotnet` off `main`.
2. Add a **second app project**, `src/BoxSubMusic.App.Modern/`, SDK-style
   (per §1's shape), that includes the existing App sources and XAML **by
   link/glob** from `src/BoxSubMusic.App/` — same pages, same controls, same
   assets. UWP XAML is unchanged between toolchains, so the sources should
   compile as-is; divergences (if any) get `#if` or small shims, each one
   documented here.
   - Create the project from the VS template in the VM (repo rule: never
     hand-write UWP project scaffolding from macOS), then Claude edits the
     csproj on the Mac side to add the source links.
3. Retarget libraries: `BoxSubMusic.Core` and `BoxSubMusic.SubsonicClient`
   from `netstandard2.0` → multi-target `netstandard2.0;net10.0` (so BOTH
   app projects build during the transition), and add
   `<IsAotCompatible>true</IsAotCompatible>` to surface AOT analyzer
   warnings. Tests already run net10.0 and should be unaffected.
4. Fix what the AOT analyzers and the XAML compiler surface. Expected to be
   small (§6), but budget a session for CsWinRT projection differences
   (event marshalling in `PlaybackService`, media-thread callbacks).
5. Verify ladder: `dotnet test` (123 tests) → VM arm64 F5 smoke of the
   Modern app → **full console regression checkpoint** — the entire
   accumulated backlog discipline applies: focus/controller navigation,
   background audio + SMTC, suspend/resume + resume card, theme, memory
   behavior under the Xbox background cap, and a Release/AOT soak.
6. Keep the branch rebased on `main` while v1 slices continue; the linked
   sources mean new pages flow into the Modern project automatically.

### Phase 3 — cutover (gated on the Phase 2 console checkpoint passing)

1. Replace `BoxSubMusic.App.csproj` with the modern project (rename
   `App.Modern` → `App`; delete the legacy csproj, `Default.rd.xml`,
   `Properties/AssemblyInfo.cs` if superseded, and the
   `Microsoft.NETCore.UniversalWindowsPlatform` reference).
2. Simplify libraries back to a single `net10.0` target (or keep
   netstandard2.0 if there's any reason to — decide then).
3. Update the build loop docs and machine notes:
   - CLAUDE.md commands, `20-project-structure.md` (toolchain section
     rewritten to point here), STATUS.md, CLAUDE.local.md.
   - **arm64-native MSBuild** requirement (already learned pre-pivot: the
     x64-emulated MSBuild breaks ILCompiler pack resolution in the VM).
   - `Directory.Build.props` bin/obj redirect: verify it still applies to
     the SDK-style project (property name and timing may differ).
   - Re-test whether command-line packaging works on the modern pipeline —
     APPX0703 was a legacy-toolchain failure; single-project MSIX may
     finally give us a scripted package/deploy path (would be a big
     quality-of-life win; VS F5 remains the fallback).
4. Revisit relaxed constraints — deliberately, not by drift:
   - The reflection-serializer ban can soften to "source-generated
     System.Text.Json permitted" — but **recommendation: keep XDocument**;
     it's AOT-safe, tested, and the server speaks XML anyway. Record the
     decision in requirements §16 either way.
   - netstandard2.0 NuGet ceiling is gone; current packages become usable.
5. Merge `modern-dotnet` → `main` as the post-v1 "modernization" milestone.
   Tag the last .NET Native commit (e.g. `v1-dotnet-native`) as the rollback
   point. Rollback = check out the tag's csproj; the legacy toolchain
   remains installed and supported.

## 6. Codebase readiness audit (2026-07-17) — surprisingly clean

The reflection-free discipline adopted for .NET Native turns out to be
almost exactly the Native AOT compatibility contract. Audit results:

| Area | Finding | Migration impact |
| --- | --- | --- |
| App csproj | Old-style, .NET Native | **The only legacy artifact in the repo.** Replaced in Phase 2/3. |
| XAML data binding | **Zero `{Binding}`; 100% `x:Bind`** (all 12 binding-using files audited) | None. x:Bind is compile-time generated — no `[GeneratedBindableCustomProperty]` work needed. |
| `Default.rd.xml` | Stock template, zero custom directives | Delete it; nothing to port. |
| Serialization | XDocument + explicit mapping everywhere (server XML, recent servers, resume state) | None. Fully AOT-safe by construction. |
| Core / SubsonicClient | SDK-style, netstandard2.0, **zero package dependencies**, no platform APIs | Retarget/multi-target + `IsAotCompatible`; expect near-zero warnings. |
| Tests | Already net10.0 (xunit) | None. |
| NuGet in App | Only `Microsoft.NETCore.UniversalWindowsPlatform` (the legacy runtime itself) | Removed by the toolchain swap; no third-party AOT risk exists. |
| WinRT API surface | `MediaPlayer`/`MediaPlaybackList`/SMTC, `PasswordVault`, `ApplicationData`, `UISettings`, `InputPane`, `FocusManager`, XAML controls | Same APIs under CsWinRT projections. Risk is behavioral nuance (thread marshalling of WinRT events — `PositionChanged` on the media thread, `ColorValuesChanged` off-thread), not availability. Phase 1 step 5 + Phase 2 step 5 verify. |
| INotifyPropertyChanged display models | Plain interface implementations | None (only `{Binding}` would have needed annotations). |

## 7. What changes for us in practice (implications summary)

**UI: essentially nothing.** Same `Windows.UI.Xaml`, same OS NavigationView,
same focus engine, same ThemeResource system. Every console-verified
behavior (XY focus fixes, ContextRequested redirect, focus trap, OSK flow,
edge-to-edge scaling calls) carries over as-is. This is the decisive
argument for Option B over any UI-framework change: ~20 sessions of
hard-won, console-verified XAML/controller work is preserved untouched.

**Functionality: nothing by design.** All Windows.* APIs remain; only the
runtime underneath changes. The §16 decisions (XDocument, PasswordVault,
LocalSettings/LocalFolder split, MediaPlaybackList mirror) all survive.

**Developer loop: much better on desktop, one caveat on console.**
- Builds significantly faster; XAML/C# Hot Reload in the VM; SDK-style
  csproj ends the "VS must regenerate project files" era (Claude can edit
  the csproj directly).
- Debug/Release parity: AOT problems become build-time warnings, retiring
  the "Release|x64 .NET Native run" as a special checkpoint item.
- **Console caveat:** Xbox forbids JIT, so console deploys are always
  AOT-compiled — the console F5 loop pays AOT compile time (VM Debug F5
  keeps CoreCLR speed). Net: desktop iteration much faster, console
  iteration similar or slightly slower per deploy.

**Risks, honestly:**
1. Xbox activation (§2) — the gate. Highest severity, existence-level.
2. Xbox support may stay unofficial even if the spike passes — meaning a
   future console OS update could break us with no support recourse.
   Mitigation: the app is sideloaded/personal; the .NET Native branch tag
   remains a working fallback; official support may follow (demand is
   visible in #9983).
3. CsWinRT behavioral differences in the media stack (threading/lifetime of
   WinRT event handlers) — medium; covered by the Phase 2 console
   regression.
4. Toolchain churn (CsWinRT/.NET version coupling, e.g. CsWinRT lagging new
   .NET majors) — low; pin versions, upgrade deliberately.

## 8. Logistics: repo, branches, and division of labor

**Same repo, no new project/solution elsewhere.** The migration reuses ~all
sources; history and requirements docs must stay attached. A separate repo
would orphan both.

**Branching:** nothing for Phase 0; Phase 1's blank spike lives outside the
repo entirely; Phases 2–3 on a long-lived `modern-dotnet` branch, regularly
rebased, merged only after the console checkpoint. `main` stays
.NET Native and shippable throughout.

**Division of labor:**

| Claude (Mac side) | Rafael (VM/console side) |
| --- | --- |
| This plan; keeping it current | Reviewing/approving each phase |
| Drafting the GitHub #9983 comment | Posting it |
| Spike checklists; interpreting failures | Creating template projects in VS; F5 deploys; console observations |
| csproj edits post-template, library retargeting, AOT warning fixes, shims | VS workload installs (UWP tools are already in), solution reloads |
| Test runs over SSH; command-line build/packaging experiments | Device Portal checks; anything interactive in VS |
| Docs/CLAUDE.md/STATUS updates at each phase boundary | Sign-off on the Phase 2 console checkpoint |

## 9. Bottom line

1. **No panic:** .NET Native is in indefinite maintenance mode with no
   end date. The deprecation notice changes nothing for v1.
2. **Finish v1 on the current stack.** It is the only stack our console has
   ever run this app on.
3. **The modern stack is the right destination** — same UI, same APIs,
   better everything else — and this codebase is unusually well prepared
   for it (one legacy csproj, zero `{Binding}`, zero reflection, zero
   third-party packages in the app).
4. **Xbox is the gate.** Run the cheap Phase 1 spike (including the
   never-tried VS Remote Machine F5 path), push on #9983, watch releases,
   and re-spike after toolchain updates. Migrate only after a blank modern
   app provably runs on the console.
