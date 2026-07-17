# Implementation Slices

## How to use this file

Each slice should be implemented in isolation.
The coding agent must:
1. quote the slice ID and linked requirements
2. propose a short plan
3. modify only relevant files
4. validate against acceptance criteria
5. stop at the slice boundary

Do not combine multiple slices unless explicitly requested.

---

## Slice 00 - Repository bootstrap

### Goal
Create the base project structure and requirement-driven development harness.

### Requirements
- CLAUDE.md present at repo root
- requirements docs committed in-repo
- basic folder structure created
- placeholder solution/project structure documented

### Deliverables
- root documentation files
- initial folder structure
- TODO placeholders for build/test/package commands

### Out of scope
- real UI implementation
- API calls
- playback

### Acceptance focus
- docs exist
- agent can reference them
- repo conventions are clear

---

## Slice 01 - Technical spike: controller focus navigation

### Goal
Prove that controller-first navigation behaves correctly in native XAML on Xbox-targeted UI.

### Requirements linked
- Platform and Architecture
- Navigation Shell
- Cross-Cutting Input Handling

### Deliverables
- minimal shell prototype
- clearly focusable nav items
- visible focus state
- directional navigation verified
- optional input-glyph placeholder behavior

### Out of scope
- server connection
- real media playback
- final styling polish

### Acceptance focus
- D-pad/left stick navigation works consistently
- focus order is predictable
- focus is visible from 10-foot distance

---

## Slice 02 - Technical spike: background playback foundation

### Goal
Prove MediaPlayer + SMTC + background behavior before heavy UI investment.

### Requirements linked
- Background Playback
- Now-Playing Bar and Expanded Queue

### Deliverables
- playback service skeleton
- SMTC integration
- background lifecycle handling skeleton
- memory-pressure handling hooks/events
- manifest capability configuration notes

### Out of scope
- polished now-playing UI
- real queue management UX
- complete metadata experience

### Acceptance focus
- audio can continue under supported background conditions
- SMTC updates
- no immediate lifecycle failure

---

## Slice 03 - Setup flow

### Goal
Implement first-launch connection flow.

### Requirements linked
- First-Launch Setup
- Server API Compatibility

### Deliverables
- setup page
- URL sanitization
- basic validation
- connect action
- inline error states
- Credential Locker (PasswordVault) storage for password/token
- recent-server persistence placeholder or initial implementation

### Out of scope
- full home experience
- browse screens
- playback

### Acceptance focus
- sanitized URL handling
- masked password
- secrets stored in Credential Locker, not plain settings
- clear error display
- successful login path reaches shell

---

## Slice 04 - Application shell

### Goal
Implement the persistent collapsible left navigation and content-host structure.

### Requirements linked
- Navigation Shell
- Cross-Cutting Theming
- Cross-Cutting Iconography

### Deliverables
- main shell layout
- collapsible nav
- fixed nav order
- bottom-area settings/account placement
- theme-aware icon treatment
- hide/show toggles plumbing placeholder

### Out of scope
- detailed browse content
- playback bar behavior
- expanded search UX

### Acceptance focus
- shell layout stable
- controller navigation works
- Search/Home cannot disappear

---

## Slice 05 - Home view

### Goal
Implement Home with resume card and album carousels.

### Requirements linked
- Home View
- Server API Compatibility

### Open design note
Persistence scope resolved 2026-07-09 (requirements §6.1 / decisions log).
Split decided 2026-07-10: **Slice 05a** delivers the three carousels with
loading/empty/error states now; the resume card renders only when persisted
playback context exists — impossible until the playback slices produce that
state. **Slice 05b** (the real resume card) follows Slices 10/11.
**05b implemented 2026-07-16** (§16 / design doc Slice 05b section): Core
`ResumeState` + `PlayQueue.Restore`, app `ResumeStore`
(LocalFolder XML structure + LocalSettings index/position scalars),
`PlaybackService.Resume` with one-shot in-track seek, Home card whose Play
restores and plays immediately. Console-verified at checkpoint #6
(2026-07-17).

### Deliverables
- resume section (05a: stub that renders nothing without persisted context; 05b: real card)
- Most Played carousel
- Recently Added carousel
- Random Picks carousel
- loading/empty/error states

### Out of scope
- artist/album detail views
- full queue behavior

