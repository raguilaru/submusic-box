# Final Requirements

## 1. Product Goal

Build BoxSubMusic, a native Xbox Series X/S music app that connects to Subsonic-compatible servers, is optimized for controller-first 10-foot use, and supports background playback with a polished now-playing and queue experience.

## 2. Platform and Architecture

- The app MUST be a native Xbox Series X/S app built with UWP using XAML + C#.
- The app MUST NOT use WebView or HTML wrapper UI for core screens.
- The implementation SHOULD use Microsoft's Xbox media and background playback samples as architectural references, but the final UI must remain XAML-native.
- The UI MUST be designed for 10-foot viewing and Xbox controller focus navigation.
- The app SHOULD disable automatic 200% XAML scaling if needed to maintain precise 1920x1080 layout control.
- The app MUST respect safe-area/overscan behavior either by using platform-safe margins or by explicitly opting out and implementing its own safe-area padding.
- Focus rectangles and focused states MUST remain clearly visible from couch distance.
- The visual theme MUST avoid TV-unfriendly overly saturated color choices.
- The solution MUST be split into an app project plus libraries (see docs/architecture/20-project-structure.md):
  - `BoxSubMusic.App` — UWP app (XAML views, view models wiring, platform services)
  - `BoxSubMusic.Core` — models, view-model logic, queue/playback orchestration contracts
  - `BoxSubMusic.SubsonicClient` — typed OpenSubsonic/Subsonic API client
- Core and SubsonicClient MUST remain unit-testable without a UWP host.
- The app manifest MUST declare both `internetClient` and `privateNetworkClientServer` capabilities so that internet-hosted and LAN servers both work.

## 3. Server API Compatibility

- The primary API target MUST be OpenSubsonic.
- The client MUST support legacy Subsonic-compatible servers with explicit fallback behavior.
- On connect, the client MUST query server capabilities and use capability detection rather than relying only on hardcoded version checks.
- If a capability is unavailable, the dependent feature MUST degrade explicitly by hiding the unsupported UI, disabling the action with explanation, or falling back to the nearest legacy endpoint.
- Home carousels for Most Played, Recently Added, and Random Picks MUST use album-based data, not mixed playlist/media content, unless a future server-specific extension is intentionally added and documented.

## 4. First-Launch Setup

- On first launch, the app MUST present:
  - Server URL
  - Username
  - Password
- Password entry MUST be masked.
- URL input MUST be sanitized before connection:
  - trim whitespace
  - default to https:// when no scheme is provided
  - strip trailing slashes
  - validate basic URL structure
- Authentication and connection failures MUST surface as clear inline errors.
- The setup flow MUST NOT use QR pairing.
- The app SHOULD maintain a locally cached recent-servers list to reduce retyping.
- The app SHOULD support paste/external keyboard entry when available on the platform.
- Credentials MUST be stored securely:
  - server URL and username in local app settings
  - password/token in the Windows Credential Locker (PasswordVault)
  - secrets MUST NOT be written to plain local settings or log output

## 5. Navigation Shell

- The app MUST have a persistent collapsible left navigation menu.
- Default top-to-bottom order:
  1. Search
  2. Home
  3. Artists
  4. Albums
  5. Songs
  6. Playlists
  7. Settings
  8. Account / server info at the bottom area
- Search filters MUST appear in the main content area once search is active, not in the left nav.
- Search and Home MUST always remain visible and MUST NOT be hideable.
- Artists, Albums, and Playlists MUST each be independently hideable from Settings.

## 6. Home View

### 6.1 Pick up where you left off
- The app MUST locally persist enough state to resume playback across app restarts (decided 2026-07-09):
  - the full play queue (track references)
  - the current track index
  - the in-track playback position
  - shuffle and repeat state
  - lightweight originating-context metadata (display name + artwork reference) for the resume card
