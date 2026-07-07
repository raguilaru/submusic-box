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
Resolve the "Pick up where you left off" persistence scope (queue only vs queue + position vs queue + position + originating context) before implementing.

### Deliverables
- resume section
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

### Deliverables
- artists grid/list browse
- view toggle
- artist detail layout
- favorite action
- play/shuffle queue replacement behavior
- album grid on detail screen

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

### Deliverables
- albums grid/list browse
- visible view toggle
- album detail view
- ordered track list
- current-track highlighting
- direct track jump behavior

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
Resolve the Songs row primary action (immediate playback on A/Enter vs secondary action/context menu) before implementing.

### Deliverables
- songs list
- required columns
- row interaction behavior
- loading/empty/error states

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