### Acceptance focus
- correct endpoint mapping
- album-only content
- non-looping horizontal carousels

---

## Slice 06 - Artists browse + artist detail

### Goal
Implement artists discovery flow.

### Requirements linked
- Browse Views / Artists
- Artist Detail View
- Cross-Cutting View-Mode Toggle

### Open design note
Split decided 2026-07-10, same rationale as Slice 05: Play/Shuffle depend on
the queue + now-playing-bar infrastructure that Slice 10 introduces. **Slice
06a** delivers Artists browse (grid/list + view toggle) and Artist detail
*display* (artwork, name, favorite heart via star/unstar, album + song counts,
lower album grid), with Play/Shuffle shown disabled + a "playback arrives
later" note. **Slice 06b** wires Play/Shuffle (queue replacement, oldest-album-
first, now-playing-bar reveal) after Slice 10. §8's contract is unchanged.

### Deliverables
- artists grid/list browse (06a)
- view toggle (06a)
- artist detail layout (06a)
- favorite action (06a — star/unstar is live)
- album grid on detail screen (06a)
- play/shuffle queue replacement behavior (06b)

### Out of scope
- album detail
- playlists
- expanded queue UI polish

### Acceptance focus
- favorite persistence
- oldest-album-first play ordering
- now-playing bar appears on playback start

---

## Slice 07 - Albums browse + album detail

### Goal
Implement albums discovery flow and album playback behavior.

### Requirements linked
- Browse Views / Albums
- Album Detail View

### Open design note
Split decided 2026-07-13, same rationale as Slices 05/06: playback depends on
the Slice 10 queue + now-playing infrastructure. **Slice 07a** delivers Albums
browse (grid/list + the shared §13.5 `ViewModeToggle`, incremental A→Z paging)
and Album detail *display* (artwork, title, artist, year, favorite heart via
star/unstar, song count, total duration, disc/track-ordered track list), with
Play/Shuffle disabled + note and inert track rows. **Slice 07b** wires
Play/Shuffle queue replacement, current-track highlighting, and jump-to-track
after Slice 10. §9's contract is unchanged.

### Deliverables
- albums grid/list browse (07a)
- visible view toggle (07a — shared control)
- album detail view (07a)
- ordered track list (07a)
- favorite action (07a — star/unstar is live)
- current-track highlighting (07b)
- direct track jump behavior (07b)
- play/shuffle queue replacement (07b)

### Out of scope
- playlist composites
- search system

### Acceptance focus
- detail layout mirrors artist view
- play/shuffle replace queue
- setlist order preserved

---

## Slice 08 - Songs browse

### Goal
Implement song-library list experience.

### Requirements linked
- Browse Views / Songs

### Open design note
Resolved 2026-07-13 (§16/§17): the row primary action (A/Enter) is immediate
playback — queue replace, play from that song — wired by Slice 10; a
Menu-button context menu carries secondary actions. Data source is search3
with an empty query (OpenSubsonic full-library contract), paged incrementally;
non-OpenSubsonic servers get an explicit unsupported state. Implemented
2026-07-13 with rows inert + visible note until Slice 10; "Go to album" in the
context menu is live.

### Deliverables
- songs list
- required columns
- row interaction behavior (display + context menu now; playback with Slice 10)
- loading/empty/error/unsupported states

### Out of scope
- advanced search
- playlist editing

### Acceptance focus
- list-only
- required metadata visible
- row selection can initiate playback or open context as designed

---

## Slice 09 - Playlists browse + composite artwork

### Goal
Implement playlist browsing and client-side playlist artwork composition.

### Requirements linked
- Browse Views / Playlists

### Open design note
Resolve playlist editing scope for v1 (read-only vs create/edit/delete) before implementing.
Resolved 2026-07-13: read-only for v1 (§16). Browse + composite artwork
shipped with this slice; tiles stayed inert until the queue existed.
**Detail/playback view implemented 2026-07-17** (§16): `PlaylistDetailPage`
mirrors album detail — composite header artwork, Play/Shuffle queue
replacement, jump-to-track + current-track highlight, starred markers, and
the standard row context menu (Go to album AND Go to artist). Tiles now
open it. No playlist favorite heart (star/unstar has no playlist form).
Console-verified at checkpoint #6 (2026-07-17).

