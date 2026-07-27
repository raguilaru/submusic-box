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
  7. Favorites
  8. Settings
  9. Account / server info at the bottom area
- Search filters MUST appear in the main content area once search is active, not in the left nav.
- Search and Home MUST always remain visible and MUST NOT be hideable.
- Artists, Albums, and Playlists MUST each be independently hideable from Settings.
- Favorites (added 2026-07-26, §16) sits last of the library sections, after
  the browse destinations it draws from. It is not hideable: the three
  hideable sections are exactly those §12 names, and Favorites is reachable
  content that no other section duplicates.

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
- List rows SHOULD carry the metadata the artist listing already provides — the album count and the favorite state (added 2026-07-27, §16).
- List rows SHOULD also show each artist's track count and total play time (added 2026-07-27, §16). getArtists does not carry them, and they MUST NOT be fetched per row (one request per artist); they MUST be summed from the album list, whose cost scales with albums rather than artists.
- The summed figures MUST be applied only once the whole album list is in — a partial sum would understate an artist — and a failed or interrupted pass MUST leave the rows' album counts as they were rather than showing zeros.

### 7.2 Albums
- MUST support grid view by default.
- MUST support list view as an alternate mode.
- Grid entries SHOULD show album artwork.
- List rows SHOULD carry the metadata the album listing already provides — release year, genre, track count, total play time, and the favorite state (added 2026-07-27, §16) — and MUST omit any part the server did not send rather than showing a zero (§3).

### 7.3 Songs
- MUST be list-only.
- Song list MUST include:
  - title
  - artist
  - album
  - length
  - format
  - bitrate