- Resume MUST restore the queue at the saved track and position; the app MUST NOT auto-play on launch. (Clarified 2026-07-16, §16: launch itself restores nothing — the saved state renders only as the Home card, and pressing the card's Play restores the queue and starts playing immediately. "Restore paused" earlier in this contract meant "never audible without a user action", not "Play lands in a paused state".)
- The Home view MUST show artwork, name, and a Play action that resumes the saved queue/context.
- Server-side play-queue sync (savePlayQueue/getPlayQueue) is out of scope for v1, but the local persistence contract MUST NOT preclude adding it later.

### 6.2 Most Played
- Data source: getAlbumList2 with type=frequent
- Max items: 20
- Presentation: non-looping horizontal carousel
- Minimum visible items target: 5, subject to responsive layout constraints

### 6.3 Recently Added
- Data source: getAlbumList2 with type=newest
- Max items: 20
- Presentation: non-looping horizontal carousel
- Minimum visible items target: 5, subject to responsive layout constraints

### 6.4 Random Picks
- Data source: getAlbumList2 with type=random
- Presentation: non-looping horizontal carousel
- Content scope: albums only

## 7. Browse Views

### 7.1 Artists
- MUST support grid view by default.
- MUST support list view as an alternate mode.
- Grid entries SHOULD show artist artwork when available.

### 7.2 Albums
- MUST support grid view by default.
- MUST support list view as an alternate mode.
- Grid entries SHOULD show album artwork.

### 7.3 Songs
- MUST be list-only.
- Song list MUST include:
  - title
  - artist
  - album
  - length
  - format
  - bitrate

### 7.4 Playlists
- MUST support grid view by default.
- MUST support list view as an alternate mode.
- Playlist artwork MUST be generated client-side as:
  - a composite of up to 4 distinct album covers when multiple albums exist
  - a single cover when all playlist tracks share one album
- This artwork composition is a client responsibility and MUST NOT depend on a nonexistent Subsonic API endpoint.

## 8. Artist Detail View

- MUST show large artist artwork.
- MUST show artist name.
- MUST show a toggleable favorite heart.
- Favorite state MUST be persisted through Subsonic star/unstar behavior.
- MUST show total album count and song count.
- MUST provide Play and Shuffle actions.
- Play MUST queue all songs for the artist in oldest-album-first order and replace the current queue.
- Shuffle MUST queue the same song pool in randomized order and replace the current queue.
- Starting playback from this screen MUST surface the now-playing bar immediately.
- The lower section MUST show an album grid matching the Albums browse view style.

## 9. Album Detail View

- MUST mirror the Artist detail layout as closely as practical.
- MUST show:
  - large album artwork
  - album title
  - artist name
  - year
  - favorite heart
  - song count
  - total duration
- Play and Shuffle MUST replace the current queue with this album's tracks only.
- The lower section MUST show the album track list in original disc/track order.
- The currently playing track MUST be visually highlighted when applicable.
- Selecting a track row MUST jump playback directly to that track.

## 10. Now-Playing Bar and Expanded Queue

- A persistent floating now-playing bar MUST appear at the bottom of the main content area during playback.
- The compact bar MUST show:
  - artwork thumbnail
  - song title
  - artist
  - album
  - format
  - bitrate
  - elapsed time
  - total time
  - controls for shuffle, previous, play/pause, next, repeat, and queue expand
- The expanded/collapsed state MUST animate with a slide transition.
- The expand action SHOULD be mapped to the Y button as the secondary detail/more action.
- The expanded state MUST support a two-pane queue layout:
  - playback controls + metadata on the left
  - full up-next queue on the right
- Users MUST be able to jump directly to any queued track from the expanded queue.
- Any new Play action elsewhere in the app MUST replace the current queue instead of appending.
- Song context menus MAY additionally offer explicit queue edits — "Play next" (insert directly after the current track) and "Add to queue" (append) — added 2026-07-17 (§16). These are deliberate user edits, not Play actions, and do not weaken the replace-not-append rule above.
- The same pair applies to whole containers — albums, artists (the §8 oldest-album-first pool), and playlists — from card context menus wherever those are displayed and as secondary buttons on the corresponding detail pages (added 2026-07-18, §16). The block is inserted/appended in its internal order.

## 11. Background Playback

- Background playback MUST use MediaPlayer + SystemMediaTransportControls.
- The manifest MUST declare backgroundMediaPlayback capability if required by the chosen implementation pattern.
- Playback MUST continue when the app is backgrounded within supported Xbox/UWP behavior.
- The app MUST respond gracefully to Xbox background memory-limit pressure.
- The app MUST avoid termination due to ignored background-memory constraints where possible.

## 12. Settings

- Theme options MUST include:
  - Automatic
  - Dark
  - Light
- Theme selection MUST persist locally.
- Automatic MUST follow the Xbox system theme.
- Settings MUST include independent visibility toggles for:
  - Artists
  - Albums
  - Playlists
- Search and Home MUST NOT be hideable.

## 13. Cross-Cutting Requirements

### 13.1 Input handling
- Primary input MUST be Xbox controller.
- The app MUST also support keyboard/mouse fallback when connected.
- Input-hint glyphs SHOULD swap dynamically based on last-used input device.
- Controller glyphs and keyboard/mouse hints MUST stay consistent with the current active input mode.

### 13.2 Theming
- Every screen MUST support both light and dark themes.
- Contrast MUST remain suitable for 10-foot TV viewing.

### 13.3 Iconography
- The app MUST use one consistent modern open-source icon set.
- Icons MUST work in light and dark themes without mixing inconsistent styles.

### 13.4 Artwork fallback
- If artwork is missing, the app MUST prefer server-provided metadata/artwork first.
- Any external artwork fallback remains a risk item until store-policy and background/programmatic-call compliance is verified.
- The client MUST NOT call Last.fm directly.
- Last.fm-derived metadata may be used only when exposed by the user's own server implementation.

### 13.5 View-mode toggle
- Wherever grid/list dual modes exist, the app MUST provide one consistent and always-visible toggle control.

## 14. AI-Assisted Build Harness

- Development MUST proceed screen-by-screen and subsystem-by-subsystem, not as a single giant generation request.
- Recommended slice order:
  1. setup flow
  2. navigation shell
  3. home
  4. artist detail
  5. album detail
  6. playback bar and queue
  7. settings
- Background audio and controller focus navigation SHOULD be treated as early technical spikes.
- Real Xbox Developer Mode validation SHOULD happen throughout development, not only at the end.
- Requirements documents and API contracts MUST be committed in-repo and referenced by the coding agent before implementation.

## 15. Non-Goals for v1

- No QR phone-pairing setup.
- No WebView-first UI.
- No direct third-party artwork/metadata calls that require an app-owned external API integration unless explicitly approved later.
- No silent degradation when a server capability is missing.
- No queue append-by-default behavior for Play actions.

## 16. Decisions Log

- 2026-07-07: App display name is **BoxSubMusic**. Repo name is xbox-subsonic-app.
- 2026-07-07: Solution is split into app + Core library + Subsonic API client library (Section 2).
- 2026-07-07: Credentials are stored via Windows Credential Locker / PasswordVault (Section 4).
- 2026-07-07: No AGENTS.md / .claude/rules for now; CLAUDE.md is the single agent contract.
- 2026-07-09: Subsonic transport is the API's native XML, parsed with XDocument into typed models via explicit mapping code. No reflection-based serializers (System.Text.Json/Newtonsoft) anywhere in the app — they are a runtime risk under legacy .NET Native.
- 2026-07-09: Local persistence split: ApplicationData.LocalSettings for simple values (theme, nav toggles, server URL/username); structured data (recent servers, resume state) as files in LocalFolder using the same reflection-free XML serialization.
- 2026-07-09: Resume scope locked (Section 6.1): full queue + current index + in-track position + shuffle/repeat + context display metadata, persisted locally; resumes paused; server play-queue sync deferred beyond v1.
- 2026-07-09: Manifest declares `internetClient` + `privateNetworkClientServer` (Section 2) — both internet and LAN servers are supported.
- 2026-07-09: Primary test server is Navidrome; compatibility target is any OpenSubsonic server via capability detection, never server-specific branching.
- 2026-07-10: Icon set (Section 13.3) is **Fluent UI System Icons** (MIT), shipped as the Regular icon font (`Assets/Fonts/FluentSystemIcons-Regular.ttf` + LICENSE.txt). Glyph codepoints come only from the set's official JSON map. The built-in NavigationView settings item (Segoe MDL2 gear) is disabled to avoid mixing icon sets; Settings/Account are custom pane-footer items.
- 2026-07-10: Recent-servers list (Section 4) stores URL + username only — never passwords; capped at 5 entries, newest first, deduped by server+user. Selecting a recent entry pre-fills URL/username; the password is retyped.
- 2026-07-10: Slice 05 (Home) is split. **05a** ships the three album carousels (Sections 6.2–6.4) with loading/empty/error states; the resume card (Section 6.1) renders only when persisted playback context exists — which cannot happen yet, since queue persistence is produced by the playback slices. **05b** (the real resume card) lands after Slices 10/11 introduce that persistence. Section 6.1's contract is unchanged.
- 2026-07-10: Slice 06 (Artists) is split for the same reason as Slice 05. **06a** ships Artists browse (Section 7.1: grid default + list alternate + always-visible view toggle §13.5) and Artist detail *display* (Section 8: large artwork, name, favorite heart persisted via star/unstar, album + song counts, lower album grid). **06b** adds the Section 8 Play/Shuffle actions — queue replacement and the now-playing-bar reveal — after Slice 10 introduces the queue/now-playing infrastructure. In 06a the Play/Shuffle buttons are shown disabled with an explicit "playback arrives later" note (§15: no silent degradation). Section 8's contract is unchanged. Favorite (star/unstar) is fully live in 06a because it needs no playback infra.

- 2026-07-13: Setup-flow controller UX (Sections 4/13.1): Enter advances to the next field (Server address → Username → Password) and submits only from the last one, so the console OSK's accept — including the system Menu-button = Enter accelerator — walks the form without B or D-pad. On Xbox the keyboard is re-shown on the next field via InputPane.TryShow. Verified on console 2026-07-13.
- 2026-07-13: Slice 07 (Albums) is split like 05/06. **07a** ships Albums browse (Section 7.2: grid default + list alternate + the §13.5 toggle, now one shared `ViewModeToggle` control) and Album detail *display* (Section 9: artwork, title, artist, year, favorite heart via star/unstar, song count, total duration, disc/track-ordered track list), with Play/Shuffle disabled + note and track rows inert. **07b** adds Play/Shuffle queue replacement, current-track highlighting, and jump-to-track after Slice 10. Album cards everywhere (Home carousels, artist detail, Albums browse) open album detail from 07a on.
- 2026-07-13: Songs row primary action (Section 7.3, was TODO): **A/Enter starts immediate playback** — replaces the queue, playing from that song — wired in Slice 10; until then rows are inert with a visible note (§15). A **context menu (Menu button, ContextFlyout)** carries secondary actions; "Go to album" is live from Slice 08.
- 2026-07-13: Songs browse data source: **search3 with an empty query** — the OpenSubsonic full-library contract — paged via songCount/songOffset (verified against Navidrome 0.62). Song order is server-defined. Servers that do not report OpenSubsonic get an explicit "not supported" state instead of a misleading empty list (Section 3).
- 2026-07-13: Browse paging: Albums and Songs load **incrementally** (ISupportIncrementalLoading via one shared `IncrementalSource<T>`, 100 items/page), because getAlbumList2/search3 cap one request at 500 items. A failed later page surfaces as the page-level error with Retry (reload from the top).
- 2026-07-13: B-button back navigation is wired in the shell (SystemNavigationManager → content frame), needed once detail views made the hierarchy deep; the nav pane selection resyncs after back. Setup can never be reached by B (its back stack is cleared on connect).
- 2026-07-13: **Playlist editing scope for v1 is read-only** (§7.4/§17 resolved). Slice 09 ships Playlists browse (grid default + list alternate + the shared §13.5 `ViewModeToggle`) with client-side composite tile artwork; no create/edit/delete/reorder. Playlist tiles are focusable but **inert** (no detail/playback view) until a later slice, since opening/playing a playlist depends on the Slice 10 queue infrastructure. Composite artwork is composed from constituent album covers via `Core/Playlists/PlaylistArtwork` (up to 4 distinct, single cover when one album, no server artwork endpoint), fetched **lazily per tile** (getPlaylists carries no track list, so each tile needs one getPlaylist call — done in parallel after the grid is shown; a per-tile failure degrades to the placeholder).
- 2026-07-14: **Automatic 200% XAML scaling disabled; app draws edge to edge** (Section 2, console checkpoint #3 finding: black TV-safe bands on all edges and doubled content sizes). `ApplicationViewScaling.TrySetDisableLayoutScaling(true)` gives the true 1920×1080 layout canvas the app's dimensions were designed for, and `SetDesiredBoundsMode(UseCoreWindow)` opts out of the system-reserved TV-safe border. Overscan is handled by the app's own page padding, as §2 explicitly permits; a full safe-area/padding audit belongs to Slice 14 polish. Both calls are no-ops on desktop.
- 2026-07-14: **Settings and Account moved from the NavigationView PaneFooter into the menu list** (Section 5), as the last two items after a separator. The OS XAML NavigationView's PaneFooter proved to be a gamepad focus dead zone on console (three key/focus-interception approaches failed on device); as ordinary menu items, controller focus, selection, and invoke work natively. Deviation accepted: they sit at the bottom of the nav order but are not pinned to the pane's bottom edge. If true bottom-pinning is ever required, WinUI 2's NavigationView (FooterMenuItems) is the upgrade path.
- 2026-07-14: **Focus convention: content owns focus** (Sections 2/13.1). After any page reveals its interactive state (initial load complete, or an error state with Retry), focus moves into that content — implemented via a shared deferred-focus helper (`ContentFocus`), because Frame navigation destroys the focused element and XAML's fallback strands the controller in the nav pane. Error states focus their Retry button; empty states leave focus in the nav (nothing to interact with).
- 2026-07-14: Inline gamepad text editing on the setup flow (Sections 4/13.1; console checkpoint #3 finding): with the OSK dismissed, **X = backspace and LB/RB = caret movement** in the text fields, mirroring the Xbox OSK's own accelerators; on the password field (masked, and PasswordBox exposes no caret/selection API) **X clears the whole field**. A static hint line documents the mapping on-screen; dynamic input-glyph swapping remains a later §13.1 item. These are gamepad-only virtual keys, so no device-family gate is needed.
- 2026-07-15: **Slice 10 playback engine** (Sections 10/11; design doc `docs/architecture/30-playback-architecture.md`): queue logic lives in `Core/Playback/PlayQueue` (replace-not-append, shuffle mapping, repeat Off→All→One, unit-tested without a UWP host); the app's `PlaybackService` mirrors it into a **MediaPlaybackList** — gapless playback, next-track prefetch, native SMTC next/previous. List items stay in original order; shuffle is expressed via `SetShuffledItems`, so toggling never interrupts the current track. Repeat-one is handled manually (the list has no per-item repeat). Stream URLs come from `GetStreamUri` with no format/maxBitRate overrides in v1 — the server's player defaults decide (known consequence: the bar's format/bitrate labels describe the file's tags, not a transcoded stream).
- 2026-07-15: **Shuffle semantics** (Sections 8/9/10): a Shuffle action randomizes the whole pool including the first track; toggling shuffle ON mid-play keeps the current track playing and randomizes the upcoming order (current pinned first); toggling OFF restores the original order at the current track. **Previous** restarts the current track when it is >5 s in (both the in-app button and SMTC), otherwise moves to the previous track.
- 2026-07-15: **Scrobbling is in** (Section 6.2 dependency): the app reports plays via `scrobble` (`submission=true`, core Subsonic API) once a track passes half its duration or 4 minutes, whichever is less — otherwise play counts, and therefore Most Played, would never reflect listening done in this app. Fire-and-forget; failures are silent (telemetry, not UX).
- 2026-07-15: **Songs-row queue semantics** (Section 7.3, completes the 2026-07-13 decision): A/Enter replaces the queue with **all rows loaded so far**, positioned at the chosen song; later pages are not auto-fetched into the queue.
- 2026-07-15 (console checkpoint #4): **The compact now-playing bar also carries a favorite heart** for the current track (Rafael's request — an addition to Section 10's control list), using the same star/unstar + checked-visuals + revert-on-failure pattern as the detail pages. **Scrobbling gained the standard "now playing" report** (`submission=false` on every track start) after the play-count-only implementation read as broken in Navidrome's UI (its Now Playing panel is fed exclusively by that report; the endpoint itself was verified live). **Re-invoking the current section in the nav returns to that section's list** when a detail page is showing (Section 5 behavior; `ItemInvoked`, since `SelectionChanged` never fires for an already-selected item). Bar focus hardening: explicit `XYFocusLeft/Right` chains between the bar's controls, and Up from the bar is constrained to the content frame (the XY engine preferred the nav pane's bottom items).
- 2026-07-15 (checkpoint #4 second round): **Favorite-heart visual language** (Sections 8/9/10/13.3): starred = an accent-filled heart with a thick black rim (a larger black filled heart layered underneath), unstarred = the outline heart in the theme foreground; the ToggleButton chrome no longer carries the state. This required shipping the icon set's **Filled style** (`FluentSystemIcons-Filled.ttf`, same set, same MIT license — an extension of the 2026-07-10 §13.3 decision, not a new icon set; filled heart U+F47E verified against the shipped TTF's cmap). Implemented once as `Controls/FavoriteHeart` (§13.5 spirit). **Song lists mark starred songs** with a small filled accent heart in a dedicated column (Songs browse + album tracks now; playlist song lists when that view lands). Browse views' `XYFocusUp` is pinned to the view-mode toggle (Up from content reached the nav pane instead). **Song context menus carry a favorite toggle** ("Add to favorites"/"Remove from favorites", label tracks state, row heart repaints in place) on the Songs rows and album track rows; the row mutates only after the server accepts, so a failure changes nothing visible.
- 2026-07-16: **Slice 11 expanded queue** (Section 10; design doc
  `docs/architecture/30-playback-architecture.md`, Slice 11 section; four
  presentation decisions confirmed with Rafael up front): the expanded state
  is a **full-screen overlay** sliding up over the whole shell (nav pane and
  bar included) — two panes, large artwork + metadata + the full transport
  cluster (incl. favorite) left, the queue right. The queue list shows the
  **whole queue in current play order** (not just up-next; §8-of-acceptance
  "jump to ANY queued track" includes backwards), current track marked with
  the accent now-playing marker, scrolled into view + focused on open.
  Queue rows are **jump-only** in this slice (A/Enter jumps playback; the
  established row context menu is a refinement-phase candidate). **Y toggles
  the overlay globally** whenever a queue exists (the §10 SHOULD); B and
  Escape close it, and a visible chevron button covers mouse users (§13.1).
  Jumping is `MediaPlaybackList.MoveTo` on the live list (queue/shuffle/
  prefetch survive); the platform leaves MoveTo's index space unstated under
  shuffle, so the jump verifies MoveTo's returned item and corrects once if
  the platform indexed the other collection. Focus is trapped by disabling
  the NavigationView beneath the overlay while it is open.
- 2026-07-16: **Slice 12 theme selector** (Section 12): the preference
  (Automatic/Dark/Light, default Automatic, LocalSettings `theme.mode`) is
  applied as `RequestedTheme` on the **root Frame** — `Application.RequestedTheme`
  cannot change at runtime, and the root-frame value reaches every page
  (setup included) plus flyouts/tooltips through their placement targets.
  **Automatic resolves the system theme explicitly** via
  `UISettings.GetColorValue(UIColorType.Background)` (black = dark) and
  re-resolves on `UISettings.ColorValuesChanged` (dispatched to the UI
  thread; the `UISettings` instance is held statically so the event
  survives GC), giving live follow of a system theme change instead of the
  launch-time snapshot `ElementTheme.Default` would give. The event was
  undocumented for Xbox; **console checkpoint #6 (2026-07-17) verified it
  DOES fire when the Xbox theme changes** — live-follow works on device.
  UI: three RadioButtons on Settings (chosen over a
  ComboBox for 10-ft/controller use — no popup focus scope, all options
  visible, one A-press), applying instantly like the console's own theme
  setting; the page's XYFocus chain extends through them.
- 2026-07-16: **Slice 05b resume semantics + persistence shape** (Section
  6.1): the Home card's **Play starts playback immediately** at the saved
  track and in-track position (confirmed with Rafael; "MUST NOT auto-play
  on launch" is satisfied because launch restores nothing — only the card
  renders, and the queue is rebuilt when Play is pressed). Persistence
  splits by write rate: the queue **structure** (tracks in original order,
  shuffled play order as indices, shuffle/repeat, `QueueContext`, owning
  server URL + username) is a LocalFolder XML file (`ResumeState` in Core,
  reflection-free XDocument) rewritten only on structural changes; the
  **current index + position** are LocalSettings scalars written on track
  change/pause/suspend and ~10 s throttled during playback, overriding the
  file at load. **Stream URLs never persist** (they embed the auth token —
  §4); only song ids do, and URLs are rebuilt at resume. State is
  validated against the active server login at load and cleared on
  sign-out/stop; any malformed part rejects the whole state. The card
  shows only when no queue is active in the session. `PlayQueue` already carries everything the resume contract needs (track references + `QueueContext` display name/artwork), so persistence bolts on without model changes. The now-playing bar's **queue-expand control ships disabled until Slice 11** (visible-deferral pattern, Section 15 spirit); the Y-button mapping lands with Slice 11 (§10's "documented as pending" clause).
- 2026-07-17: **Playlist detail/playback view** (the piece deferred out of
  Slice 09; Sections 7.4/10): the layout mirrors album detail (Section 9 as
  the template) — client-side composite header artwork at 320 epx (the same
  §7.4 composition as the browse tile, resolved with the page load since
  getPlaylist returns the songs), playlist name, owner, song count + total
  duration. Play/Shuffle replace the queue with the playlist's songs in
  stored order (Shuffle randomizes the first track too, §8 semantics);
  song rows carry position numbers, starred markers, the current-track
  highlight, A/Enter jump-to-track, and the standard context menu — Play,
  favorite toggle, **Go to album AND Go to artist** (neither is circular on
  this page). **The playlist itself has no favorite heart:** the Subsonic
  star/unstar endpoint covers songs, albums, and artists only — no playlist
  form exists, so the control is intentionally absent. The `QueueContext`
  (resume-card) artwork is the first constituent album cover — the
  single-image cut of the §7.4 composition. Playlists remain read-only in
  v1; browse tiles now open the detail view.

- 2026-07-17 (console checkpoint #6 feedback): **Queue edits "Play next" /
  "Add to queue"** join every song context menu (Songs rows, album track
  rows, playlist rows) — an addition to §10's control surface, decided with
  Rafael. Semantics: "Play next" inserts directly after the current track in
  the play order (repeated inserts each land at current+1, so the most
  recent plays first); "Add to queue" appends to the end of the play order.
  Neither interrupts playback; under shuffle the original order receives the
  song at the end, so un-shuffling places it last. With **no active queue
  both actions play the song immediately** as its own queue (confirmed:
  never build invisible/silent state). Repeated edits on the same row clone
  the track instance (the queue and player mirror key by reference).
  **Context menus gained icons** (§13.3, all cmap-verified in the shipped
  Fluent Regular TTF): play F606, play-next F56A, add-to-queue F7A8
  (text_bullet_list_add), heart F47A, album E010, person F5BE.
- 2026-07-17 (console checkpoint #6 feedback): **Visible controller hints +
  quick nav** (§13.1). Hints stay per-surface static text (the Songs-header
  pattern; dynamic input-glyph swapping remains deferred): the expanded
  queue gained "A jump / Y-B close" text, both detail pages' song lists
  gained "A play / Menu options" text, and the nav pane's footer shows the
  global shortcuts (non-focusable text — the 2026-07-14 PaneFooter focus
  dead zone concerned focusable items; hidden while the pane is collapsed).
  **LB jumps focus to the nav pane, RB into the page content**, app-wide in
  the shell (inert while the queue overlay traps focus). Expanded-queue
  Left-from-row focus is pinned to the play/pause button — checkpoint #6
  showed the XY engine picking whatever transport control was vertically
  nearest, which read as inconsistent.

- 2026-07-18 (console checkpoint #7 feedback): **Container queue edits** —
  "Play next"/"Add to queue" extend from songs to whole **albums, artists,
  and playlists** (§10): two-item context menus (same icons) on every card
  surface (Albums browse grid/list, artist-detail album grid, Home
  carousels, Playlists grid/list, Artists grid/list) and icon-only
  buttons next to Play/Shuffle on the three detail pages (placement
  confirmed with Rafael). The block keeps its internal order (album =
  disc/track, playlist = stored, artist = the §8 oldest-album-first pool)
  and lands at current+1 / at the end; original-order placement and the
  empty-queue play-immediately rule follow the 2026-07-17 song semantics.
  Card menus fetch the songs on demand; a failed fetch changes nothing
  (menu-closed contract). Artist adds reuse the §8 getAlbum fan-out,
  shared with artist detail via `QueueActions.LoadArtistPoolAsync`.
- 2026-07-18 (console checkpoint #7 feedback): **Controller hints are
  drawn button badges, not sentences** (§13.1): `Controls/ControllerGlyph`
  renders a monochrome outlined circle with the button letter (A/B/X/Y),
  a pill for LB/RB, and the Fluent hamburger in a circle for the Menu
  button — deliberately NOT the Xbox brand colors (Rafael's call) and not
  a second icon font (§13.3 stays one set). Badges + short labels replace
  the static hint sentences on the Songs header, both detail-page song
  lists, the expanded queue, and the nav pane footer. Dynamic
  input-device glyph swapping remains deferred.
- 2026-07-18 (console checkpoint #7 feedback): **LB/RB cycle the shell
  regions** (§13.1, superseding the 2026-07-17 fixed mapping as a strict
  superset): RB moves nav → content → now-playing bar → nav, LB the
  reverse; the bar is skipped while hidden; both remain inert while the
  queue overlay traps focus. This is the requested "quick way to reach
  the play bar" without burning a new button.
- 2026-07-18 (console checkpoint #7 finding): **TV-safe shell inset**
  (§2): the nav pane was clipped by TV overscan — the app-side padding
  promised by the 2026-07-14 edge-to-edge decision now exists as
  `ShellRoot` Padding 32,20,32,20, sized so the pane's leftmost
  interactive pixel lands at the 48-epx TV-safe line and top/bottom
  clear 27 epx. Backgrounds still paint the full 1920×1080 canvas; on
  overscan-free displays the inset reads as a uniform background border
  (accepted). The setup page already sat inside the safe area.

- 2026-07-19 (console checkpoint #8 feedback): **Nav pane visual refresh**
  (§5/§13.2/§13.3). (a) The pane renders as a **rounded floating card**
  (radius 12, matching the now-playing bar): the OS NavigationView's own
  pane brushes are overridden to Transparent — they live in generic.xaml's
  theme dictionaries, the one resource class app-side overrides reliably
  reach — and the shell draws a rounded backdrop behind the pane whose
  width follows the open/compact state. (b) The built-in hamburger toggle
  is retired (it was also the app's last Segoe MDL2 glyph — a standing
  §13.3 violation) in favor of the Fluent **panel_left_contract /
  panel_left_expand** pair (E8B6/E8BA, cmap-verified) on an ordinary
  first menu item with `SelectsOnInvoked=False` — a menu item, not a
  floated button or PaneHeader content, because ordinary items are the
  pattern the gamepad focus engine is proven to handle (2026-07-14
  PaneFooter lesson). (c) Badge letters in `ControllerGlyph` center via
  `TextLineBounds=Tight` (checkpoint-#8 note: the default line box's
  descender space seated capitals low).
- 2026-07-19: **Nav icon size root cause + fix** (closes the item deferred
  2026-07-15): the OS left-pane item template renders the icon inside a
  Viewbox ("IconBox"), so `FontIcon.FontSize` never mattered, and the
  box's margin resource (`NavigationViewItemIconBoxMargin`) is defined at
  generic.xaml's **global scope** — resource references inside a system
  template resolve at the template's own parse scope first, so an
  App.xaml override is never consulted (on any device; both historical
  on-device failures explained). Fix: the shell sizes each item's named
  IconBox directly after template load (28 epx in the 48-epx icon
  column). Accepted fragility: a future OS rename of "IconBox" would
  silently restore small icons (fails soft).
- 2026-07-19: **App display name is "SubMusic Box"** (supersedes the
  2026-07-07 BoxSubMusic naming) — a **display-layer rename only**:
  manifest DisplayName/Description, tile/splash/store assets generated
  from `logos/SubMusic Box - logo.png` (square tiles use the "S"-card
  crop — the wordmark is illegible below ~300 px; wide tile and splash
  use the full logo padded on its own #1E1E1C background, which is also
  the manifest tile/splash BackgroundColor), the setup-page header
  (logo + title), and the Subsonic `c=` client name (`SubMusicBox`).
  Internal identifiers deliberately keep the BoxSubMusic code name:
  solution/project/namespace names, the PasswordVault resource
  (renaming would orphan stored credentials), and the package identity
  GUID. Tests assert the client name via `SubsonicApiClient.ClientName`,
  not a literal, so the code stays project-name-agnostic.

## 17. Open Decisions / TODO

- TODO: choose exact search endpoints/scopes and pagination/virtualization behavior for large libraries — resolve before Slice 13. (Browse views settled on incremental loading 2026-07-13; search may follow the same pattern.)
- RESOLVED 2026-07-13: playlist editing scope for v1 is **read-only browse** (see §16). Create/edit/delete deferred beyond v1 unless later requested.
- TODO: define error-state copy guidelines and offline/unreachable-server retry UX in more detail.