### Deliverables
- playlists grid/list browse
- visible view toggle
- composite artwork renderer
- single-album playlist fallback rule
- loading/empty/error states

### Out of scope
- playlist editing unless later required
- advanced queue editing UX

### Acceptance focus
- client-generated artwork works
- no fake server dependency
- view modes consistent with app standards

---

## Slice 10 - Now-playing bar

### Goal
Implement persistent compact playback UI.

### Requirements linked
- Now-Playing Bar and Expanded Queue
- Background Playback

### Deliverables
- bottom bar
- playback controls
- format/bitrate metadata
- elapsed/total time display
- queue expand control
- theme-aware/focus-aware styling

### Out of scope
- full two-pane expanded queue
- advanced animations beyond needed transition

### Acceptance focus
- bar persists during playback
- controls work
- metadata updates correctly

---

## Slice 11 - Expanded queue

### Goal
Implement the expanded playback and queue inspection experience.

### Requirements linked
- Now-Playing Bar and Expanded Queue

### Open design note
Resolved 2026-07-16 (§16 / design doc Slice 11 section): full-screen overlay
over the whole shell; whole queue shown in current play order; queue rows
jump-only (context menu deferred to refinement); Y is a global toggle, B and
Escape close, chevron button for mouse. Implemented 2026-07-16.

### Deliverables
- slide expand/collapse
- rich left pane
- queue list right pane
- jump-to-track interaction
- Y-button mapping or documented fallback

### Out of scope
- settings
- search refinement

### Acceptance focus
- two-pane layout works
- direct track jumping works
- queue replace behavior remains consistent

---

## Slice 12 - Settings

### Goal
Implement theming and nav-visibility preferences.

### Requirements linked
- Settings
- Cross-Cutting Theming

### Open design note
Implemented 2026-07-16 (§16): the nav-visibility half had existed since
Slice 04; this slice added the theme selector — RadioButtons on Settings,
instant apply, root-frame `RequestedTheme`, Automatic resolved from
`UISettings` with `ColorValuesChanged` live-follow. Console-verified at
checkpoint #6 (2026-07-17) — the live-follow event fires on Xbox.

### Deliverables
- Automatic/Dark/Light theme selector
- persistence of theme selection
- nav visibility toggles for Artists/Albums/Playlists
- protections preventing Search/Home from being hidden

### Out of scope
- account management beyond current needs
- server switching UX beyond existing flow

### Acceptance focus
- theme persists
- system theme respected in Automatic
- nav visibility behaves correctly

---

## Slice 13 - Search

### Goal
Implement search entry, active-state filters, and results UX.

### Requirements linked
- Navigation Shell
- Search behavior implied by requirements doc

### Deliverables
- search entry point
- segmented filters in content pane
- result rendering by scope
- loading/empty/error states

### Open design note
Search API shape and pagination behavior need final clarification.
Resolved 2026-07-19 (§16): search3, submit-triggered, "All" view (top
6/6/10) + Artists/Albums/Songs scope filters with incremental paging via
search3 offsets; song results play with the Songs-browse queue semantics;
pre-1.8 servers get an explicit unsupported state. Implemented 2026-07-19
(`SearchPage`, `SubsonicApiClient.SearchAsync`).

### Acceptance focus
- filters are in content area, not left nav
- search remains always accessible
- results are scoped clearly

---

## Slice 14 - Resilience and polish

### Goal
Harden the app for real-device use.

### Requirements linked
- Cross-Cutting Requirements
- Background Playback
- Home / Browse / Detail / Queue acceptance items

### Deliverables
- loading states
- empty states
- retry states
- unsupported-capability states
- theming audit
- focus audit
- performance/memory audit
- documentation updates

### Acceptance focus
- no silent failures
- graceful unsupported-server behavior
- acceptable device-level UX quality

---

## Reusable task prompt

Use this prompt for each slice:

Implement Slice XX only.

Before coding:
- Restate the linked requirement sections.
- List the exact files you expect to change.
- Identify any ambiguity before implementation.

During implementation:
- Keep changes limited to this slice.
- Do not invent server APIs or platform capabilities.
- Preserve controller-first navigation and 10-foot readability.

Before finishing:
- Check the relevant acceptance criteria.
- List known gaps, TODOs, or deferred items.
- Summarize what was implemented and what was intentionally left out.