- The view MUST offer a sort control with the options Default Order (the server's own order, the default), Title, Artist, Album, and Date Added (added 2026-07-26, §16).
- The view MUST offer Play All and Shuffle All actions that replace the queue with the entire song library — Play All in the current sort order, Shuffle All randomized (§10 replace-not-append).
- Because the Subsonic API cannot order songs, sorting is client-side over the fully loaded library; the sort and the two library actions MUST NOT operate on a partially loaded library, and MUST show why they are unavailable while it loads (§15).
- A sort whose data the server does not provide (Date Added on a server that omits `created`) MUST degrade explicitly rather than silently ordering by nothing (§3).

### 7.4 Playlists
- MUST support grid view by default.
- MUST support list view as an alternate mode.
- Playlist artwork MUST be generated client-side as:
  - a composite of up to 4 distinct album covers when multiple albums exist
  - a single cover when all playlist tracks share one album
- This artwork composition is a client responsibility and MUST NOT depend on a nonexistent Subsonic API endpoint.
- List rows SHOULD carry the metadata the playlist listing already provides — owner, the playlist's own description, track count and total play time (added 2026-07-27, §16) — omitting any part the server did not send (§3). Playlists have no favorite state anywhere in the app (star/unstar has no playlist form), so their rows carry no starred marker.

### 7.5 Favorites (added 2026-07-26)

- The Favorites view MUST show everything the signed-in user has starred,
  split into three tabs, in the order Artists, Albums, Songs.
- **Songs MUST be the tab a first visit lands on** — the default is the third
  tab, not the first.
- Data MUST come from `getStarred2` (the ID3 variant, matching the world of
  getArtists/getAlbumList2). The endpoint takes no counts or offsets and
  returns the complete favorites set, so this view has no incremental paging.
- The view MUST degrade explicitly (§3) on servers older than API 1.8, which
  is where getStarred2 was introduced.
- Artists and Albums MUST render with the browse-view card language
  (§7.1/§7.2); Songs MUST render with the §7.3 column set.
- The selected tab MUST persist locally across navigations and app restarts.
- Each tab MUST offer sort options appropriate to its content, and each tab's
  chosen sort MUST persist independently:
  - Artists: Alphabetical (default), Most Albums
  - Albums: Year (default), Alphabetical
  - Songs: Default Order (default, the server's own ordering), Title, Artist,
    Album, Date Added
- Sorting MUST reorder the whole scope, not a partially loaded prefix of it.
- Selecting a song MUST replace the queue with the favorite songs in their
  current sort order, positioned at that song (§10 replace-not-append, the
  §7.3 row semantics).
- The view MUST provide **Play All** and **Shuffle All** actions covering the
  whole active tab in its current sort order (§10 replace-not-append; Shuffle
  randomizes the entire pool including the first track, per §8). On the
  Artists and Albums tabs this requires fetching each container's songs, which
  MUST be bounded-parallel (§16 2026-07-19) and MUST show progress and disable
  the actions while it runs rather than appearing inert.
- Every result MUST carry a context menu appropriate to its kind:
  - songs: Play, Play next, Add to queue, Remove from favorites, Go to album,
    Go to artist
  - albums: Play, Play next, Add to queue, Remove from favorites, Go to artist
  - artists: Play, Play next, Add to queue, Remove from favorites
- Removing an item from favorites here MUST remove it from the list — a
  favorites list showing an unfavorited item would contradict its own premise.
- Each tab MUST have its own explicit empty state naming that scope.

## 8. Artist Detail View

- MUST show large artist artwork.
- MUST show artist name.
- MUST show a toggleable favorite heart.
- Favorite state MUST be persisted through Subsonic star/unstar behavior.
- MUST show total album count and song count.
- SHOULD also show total play time and the span of years the artist's albums cover (added 2026-07-27, §16), both computed from the albums the view already loaded; each MUST be omitted when the server provided no data for it (§3).
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
  - genre, when the server tagged one (added 2026-07-27, §16)
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

### 10.1 Lyrics (added 2026-07-22, §16)

- The app MUST display lyrics for the current track when the server provides them, using the OpenSubsonic `songLyrics` extension (`getLyricsBySongId`).
- Lyrics support MUST be gated on capability detection: when the connected server does not advertise the `songLyrics` extension, the lyrics UI (the bar's lyrics button and the overlay's Lyrics tab) MUST be hidden rather than shown broken (§3 explicit degrade).
- Lyrics MUST be presented inside the now-playing experience:
  - a lyrics control on the now-playing bar that opens the expanded overlay directly to a Lyrics view;
  - a Lyrics view alongside the queue in the expanded overlay (the queue remains reachable — Y still opens it).
- Synchronized lyrics (lines carrying timestamps) MUST auto-follow playback: the current line is highlighted and the view auto-scrolls to keep it visible.
- The user MUST be able to navigate the lyrics with the controller (read the lines ahead/behind the current position). Moving the selection alone MUST NOT change playback position; an affordance MUST let the user return to and resume following the current line without moving playback.
- Selecting a synchronized lyric line (A/Enter, or a click) MUST seek playback to that line's timestamp and resume following from there (revised 2026-07-26, §16 — this supersedes the strictly read-only rule of the original contract and is the one in-track seek the app performs). Lines with no reachable timestamp, unsynchronized lyrics, and streams the platform will not seek MUST fall back to the return-to-current-line behavior rather than doing nothing.
- Unsynchronized lyrics (no timestamps) MUST display as readable, scrollable text without a highlight.
- When the server supports lyrics but the current track has none, the app MUST show an explicit "no lyrics" state (not a blank or an error).
- Lyrics MUST follow the current track, updating when playback moves to another song.
- Word/syllable-level ("enhanced") lyrics and multi-language selection are out of scope for v1 (line-level lyrics only).

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
  from `logos/SubMusic Box - logo.png` (**every asset shows the complete
  logo, only scaled** — Rafael's call after the first console pass
  showed a cropped "S"-card variant; wide tile and splash pad the square
  logo to aspect on its own #1E1E1C background, which is also the
  manifest tile/splash BackgroundColor), the setup-page header
  (logo + title), and the Subsonic `c=` client name (`SubMusicBox`).
  Internal identifiers deliberately keep the BoxSubMusic code name:
  solution/project/namespace names, the PasswordVault resource
  (renaming would orphan stored credentials), and the package identity
  GUID. Tests assert the client name via `SubsonicApiClient.ClientName`,
  not a literal, so the code stays project-name-agnostic.

- 2026-07-19: **Slice 13 Search — endpoint, trigger, scopes, and paging**
  (resolves the §17 TODO; all four decisions confirmed with Rafael).
  (a) **Endpoint: search3** (ID3-based, the same world as getArtists/
  getAlbumList2) — one request fills any combination of the artist/album/
  song scopes; search3 exists since API 1.8, so pre-1.8 servers get an
  explicit unsupported state (§3) rather than a legacy search2 fallback.
  (b) **The query runs on submit** (Enter / the console OSK's accept; a
  Search button covers mouse users) — no live-as-you-type requests.
  (c) **Results: an "All" view + scope filters in the content area** (§5):
  All shows the top 6 artists, 6 albums, and 10 songs from one request in
  three sections (empty sections collapse); segmented Artists/Albums/Songs
  filters switch to a full single-scope list paged incrementally via
  search3 offsets (the browse-view `IncrementalSource` pattern, 100/page).
  Result order within a scope is server-defined (search3 has no client
  sort). (d) **A/Enter on a song result replaces the queue with the song
  results loaded in that list** (All's top matches, or the scoped list's
  pages so far), positioned at the chosen song — the Songs-browse
  semantics; artist/album results open their detail pages. Song rows carry
  the full context menu (Play, queue edits, favorite, Go to album AND Go
  to artist); artist/album cards carry the container queue edits.
  Supporting decisions: the Search page is **navigation-cached** so B-back
  from a result's detail page returns to the same query and results — the
  one deliberate exception to the refetch-on-navigation pattern (starred
  markers on kept results may lag until the next search); the query box
  gets the SetupPage **inline gamepad editing** (X backspace, LB/RB caret),
  which deliberately outranks LB/RB region cycling while the box has focus.

- 2026-07-19: **Slice 14 resilience scope + toast feedback surface**
  (§3/§10/§15; scope confirmed with Rafael — the audits, the
  no-silent-failure fixes, and the queue-row context menu; seek support,
  dynamic input glyphs, and the minor visual-polish items stay deferred
  beyond v1). (a) **One app-wide transient toast** (non-focusable,
  hit-test-invisible, topmost in the shell so it shows over the expanded
  queue; auto-dismisses, newest message wins) carries exactly the feedback
  that has no other visible effect: queue-edit confirmations ("will play
  next" / "added to the queue"), container-fetch failures after the menu
  closed (the contract now *reports* that nothing was added), per-track
  stream-failure skips, and the consecutive-failure playback stop. Play
  actions and the empty-queue play-immediately path show no toast —
  audible playback and the bar reveal are their own feedback. (b) **The
  expanded queue's rows gained the standard context menu** (the Slice 11
  deferral): Play (jump), favorite toggle, Go to album / Go to artist;
  the navigation items close the overlay first and the destination page's
  content takes focus. (c) **Server-facing fan-outs are bounded to 4
  concurrent requests** (the §8 artist song pool and the §7.4 playlist
  tile artwork) via Core's order-preserving `BoundedParallel` helper —
  a 100-album artist no longer fires 100 simultaneous getAlbum calls.
- 2026-07-19: **Error-state copy guidelines** (closes the §17 TODO by
  codifying the practice the app already follows): sentence case, plain
  words, no error codes; each message says what happened, what state the
  app is in now, and — when the user can act — what to do next. Surface
  mapping: retryable page loads get the inline error state with a focused
  Retry; auth failures point at Account → sign out; missing capabilities
  get an explicit unsupported state without Retry (§3); failures whose
  originating surface is already closed (context-menu fetches) and
  background events (skipped tracks, the failure stop) use the toast;
  successes toast only when nothing else visible would confirm them
  (queue edits). Silent-by-design remains limited to telemetry-class
  calls (scrobbling) and the favorite heart's snap-back, both recorded
  decisions.
- 2026-07-19: **Bitrate restriction: the server-side limit is
  authoritative — no client-side quality setting** (Rafael's call,
  superseding the client-setting shape briefly recorded earlier the same
  day; outcome of the bitrate-restriction investigation, detail in
  STATUS "Known gaps"). The v1 no-overrides decision on `stream` stands
  as the way the app *respects* a server-enforced restriction: the
  server transcodes, the client streams what it is given. The failures
  observed were server-side (Navidrome 0.62.0 ignored player MaxBitRate
  on /stream — their #5583, fixed in 0.63.0; a lossless transcoding
  target still silently serves raw). The one app-side piece, implemented
  in **Slice 14**: a **decode-failure mp3 fallback** — when a stream
  fails with a decode-class error (DecodeError/SourceNotSupportedError — the
  signature of a server transcode format this hardware lacks, e.g.
  Navidrome's Opus default), the track is retried once with
  **`format=mp3`** (core API 1.6.0, decodable on every Xbox; the server
  still applies its own bitrate cap to that transcode, so the
  restriction stays respected — works on every Subsonic/OpenSubsonic
  server, §3). Once a fallback item audibly plays, the session builds
  further stream URLs as mp3 directly, so a fully-transcoded queue does
  not fail-and-retry on every track (already-built items in the current
  queue still retry individually — accepted hiccup). Network-class
  failures keep the normal skip/stop path untouched. Research
  (2026-07-19): Microsoft's Xbox codec table lists no Opus/Ogg at all
  (audio set: AAC/MP3/FLAC/ALAC/WAV/WMA/AC-3), so the fallback engaging
  is the EXPECTED path on a restricting Navidrome; console-verify that
  it engages cleanly.
- 2026-07-19: **Playback surfaces show the DELIVERED stream's
  format/bitrate, not the file's tags** (closes the §10 display known
  gap; prompted by Rafael's on-device observation that a
  server-restricted stream — delivered as Opus 32 kbps — still showed
  "M4A · 256 kbps"). The now-playing bar and the expanded queue read
  the current item's audio-track encoding properties
  (`PlaybackService.TryGetDeliveredStreamInfo`: codec subtype +
  declared bitrate) — platform-sourced, so it is server-agnostic (§3),
  needs no server extension, and also reflects the mp3 decode-fallback
  when it engages. Tag values remain only as the brief pre-open
  fallback (the next Changed after the stream opens refreshes the
  line), and streams that declare no bitrate (VBR Opus) show the codec
  alone rather than a guessed number. **Browse/search song lists
  deliberately keep the file's tag metadata** (§7.3): a list row
  describes the file in the library; what a future playback of it
  would deliver depends on server transcoding config at that moment
  and is unknowable per row without streaming it.

- 2026-07-19: **"Play" joins the container card context menus** (§10;
  Rafael's request): every artist/album/playlist card menu (browse
  grids/lists, Home carousels, search result cards, the artist-detail
  album grid) now opens with Play above Play next / Add to queue. Play
  is the standard §10 replace-the-queue action — identical semantics to
  the detail page's Play button (album = disc/track order, playlist =
  stored order, artist = the §8 oldest-album-first pool), fetched on
  demand like the queue edits. A failed fetch changes nothing and
  toasts "…playback didn't start" (menu-closed contract).
- 2026-07-19: **Browse-page initial focus lands below the view toggle**
  (§13.1/§13.5; Rafael's request): on pages with a grid/list toggle
  (Artists, Albums, Playlists) the content-owns-focus pass targets the
  ACTIVE view, so first focus is the first card, not the toggle. The
  toggle remains always visible and one Up-press away (the existing
  XYFocusUp pins).
- 2026-07-19: **Retro visual theme — direction "B: retro flavored"**
  (§13.2/§13.3; direction, accent policy, typography, and motifs
  confirmed with Rafael through a design Q&A with mockups): the app's
  visual identity derives from the SubMusic Box logo. (a) The logo
  palette REPLACES the console accent app-wide — dark theme: seafoam
  #7FADA9 accent, cream #E9DCC0, petrol sleeve teal #253B3F surfaces;
  light theme: deep teal #2F6B66, aged tan, pale seafoam — implemented
  as App.xaml theme-dictionary resources (`Retro*`) plus overrides of
  `SystemAccentColor` and the reachable control brush keys (key names
  verified against the 26100 generic.xaml). (b) Surfaces: the nav pane
  backdrop and the now-playing bar paint in the sleeve teal; nav
  section icons take the accent; progress lines and the play/pause
  primary fill in cream with per-theme ink. (c) Motifs: the logo's
  triple-stripe underline (`RetroStripes`) under section headers; a
  drawn vinyl record (`VinylDisc`) as the album-artwork placeholder
  (artist/playlist glyph placeholders unchanged); a spinning vinyl
  (`SpinningVinyl`, independent-animation storyboard, spins only while
  visible and playing) as the current-track marker; 1-epx tan
  "parchment" frames on card artwork and detail headers. (d) Typography
  stays Segoe UI (no display font in this round). Dynamic input glyphs
  and further §13.1 items remain deferred.

- 2026-07-22: **Lyrics display (OpenSubsonic `songLyrics` extension)**
  (§3/§10/§13.1/§13.2; refinement feature, placement + behavior confirmed
  with Rafael). (a) **Data:** `getLyricsBySongId` → the client's new
  `GetLyricsBySongIdAsync` + `StructuredLyrics`/`LyricLine` models
  (XDocument, reflection-free); line-level only (`enhanced`/`cueLine`
  word-timing and multi-language selection out of scope for v1 — the app
  prefers a synced entry, else the first, and treats an entry claiming
  `synced` with no usable timestamps as plain text). (b) **Placement:** a
  **Queue ⇄ Lyrics segmented toggle on the right pane** of the expanded
  Now-Playing overlay — the left pane (artwork/transport/progress) is
  unchanged; reuses the overlay focus trap and slide. (c) **Entry:** a
  lyrics button on the now-playing bar opens the overlay straight to the
  Lyrics tab (Y still opens Queue). (d) **Navigation is READ-ONLY** (no
  in-track seek is introduced, `PlaybackService` gains none): synced
  lyrics auto-highlight and auto-scroll to the current line; D-pad reads
  ahead/back and pauses the follow (resumes on A / the "Jump to current
  line" button / a few seconds idle); **A does not seek — it re-centers.**
  Unsynced lyrics are a plain scrollable list with no highlight/timer.
  (e) **Capability gate + degrade (§3):** with no `songLyrics` extension
  the bar button and the Lyrics tab are hidden (the overlay stays
  queue-only, "Queue" title). A supported server whose track has no
  lyrics (or a fetch failure — lyrics are a display extra, not a §15
  case) shows "No lyrics available for this track."; the loading state
  shows "Loading lyrics…". Lyrics follow the current track (refetched on
  song change; a one-entry cache avoids refetching on unrelated Changed
  events). Timestamps are milliseconds; the entry's `offset` is applied.

- 2026-07-26: **Selecting a synced lyric line SEEKS playback to it**
  (§10.1; Rafael's request — supersedes clause (d) of the 2026-07-22
  lyrics decision, whose read-only rule made A a pure re-center). A /
  Enter / click on a line moves the current track to that line's
  timestamp minus the entry's `offset`, resumes auto-follow, and paints
  the destination line immediately (the session's position readback
  settles a few ms after a seek, so the highlight is set explicitly
  rather than derived). **Merely moving the D-pad selection still does
  not seek** — reading ahead/back stays free, and the "Jump to current
  line" button keeps its §10.1 role as the return affordance that
  resumes following WITHOUT moving playback. Fallbacks (§15, never a
  dead button): unsynced lyrics, a line with no reachable timestamp
  (a blank spacer seeks to the nearest timed line above it; a line with
  none above it does not), and a stream the platform reports as
  non-seekable all fall back to re-centering.
  Implementation: the app's first and only in-track seek —
  `PlaybackService.SeekTo(TimeSpan)`, which clamps into the open stream
  (and stops `SeekEndGuard` = 500 ms short of the end so the last line
  cannot trip end-of-stream and skip the track), refuses when
  `MediaPlaybackSession.CanSeek` is false, re-baselines the §6.1 resume
  scalars (the ~10 s throttle would otherwise keep a stale position
  after a short backward seek), and raises `Changed`. **This does NOT
  introduce a general seek/scrubbing surface** — the now-playing bar's
  and overlay's progress lines stay display-only (STATUS "Known gaps"),
  because §10 asks for no scrubber. Accepted consequence: seeking past
  the min(50 %, 4 min) scrobble threshold submits the play, as the
  threshold is a position check, not a listened-time accumulator
  (telemetry-class, consistent with the silent-scrobbling decision).

- 2026-07-26: **Favorites section** (§5/§7.5; Rafael's request, sort
  options taken from a supplied reference screenshot). A new left-nav
  destination showing the starred library, with Artists/Albums/Songs tabs.
  (a) **Data: `getStarred2`** — the ID3 variant, so its artist/album entries
  are the same shapes the browse grids already render. It accepts no
  count/offset parameters and answers with the complete favorites set in
  **one request**, so this view needs no paging of any kind: one fetch fills
  all three tabs, tab switching costs no request, and sorting reorders the
  entire scope. Note the contrast with the Songs view (the same-day decision
  further down this log), which reaches the same "sort tells the truth over
  everything" guarantee the hard way — by paging the whole library into
  memory — because its endpoint has no such all-at-once form. API 1.8 gate
  with an explicit
  unsupported state (§3), same as search3.
  (b) **Tabs are segmented ToggleButtons** in the content area (the Slice 13
  scope-filter pattern), reading Artists / Albums / Songs. **Songs is the
  default tab** — deliberately not the first one: favorited songs are what
  people come to this view for, so that is where a first visit lands.
  (c) **Sort is a button + MenuFlyout of ToggleMenuFlyoutItems**, generated
  per tab because the option sets differ (2/2/5 entries): a fixed inline row
  would be wrong for two tabs out of three. (The Songs view declares its five
  items statically for the same reason in reverse — its set never varies.)
  MenuFlyout is the app's console-verified flyout (the row/card context
  menus) and its checkmark column reads as the current choice at 10 ft; the
  button's label always names the active sort, so the order is readable
  without opening the menu. Per-tab option sets and defaults are listed in
  §7.5. Album **Year** is newest-first with undated albums last.
  **The Songs tab reuses `Core/Browsing/SongSorter`** — the same orderings
  and the same `Label()` the Songs view uses, so the two pages can never
  drift apart and the ordering stays unit-tested in Core. That also settles
  song **Date Added** as the file's `created` timestamp rather than the
  `starred` one: it matches the Songs view, and it keeps the option distinct
  from Default Order (servers commonly return favorites
  most-recently-starred first). `Song.Created` was added independently by
  both this round and the Songs round on the same day, converging on the
  identical optional-trailing-parameter shape.
  (d) **Tab AND per-tab sort persist** in LocalSettings
  (`FavoritesPreferences`). Rafael asked for the tab; the sort follows,
  because a remembered tab with a forgotten sort would be incoherent.
  (e) **Removing a favorite drops the row** from the list (after the server
  accepts — the standard change-only-after-success rule), unlike every other
  song list, which repaints the heart in place. There the row still belongs
  in the list; here the list *is* the favorites set.
  (f) Song rows play with the §7.3/§10 semantics (replace the queue with the
  sorted favorites, positioned at the row) and carry the standard context
  menu; artist/album cards carry the container Play / queue-edit menu.
- 2026-07-26 (follow-up, same round): **Play All / Shuffle All + fuller
  result menus on Favorites** (§7.5; Rafael's request).
  (a) **Play All / Shuffle All act on the ACTIVE TAB**, in its current sort
  order — §10 replace-the-queue, and Shuffle randomizes the whole pool
  including the first track (§8). Songs is instant (the tracks are already
  loaded); Albums costs one getAlbum per favorite album; Artists costs a
  getArtist per artist *plus* a getAlbum per album of every one of them.
  Rather than nesting two bounded fan-outs — whose limits would multiply —
  the artist path fetches all artist details bounded, flattens every album
  across all artists into one list, and fetches those at the SAME single
  bound of 4 (`QueueActions.MaxParallelFetches`). Order survives end to end:
  artists in the tab's sort order, each artist's albums oldest-first (§8),
  each album's songs disc/track. **Accepted trade-off:** Play All on a large
  Artists tab is a genuinely long operation, so the actions disable
  themselves behind a progress ring while it runs (a dead-looking button
  would read as broken) and the sort button disables too — reordering
  mid-build would make the queue that lands disagree with the screen. The
  built pool belongs to the tab the button was pressed on, so switching tabs
  mid-build still plays what was asked for.
  (b) **Result context menus are filled out**: artist cards and album cards
  gain "Remove from favorites", and album cards gain "Go to artist" (not
  circular — A on the card opens the ALBUM, the same reason song rows carry
  both navigation items). Removal now behaves identically on all three tabs:
  the item leaves the list once the server accepts. Deliberately NOT added:
  a per-card "Shuffle" — no card menu anywhere in the app has one (the Songs
  view's Shuffle All is a page-level action, not a card one), and
  introducing it on this page alone would be an inconsistency; it is an
  app-wide change if wanted.
- 2026-07-26: **Songs gains sort options + Play All / Shuffle All**
  (§7.3/§10; Rafael's request, options taken from a reference screenshot:
  Default Order, Title, Artist, Album, Date Added). **The API cannot sort
  songs** — search3 has no sort parameter and no other endpoint lists the
  library in a chosen order — so ordering is client-side
  (`Core/Browsing/SongSorter`, unit-tested). Two consequences, both
  confirmed with Rafael: (a) **the page now loads the WHOLE library**
  (500/request, the API cap) instead of the scroll-driven incremental
  paging of the 2026-07-13 decision, because a client-side "Title A–Z"
  over a partial library would be a lie; the first page shows immediately
  and the rest streams in behind it, with the sort control and both
  library actions **disabled and captioned "Loading all songs…"** until
  every page is in (§15). (b) **Play All / Shuffle All cover the entire
  library** — Play All in the current sort order, Shuffle All randomized
  including the first track (§8 semantics) — matching the detail pages,
  where Play/Shuffle always cover the whole container.
  Ordering details: sorts are stable, so Default Order is a pure
  pass-through and ties keep the server's order; Artist and Album both
  fall through to disc/track so an artist's songs read in album order;
  Date Added is newest first with undated songs last. Alphabetical
  comparisons are culture-aware and case-insensitive.
  Supporting decisions: `Song` gained **`Created`** (the core Subsonic
  `created` attribute) to feed Date Added, and the Date Added option
  **disables itself** when no loaded song carries one (§3 — never sort by
  nothing). A/Enter on a row still replaces the queue with the rows as
  shown, which now follows the sort and, once loaded, means the whole
  library — the "rows loaded so far" cap of the 2026-07-15 decision now
  applies only during the load. **Songs becomes the second
  navigation-cached page** after Search (a deliberate second exception to
  refetch-on-navigation): re-running a whole-library load on every B-back
  from an album page would be untenable, and it inherits Search's
  documented trade-off that starred markers on kept rows can lag. A later
  page failing is now fatal to the load (inline error + Retry, reload from
  the top) rather than merely ending the scroll, because a silently
  truncated library would break what sort and Play All promise.

- 2026-07-27: **Browse list rows carry real metadata, and the view-mode
  toggle reads as a segmented control** (§7.1/§7.2/§8/§9/§13.2/§13.5;
  Rafael's request).
  (a) **The grid/list toggle is restyled onto the retro palette.** The stock
  ToggleButton painted its CHECKED state with the accent fill, so list mode
  looked like a switched-on setting while grid mode looked switched off —
  wrong information design for two peer modes. It now draws as a tonal-teal
  pill with the parchment card frame (the `RetroTonalButtonStyle` language),
  holding two always-visible glyph segments with a thumb under the current
  one plus the mode's name; the surface stays identical in every checked
  state and only the thumb and the inactive glyph's opacity move.
  It remains **one focusable ToggleButton** — one A-press flips the mode and
  the console-verified focus story (`FocusTarget` / `FocusDownTarget`, Up from
  the content lands here) keeps exactly one stop, which a real two-button
  segmented control would have doubled. All eight ToggleButton common states
  are declared in the template: an undeclared `Checked*` state leaves the
  previous state's visuals applied, which would strand the pill in its
  pressed fill after a gamepad press.
  (b) **List rows show what the listing endpoint already returned** — free
  data, no extra request, which is the whole constraint. Albums (getAlbumList2)
  gain year · genre · track count · total play time on the right of the row
  plus the starred marker; each part is omitted when absent rather than shown
  as a zero (§3). **Artists are asymmetric on purpose:** getArtists carries
  only `albumCount` and `starred`, so an artist's song count and play time
  would cost one getArtist per row — rejected on the §11 perf grounds that
  bounded the §8 fan-out — and those totals go on the artist detail page
  instead, where the albums are already in hand. Both row shapes are the same
  four-column grid so the two list views read as one family.
  (c) **Detail views absorb the totals**: artist detail's counts line gains
  total play time and the span of years its albums cover (summed from the
  loaded albums); album detail's header gains genre. Same free-data rule.
  (d) The duration wordings the detail headers had duplicated privately moved
  to Core as `Browsing/DurationText` (`Track`, `Total`) so the new rows and
  the old headers cannot drift, with the shapes now unit-tested.
  (e) **The "Artist" sort option's icon is the person glyph** (F5BE), not the
  mic (E7FA) it shipped with on 2026-07-26 — the app's artist iconography is
  the person/people figure everywhere else ("Go to artist", the Artists nav
  item), and a recording-input glyph read as "record". Changed in both places
  that declare the five song orderings (the Songs view and the Favorites
  Songs tab), which stay in step by rule. The Favorites Albums tab has no
  artist-related ordering, so nothing there changed.
- 2026-07-27 (follow-up, same round): **Playlist list rows join the pattern**
  (§7.4; Rafael's request). getPlaylists already returns `owner`, `comment`,
  `songCount` and `duration`, so the row shows owner + the playlist's own
  description on its second line and track count + total play time on the
  right — same three/four-column geometry as the Albums and Artists rows, no
  extra request. The description earns its place because it is the one field
  that says what a playlist is FOR; nothing else in the app surfaces it. No
  starred column: star/unstar has no playlist form (2026-07-17), so playlists
  have no favorite state to show. `changed`/`created` are deliberately NOT
  shown — the client does not parse them today, and "updated 3 weeks ago" is
  weak information on a read-only browse list; adding them would mean
  extending the client's Playlist model for a caption.
  **Artist row totals were measured and rejected in this form** (§7.1): against
  Navidrome 0.63.2, getArtists returns `albumCount` and `starred` and no
  song/duration totals, so per-row totals mean one getArtist per visible row
  (~16–20 ms each warm on LAN, but N requests and RTT-bound off-LAN). If those
  totals are ever wanted on the browse list, the cheap route is summing
  getAlbumList2 pages by `artistId` — every album carries `artistId`,
  `songCount` and `duration`, so the cost is ceil(albums / 500) requests
  regardless of artist count, and the sums would be album-artist sums (which is
  the same grouping getArtists itself indexes by). Not implemented: it puts a
  library-wide pass on a page that is one request today.
- 2026-07-27 (follow-up): **Artist list totals implemented via the album-list
  aggregate** (§7.1; Rafael's call after seeing the measurements above, which
  this supersedes). `Core/Browsing/ArtistTotals` folds album pages into
  per-artist album/track/duration sums; `ArtistsPage` runs one pass over
  getAlbumList2 at the API's 500-item cap after the rows are already on screen,
  then fills each row's right-hand line in place ("3 albums · 42 tracks ·
  2 hr 51 min"). Cost is ceil(albums / 500) requests regardless of artist
  count — the per-row getArtist alternative was rejected as RTT-bound
  (measured ~19 s for 500 artists at a 150 ms RTT).
  Design rules, all of them about not lying:
  (a) **Totals are applied only when the WHOLE list is in.** A per-page
  application would briefly show an artist's partial sum, and a wrong number is
  worse than the album count the row already shows honestly.
  (b) **A failure or a mid-pass navigation applies nothing** — rows keep their
  album counts, which remain correct. No error state and no toast: the page the
  user asked for loaded fine, and nothing they requested went missing, so this
  is not a §15 silent degrade.
  (c) **The sums are album-artist sums** (a guest appearance counts under the
  album's artist) — the same grouping getArtists indexes by, so they agree with
  the album count beside them and with artist detail, which sums the same albums.
  (d) **Albums the server sent without an `artistId` are skipped** rather than
  attributed to anyone.
  (e) Only the running sums are held, not the album pages, so the memory cost is
  one small entry per artist regardless of library size (§11).
  Deliberately NOT included: sorting the Artists browse view by these figures
  (the view has no sort control at all today — adding one is its own decision),
  and totals on the Favorites Artists tab, which draws a different, usually much
  smaller set from getStarred2 and would need its own pass.

## 17. Open Decisions / TODO

- RESOLVED 2026-07-19: search endpoint/scopes/paging — see §16 (Slice 13). search3, submit-triggered, All view + scope filters, incremental scoped paging.
- RESOLVED 2026-07-13: playlist editing scope for v1 is **read-only browse** (see §16). Create/edit/delete deferred beyond v1 unless later requested.
- RESOLVED 2026-07-19: error-state copy guidelines and unreachable-server retry UX — see §16 (Slice 14). Copy rules codified; retry UX stays the established pattern (inline error + focused Retry, reload from the top; toast for closed-surface and background failures).
- RESOLVED 2026-07-19: bitrate restriction — the server-side limit is authoritative, no client-side quality setting; the app's contribution is the Slice 14 decode-failure `format=mp3` fallback. See §16 (2026-07-19); Ogg/Opus decode remains a console-verify item.
