# Current Status

Last updated: 2026-07-19, sixth pass (**nav-icon fix: the collapsed-rail
correction stands; the expanded-label `ContentTemplate` was reverted
after it blanked all nav icons on device.** The fifth pass added two
nav fixes; on device the icons vanished completely. Diagnosed by
elimination: `CompactPaneLength="48"` only affects the collapsed pane
width and cannot hide expanded icons, so the culprit was the other
fifth-pass change — the `NavigationViewItem.ContentTemplate` override
(intended to apply `TextLineBounds="Tight"` to the label). Overriding a
NavigationViewItem's ContentTemplate disrupts how its presenter hosts
the item, taking the icons with it. Reverted; `CompactPaneLength="48"`
(collapsed-rail centering) and `VerticalContentAlignment="Center"`
(kept from round two) remain. The residual label-descender offset is a
minor deferred item needing a safer technique than a bare
ContentTemplate swap. Build green. Earlier passes the same day below.)

Previous update, 2026-07-19 fifth pass (**nav-icon alignment round three
— the real causes, found by reading the 26100 SDK's
`NavigationViewItemPresenter` template over SSH.** Collapsed rail: the
template's icon column is hardcoded to 48 epx but
`NavigationView.CompactPaneLength` was never set and defaulted to 40 —
an 8-epx mismatch invisible until measured, uniform across every icon
("unbalanced" rather than per-icon wobble); fixed by setting
`CompactPaneLength="48"` to match. Expanded labels: attempted via an
explicit `ContentTemplate` (`TextLineBounds="Tight"`) — REVERTED next
pass, it blanked the icons on device. Round two's per-glyph
square-FontIcon-bounds fix is kept but wasn't the real cause. Also:
Rafael confirmed the redesigned spinning-vinyl marker (dark record +
rim + grooves + seafoam label + stripe) looks right on device. Build
green. Earlier passes the same day below.)

Previous update, 2026-07-19 fourth pass (**playback surfaces now show
the DELIVERED stream's format/bitrate** — Rafael confirmed the
server-side bitrate restriction transcodes correctly on device
(Navidrome log: opus 32 kbps from a 256 kbps m4a) but the bar still
showed the file's tags; the bar + expanded queue now read the playing
item's audio-track encoding properties
(`PlaybackService.TryGetDeliveredStreamInfo`), closing the §10 label
known gap. Browse/search lists keep tag metadata by design. Build
green; backlog item 3 in the bitrate group covers the on-device
check. Earlier passes the same day below.)

Previous update, 2026-07-19 third pass (**UI/UX refresh round
code-complete, NOT yet device-verified** — Rafael's three requests:
"Play" on every container card context menu, browse-page initial
focus lands on the first card below the view toggle, and the **retro
visual theme** derived from the SubMusic Box logo (direction B
"retro flavored", confirmed via design Q&A: logo palette replaces the
system accent app-wide, petrol-teal nav pane + playback surfaces,
triple-stripe header motif, vinyl-disc artwork placeholders, a
spinning-vinyl current-track marker, parchment card frames; Segoe UI
kept). 3 new controls (RetroStripes, VinylDisc, SpinningVinyl); build
green, 76 Core + 78 SubsonicClient tests green; console items appended
to the backlog. Earlier passes the same day below.)

Previous update, 2026-07-19 second pass (**bitrate-restriction
investigation + decode-failure mp3 fallback added to Slice 14 after
checkpoint #12** — §16 decision: the server-side limit is
authoritative, no client-side quality setting; 4 new console items in
the backlog, 3 new SubsonicClient tests → 78. Earlier the same day:
**Console checkpoint #12: Rafael verified all
seven Slice 14 items on device — the LAST planned slice is closed.**
Queue-edit confirmation toasts, toast visibility over the expanded
queue, the container-fetch failure toast, per-track skip + failure-stop
toasts, the queue-row context menu with overlay-close navigation, the
bounded artist fan-out feel, and the throttled playlist tile artwork
all passed. **Every slice 00–14 is now implemented and
console-verified; the on-device backlog is empty.** Build green
(compile; APPX0703 stays the known command-line-only packaging
failure), **76 Core + 75 SubsonicClient tests green**. **PR #6 is
merged to main — v1's planned scope is COMPLETE.**)

## Where we are

**Phase 1 (foundation + spikes) is DONE, including on-device checkpoint #1**
(app launches, D-pad navigates, background audio + SMTC verified on console
2026-07-09).

**Phase 2 steps 2.1–2.4a + 2.5a–2.7 are code-complete** (older entries compressed; see git history for detail):

- **2.1 SubsonicClient (Slice-less, committed `9a542bf`):** typed
  OpenSubsonic/Subsonic client — token+salt auth with explicit pre-1.13
  legacy fallback, ping, capability detection, getAlbumList2, getCoverArt URL
  building; XML via XDocument. 27 tests green incl. 4 live Navidrome tests.
- **2.2 Slice 03 — setup flow (committed `ae9573e`, 2026-07-10):**
  - Core: `ServerUrlSanitizer` (trim, default https://, strip trailing
    slashes, reject non-http(s)/query/fragment) and `RecentServers`
    (newest-first, dedupe by server+user, cap 5, reflection-free XDocument
    XML). 23 Core tests green. Bootstrap `Placeholder` retired.
  - App: `SetupPage` (masked password, inline auth-vs-connection errors,
    recent-servers pre-fill via x:Bind), `CredentialStore` (password only in
    PasswordVault; URL/username in LocalSettings), `RecentServersStore`
    (LocalFolder XML; corrupt file = empty cache), `AppSession` (one shared
    HttpClient, 15 s timeout), launch routing (stored login → shell,
    none/corrupt → setup). Successful connect clears the back stack.
  - Manifest now declares `privateNetworkClientServer` + `internetClient`.
  - `App → Core → SubsonicClient` project references wired.
- **2.3 Slice 04 — navigation shell (implemented 2026-07-10):**
  - `MainPage` rebuilt as the real shell: persistent collapsible
    NavigationView, fixed §5 order (Search, Home, Artists, Albums, Songs,
    Playlists), content `Frame`, keyboard L/R focus handoff carried over from
    the verified spike.
  - **Icon set decided (§13.3): Fluent UI System Icons (MIT)**, shipped as
    `Assets/Fonts/FluentSystemIcons-Regular.ttf` (+ LICENSE.txt). Glyph
    codepoints taken from the set's official JSON map — never invented.
    The built-in NavigationView settings item is disabled (its gear is Segoe
    MDL2 — would mix icon sets); Settings + Account are PaneFooter items with
    manual invoke handling (Tapped + KeyDown for GamepadA/Enter/Space) and
    hand-mirrored IsSelected, because OS XAML NavigationView has no
    FooterMenuItems.
  - Nav-visibility plumbing (§5): `NavigationPreferences` (LocalSettings,
    default visible, Changed event) + working toggles on `SettingsPage` for
    Artists/Albums/Playlists. Search/Home have no setting by construction.
  - `AccountPage`: server URL, username, capability info when connected this
    session, and **Sign out** (clears vault + settings, returns to setup —
    also the practical way to re-test setup on device).
  - `SectionPage`: placeholder host for sections until their slices land.
- **2.4a Slice 05a — Home carousels (implemented 2026-07-10):**
  - **Slice 05 was split (decision 2026-07-10, §16):** 05a = the three album
    carousels now; 05b = the §6.1 resume card, deferred until queue
    persistence exists (Slices 10/11) — until then no persisted context can
    exist, so Home correctly renders no resume card (TODO marker in
    HomePage.xaml).
  - `HomePage` (routed from the shell's `home` tag): Most Played
    (`frequent`), Recently Added (`newest`), Random Picks (`random`) via
    `GetAlbumList2Async`, 20 items each, loaded in parallel. Album-only
    content per §6.2–§6.4.
  - `Controls/AlbumCarousel` (UserControl, one implementation for all rows):
    non-looping horizontal `ListView` (`ItemsStackPanel` horizontal —
    virtualizes, no wrap-around), 220 epx cards with cover art
    (`GetCoverArtUri(id, 440)`, `DecodePixelWidth` capped), music-note
    placeholder behind the image (same official Fluent codepoint as the
    Songs nav item), explicit loading / empty / error+Retry states driven by
    the host page. Cards open album detail since Slice 07a.
  - `AppSession.EnsureConnectedAsync()`: connect-on-first-use for the client
    restored at relaunch (shared in-flight task; failed attempts are
    replaced so Retry retries). Connection/auth failure on Home is a
    page-level error state with Retry (auth errors point at Account →
    sign out); per-carousel failures degrade independently (§3).

- **2.5a Slice 06a — Artists browse + artist detail display (implemented
  2026-07-10):**
  - **Slice 06 was split (decision 2026-07-10, §16), same rationale as 05:**
    06a = browse + detail display + favorite now; 06b = §8 Play/Shuffle
    (queue replacement + now-playing-bar reveal), deferred until the queue /
    now-playing infra exists (Slice 10). In 06a the Play/Shuffle buttons are
    present but disabled with a visible "playback arrives later" note (§15).
  - Client: `GetArtistsAsync` (getArtists, flattens the alphabetical
    `<index>` buckets into one ordered list), `GetArtistAsync` (getArtist →
    `ArtistDetail` = artist + albums; album count authoritative from the
    artist element, song count summed from albums), `StarAsync`/`UnstarAsync`
    (generic star/unstar by id) + `Artist`/`ArtistDetail` models. 9 new tests
    → **36 SubsonicClient tests green** (was 27).
  - App `ArtistsPage` (routed from the shell's `artists` tag): grid by
    default + list alternate + one always-visible grid/list `ToggleButton`
    (§13.5); people-glyph placeholder (same official Fluent codepoint as the
    Artists nav item); connect-on-use + page-level connect/auth error with
    Retry, loading/empty states; artist cards open detail.
  - App `ArtistDetailPage`: large artwork, name, favorite heart
    (`ic_fluent_heart_24_regular` U+F47A, state shown by accent-color fill
    since the Regular font ships no filled heart) persisted via star/unstar
    with optimistic toggle + revert-on-failure, album + song counts, lower
    album grid in the browse-card style (cards open album detail since
    Slice 07a), disabled Play/Shuffle + note, retryable error state.
  - New Fluent glyphs all verified against the shipped TTF's glyph table
    before use (§13.3, never invented): heart F47A, grid F463, list F4EE,
    play F606, shuffle EF37.

- **Setup-flow controller UX fix (committed `f5e1908`, console-verified
  2026-07-13):** Enter — which the console OSK's accept and the system
  Menu-button accelerator both produce — now advances Server address →
  Username → Password and submits only from the password field (the old
  handler submitted from any field, which mis-fired on the first OSK
  accept). On Xbox, `InputPane.TryShow()` re-opens the keyboard on the next
  field, so the whole form is typed without touching B or the D-pad;
  desktop keyboards are unaffected (device-family gate). Explicit TabIndex
  0–3. Rafael verified the flow on console: works as expected.

- **2.6 Slice 07a — Albums browse + album detail display (implemented
  2026-07-13):**
  - **Slice 07 was split (decision 2026-07-13, §16), same pattern as 05/06:**
    07a = browse + detail display + favorite now; 07b = §9 Play/Shuffle,
    current-track highlight, and jump-to-track after Slice 10.
  - Client: `AlbumListType.AlphabeticalByName`, `Album.IsStarred`, `Song` +
    `AlbumDetail` models, `GetAlbumAsync` (getAlbum; songs explicitly ordered
    disc→track; missing track sorts last within its disc).
  - App `AlbumsPage` (routed from the shell's `albums` tag): grid default +
    list alternate using the **shared `Controls/ViewModeToggle`** (extracted
    from ArtistsPage — the §13.5 "one consistent toggle" debt is paid);
    **incremental A→Z paging** via `Models/IncrementalSource<T>`
    (ISupportIncrementalLoading, 100/page; first page awaited by the page so
    loading/empty/error states stay explicit; a failed later page surfaces
    the page-level error with Retry, which reloads from the top).
  - App `AlbumDetailPage`: mirrors ArtistDetailPage — large artwork, title,
    artist, year, favorite heart (star/unstar with optimistic toggle +
    revert), song count + total duration (computed from the returned songs),
    disc/track-ordered track list (rows focusable but inert until 07b),
    disabled Play/Shuffle + note, retryable error state.
  - **Album cards are live everywhere:** Home carousels (new `AlbumClicked`
    event on `AlbumCarousel`), artist detail album grid, and Albums browse
    all navigate to album detail.
  - **B-button back navigation is now wired in the shell** (deep hierarchy
    made it necessary): unhandled B → SystemNavigationManager.BackRequested →
    ContentFrame.GoBack; the nav pane highlight resyncs on back; setup stays
    unreachable (outer back stack cleared at connect).

- **2.7 Slice 08 — Songs browse (implemented 2026-07-13):**
  - **Decisions locked 2026-07-13 (§16/§17):** row primary action = immediate
    playback (A/Enter replaces the queue, plays from that song) — wired by
    Slice 10, rows inert with a visible note until then; secondary actions
    live in a Menu-button context menu ("Go to album" is live now). Data
    source = **search3 with an empty query** (OpenSubsonic full-library
    contract; verified live against Navidrome 0.62 incl. songOffset paging
    with stable, disjoint pages in server-defined order).
  - Client: `GetSongsAsync(size, offset)` (query="", songCount/songOffset,
    artistCount=albumCount=0 → `<searchResult3>` songs).
  - App `SongsPage` (routed from the shell's `songs` tag): **list-only**
    (§7.3, no view toggle by design) with Title / Artist / Album / Length /
    Format / Bitrate columns; same incremental-loading + state pattern as
    Albums; **non-OpenSubsonic servers get an explicit "not supported" state
    (no Retry)** instead of a misleading empty list (§3).

- **2.8 Slice 09 — Playlists browse + composite artwork (implemented
  2026-07-13):**
  - **Playlist editing scope resolved: read-only for v1** (§16/§17). No
    create/edit/delete/reorder. Playlist tiles are focusable but **inert** —
    opening/playing a playlist depends on the Slice 10 queue infra, so a
    detail/playback view lands with a later slice; the page carries a visible
    "opening a playlist and playback arrive in a later update" note (§15).
  - Client: `Playlist` + `PlaylistDetail` models, `GetPlaylistsAsync`
    (getPlaylists → summaries) and `GetPlaylistAsync` (getPlaylist; song
    `<entry>` children mapped in stored order).
  - Core: `Playlists/PlaylistArtwork.SelectCoverArtIds` — the §7.4 composition
    rule as a pure, unit-tested helper (up to 4 distinct album covers, deduped
    by album with a cover-id fallback, single cover when one album, artless →
    empty). Lives in Core so it needs no UWP host. 6 new Core tests.
  - App `PlaylistsPage` (routed from the shell's `playlists` tag — previously
    fell through to the SectionPage placeholder): grid default + list alternate
    using the shared `ViewModeToggle` (§13.5); same connect-on-use +
    loading/empty/error-with-Retry pattern as the other browse pages. Not
    incremental — getPlaylists returns the full (small) set in one call, so it
    follows the ArtistsPage load-all shape.
  - **Composite artwork is client-side and lazy** (§7.4): getPlaylists carries
    no track list, so each tile fetches its own getPlaylist **in parallel after
    the grid is on screen** and fills in when ready. `PlaylistCard` is the
    project's first observable display model (INotifyPropertyChanged) so tiles
    update post-paint. Single-album playlists render one full cover; multi-album
    render an **adaptive composite with no empty cells** (2 covers =
    full-height halves side by side, 3 = one full-height + two stacked, 4 =
    2×2 — via x:Bind-driven Grid.RowSpan; reworked after Rafael's VM
    screenshots showed the 2-cover case wasting the bottom half). Covers
    render as **ImageBrush fills so partial cells center-crop** — Image's
    UniformToFill crops from the top-left, which showed the left edge of the
    art instead of its middle (second round of 2026-07-14 VM feedback); while
    loading or artless the placeholder (same official Fluent codepoint as the
    Playlists nav item) shows through. A per-tile fetch failure degrades to the
    placeholder — the page does not fail.
  - Live-verified: new `Playlists_ListAndFetchOne` live test passes against
    Navidrome (getPlaylists + getPlaylist round trip).

- **Console checkpoint #3 (2026-07-14): backlog items 1–18 run on device.**
  Rafael reported three findings, all controller-input class, all fixed the
  same day (fixes not yet console-verified):
  1. **Setup fields were uneditable by controller with the OSK dismissed**
     (D-pad only moves focus — no delete, no caret movement). Fix: inline
     gamepad editing on `SetupPage`, mirroring the Xbox OSK's own
     accelerators — **X = backspace, LB/RB = caret left/right** in the
     URL/username boxes; on the PasswordBox (masked, and PasswordBox exposes
     no caret/selection API) **X clears the field** for a clean retype. A
     static hint line under the fields documents the mapping (dynamic
     input-glyph swapping stays a later §13.1 item). Gamepad-only virtual
     keys, so no device-family gate — testable in the VM with the shared
     controller.
  2. **The Songs context menu never opened from the gamepad Menu button**
     (mouse right-click worked). Root cause: the `ContextFlyout` sits on the
     row template's inner grid, but the gamepad raises `ContextRequested` on
     the *focused* element — the ListViewItem, the grid's *parent* — and
     routed events bubble up, never down, so the flyout never saw it. Fix: a
     ListView-level `ContextRequested` handler redirects the request to the
     focused row's own flyout (pointer input still resolves at the grid
     first and marks it handled, so no double-open).
  3. **Account (footer) was unreachable by gamepad** — the OS
     NavigationView's `PaneFooter` is a separate XY focus region the gamepad
     cannot enter. **THREE interception attempts failed on console:**
     (a) explicit `XYFocusDown`/`Up` hints — never consulted; (b) a bubbling
     `KeyDown` handler with `handledEventsToo: true` — ran *after* the
     pane's list had already moved focus, misread positions (the erratic
     pane focus Rafael saw on re-test #2) and its `Focus()` calls were
     stomped by the list's in-flight move; (c) a `PreviewKeyDown` tunneling
     handler — still no Account access on re-test #3. **Final resolution
     (structural, not another hack): Settings and Account are ordinary menu
     items at the bottom of the MenuItems list, after a
     `NavigationViewItemSeparator`; `PaneFooter` is gone.** Focus,
     selection highlight, and A-invoke all work natively, and all footer
     machinery was deleted (manual Tapped/KeyDown invoke, hand-mirrored
     IsSelected, the hop handlers). Trade-off: they sit at the bottom of
     the list, not pinned to the pane's bottom edge (§16 decision
     2026-07-14; WinUI 2's FooterMenuItems is the documented upgrade path
     if pinning is ever wanted).

  **Re-test #1 (2026-07-14):** context-menu fix (2) verified working on
  console; footer attempt (a) failed. **Re-test #2:** attempt (b) failed +
  caused erratic pane focus (removed). **Re-test #3:** attempt (c) failed;
  also surfaced the systemic focus problem below. Setup editing fix (1)
  still untestable on console (signing out needs Account).

- **Focus architecture pass (2026-07-14, prompted by Rafael's re-test #3
  findings + explicit request to review the approach):** navigating to any
  page stranded the controller in the left nav, because Frame navigation
  destroys the old page's focused element and XAML falls back to the first
  focusable in the tree — the nav pane. Detail pages *tried* to counter
  this with a synchronous `FavoriteToggle.Focus(Programmatic)` in
  ShowContent, which silently failed (the content panel was un-collapsed in
  the same tick, so the target wasn't focusable yet — and Programmatic
  draws no focus visual anyway). **Convention now: content owns focus.**
  New `Services/ContentFocus.MoveInto(root)` defers one low-priority
  dispatcher pass (so layout has run and list containers exist), then
  focuses the first focusable element with `FocusState.Keyboard` (visible
  ring). Called from every page's content-reveal: the six async pages'
  ShowContent / load completion (Home, Artists, Albums, Songs, Playlists,
  both detail pages) and Settings/Account on Loaded. Error states already
  focused their Retry buttons (kept). Known accepted quirk: focus sits in
  the nav pane during the brief loading state, then moves into content —
  and a load completing while the user is deliberately browsing the pane
  will pull focus into the new content.

- **Album detail track rows now have the same Menu-button context menu as
  Songs rows (console feedback 2026-07-14, re-test #2):** disabled
  "Play (arrives in a later update)" + **"Go to artist"** (live — "Go to
  album" would be circular here; disabled when the server sends no artistId).
  Same console-verified redirect pattern: flyout in the row template for
  pointer input, ListView-level `ContextRequested` handler for the gamepad.
  `TrackRow` gained `ArtistId`/`HasArtistId`.

- **2.9 Slice 10 — playback queue + now-playing bar (implemented 2026-07-15),
  including the 06b/07b/Songs-row retrofits deferred onto it:**
  - **Design doc written before coding:**
    `docs/architecture/30-playback-architecture.md` (scope + 4 decisions
    confirmed with Rafael up front: engine = Core queue mirrored into a
    MediaPlaybackList; §6.1 persistence deferred to 05b; scrobbling in;
    Songs-row queue semantics).
  - Client: `GetStreamUri(songId)` (authenticated /rest/stream URL, no
    format/maxBitRate overrides in v1 — server player defaults decide) and
    `ScrobbleAsync(id, submission)`. 7 new tests → **66 SubsonicClient tests
    green** (was 59).
  - Core: `Playback/PlayQueue` (+ `RepeatMode`, `QueueContext`) — pure,
    injectable-Random-tested queue logic: replace-not-append (§10), full
    randomization on Shuffle actions (§8: the first track is random too),
    mid-play shuffle toggle pins the current track first / un-toggle restores
    the original order at the current track, repeat Off→All→One, index sync
    back from the player via `NotifyAdvanced`. 15 new tests → **44 Core tests
    green** (was 29).
  - **`PlaybackService` rewritten** (Slice 02 spike + TestTone.wav deleted):
    one app-lifetime MediaPlayer fed by a `MediaPlaybackList` mirror —
    gapless playback with next-track prefetch and native SMTC next/previous.
    List items always sit in original order; shuffle is expressed via
    `ShuffleEnabled` + `SetShuffledItems`, so toggling never interrupts the
    current track. Per-item SMTC metadata + cover thumbnail;
    `MaxPlayedItemsToKeepOpen = 2` (Xbox background memory, §11); repeat-one
    handled manually (end-of-stream MoveTo; MediaEnded path for the last
    track); `ItemFailed` skip with a consecutive-failure stop guard; scrobble
    fired at min(50 %, 4 min) from `PositionChanged` on the media thread
    (works while backgrounded), fire-and-forget, silent failures; Previous =
    restart-track when >5 s in (in-app AND SMTC, via a `PreviousReceived`
    intercept), else previous track; `Stop()` for sign-out. Service events
    marshal to the UI dispatcher captured at first use.
  - **`Controls/NowPlayingBar`** hosted in MainPage (NavigationView content is
    now a Grid: content `Frame` + an Auto bottom row) — its own layout row,
    so pages are never overlapped; "floating" is visual (margins + corner
    radius). §10 compact bar: 84-epx artwork (music-note placeholder), title,
    artist · album, format · bitrate, elapsed / thin progress / total, and
    shuffle (ToggleButton, checked visuals) · previous · play/pause (64-epx
    primary) · next · repeat · queue-expand. Slide-up + fade reveal on first
    Play; hides on Stop. **Repeat state is glyph + opacity only** (F172 all /
    EF34 one / dimmed when off) — never painted Foreground (favorite-heart
    console lesson). Elapsed display runs on a 500 ms DispatcherTimer only
    while playing. **Queue-expand is present but disabled until Slice 11**
    (visible-deferral pattern). New glyphs verified against the shipped TTF
    cmap: pause F5A2, previous F629, next F56A, repeat-all F172, repeat-one
    EF34 (queue button reuses list F4EE).
  - **Retrofits, all live now:** ArtistDetail Play/Shuffle (§8 pool =
    oldest-album-first via one getAlbum per album fetched in parallel — year
    ascending, unknown last, then name; buttons disable during the fetch;
    failures surface in a status line, §15); AlbumDetail Play/Shuffle (§9,
    album tracks only), track-row A/Enter = jump-to-track (queue = album at
    that track), context-menu "Play" live, **current-track highlight** (the
    number column swaps to an accent music-note marker; `TrackRow` is now
    observable); SongsPage rows play (queue = all rows loaded so far,
    positioned at the clicked song — §16 decision), context-menu "Play" live,
    header note now describes A/Menu instead of "arrives later"; sign-out
    calls `PlaybackService.Stop()` so no audio/queue survives to setup; the
    `XYFocusDown` pins on the favorite hearts are gone (Play/Shuffle below
    are focusable again).

- **Console checkpoint #4 (2026-07-15): Slice 10 verified on device,
  including the Release|x64 .NET Native run (finally done — plays music as
  expected).** Passed: album/artist/Songs playback entry points, jump-to-
  track + current-track marker, gapless album playback, background audio
  with real streams + SMTC metadata/art/controls, sign-out silence, and the
  failure path (playback stops after a queue's worth of failures). Four
  findings, all addressed the same day (fixes NOT yet console-verified):
  1. **Bar focus, Up:** from any bar control, Up landed in the nav pane
     instead of the page content — the XY engine preferred the pane's bottom
     items (vertically adjacent to the bar) over content further up. Fix: a
     `KeyDown` handler on the bar in MainPage constrains the Up search to
     the content frame via `FocusManager.FindNextElement(SearchRoot =
     ContentFrame)`; falls through to the default when the page has nothing
     focusable.
  2. **Bar focus, Left:** Left from a bar button jumped to the nav pane
     instead of the adjacent button. Fix: explicit `XYFocusLeft`/`XYFocusRight`
     chains pin every bar button to its neighbors (in-tree XYFocus hints are
     console-verified tech — the detail-page pins worked; only the
     NavigationView-internal ones didn't). Leftmost control still exits to
     the nav, by design.
  3. **Favorite heart on the bar** (feature request): added at the bar's
     right, before the queue button — same star/unstar + checked-visuals +
     revert-on-failure pattern as the detail pages (no room for a status
     line; the visible snap-back is the failure feedback). `Song.IsStarred`
     became settable so a successful toggle keeps the queue's loaded
     instance honest across track changes.
  4. **Scrobbling read as broken:** Navidrome's "Now Playing" panel showed
     nothing and Most Played didn't move. Diagnosed live from the Mac: a
     `scrobble?submission=true` with the app's exact URL shape increments
     `playCount` immediately — the endpoint and auth are fine. The panel
     Rafael checked is fed by `submission=false` ("now playing") reports,
     which the app never sent. Fix: the service now also fires a
     now-playing scrobble on every track start (the play-count submission
     still waits for the min(50 %, 4 min) threshold). Re-verify on console:
     Navidrome → Now Playing should show within seconds of starting a
     track, and the song's play count (song → info) should rise after the
     threshold.
  - Plus a navigation finding: pressing the current section (e.g. Artists)
    in the nav while inside a detail page did nothing — `SelectionChanged`
    never fires for an already-selected item. Fix: `ItemInvoked` handler
    re-navigates to the section's list page when the frame is showing
    something else (B-back still returns to the detail page — standard
    frame history).
  - SMTC note from the checkpoint (corrected — the icon strip visible at
    the screenshot's bottom edge was the app's OWN now-playing bar behind
    the guide overlay, not an OS surface): what the OS renders from the
    app's SMTC integration is the guide's media tile ("BoxSubMusic.App" +
    current track title) and its quick-action hints (X Pause, Y Skip
    forward) — both fed by our per-item display properties and
    CommandManager. The richer icon-based media card (as seen with
    Spotify) is the same OS surface one level deeper — expected via A /
    "More options" on that tile; presentation is OS-chosen, nothing known
    to implement app-side. TO VERIFY on the next console pass: selecting
    the tile opens the full media card, and it shows our cover art.

- **Checkpoint #4 re-test + second feedback round (2026-07-15 evening):**
  scrobbling, bar Up/Left focus, bar favorite heart, and nav re-click all
  **verified on console**. Two new requests, implemented the same day:
  1. **Favorite heart visual redesign (everywhere):** the starred state now
     lives in the glyph — an accent-filled heart over a slightly larger
     black filled heart (reads as a thick black rim), unstarred stays the
     outline heart in theme foreground. The old accent-flooded ToggleButton
     chrome is silenced via per-instance lightweight styling. Implemented
     as the shared `Controls/FavoriteHeart` (a ToggleButton subclass, so
     focus/XYFocus/IsChecked/events stay native — no wrapper-control focus
     risk), used by artist detail, album detail, and the bar. This required
     shipping the icon set's **Filled font variant**
     (`Assets/Fonts/FluentSystemIcons-Filled.ttf`, same MIT license; filled
     heart U+F47E verified against its cmap) — the Regular font has no
     filled glyphs (the 2026-07-14 console finding that forced the old
     chrome-based treatment in the first place).
  2. **Starred markers in song lists:** Songs rows and album track rows
     show a small filled accent heart next to starred songs (dedicated
     28-epx column after the title). Snapshot from load; pages refetch on
     navigation. Playlist song lists will get the same treatment when the
     playlist detail view lands.
  3. **Browse pages, Up-to-toggle (bug):** Up from the grid/list content
     jumped to the nav pane instead of the view-mode toggle. Fix:
     `XYFocusUp` hints on all six view elements pinned to the toggle's
     inner button (new `ViewModeToggle.FocusTarget` — hints must target a
     focusable element, not the UserControl wrapper).

- **Third feedback round (2026-07-15 night):** heart redesign, browse
  Up-to-toggle, and the guide media card (A on the media tile shows the
  full OS control card) all **verified on console**. One request:
  **favorite toggle in the song context menus** — both menus (Songs rows,
  album track rows) gained an "Add to favorites"/"Remove from favorites"
  item whose label tracks the state; a successful star/unstar repaints the
  row's heart marker in place (TrackRow/SongRow raise change
  notifications; the album's loaded Song instances are updated too so the
  queue/bar read the new state). The row state changes only after the
  server accepted the call — a failure leaves everything untouched (the
  menu is closed by then, so there is no status line; nothing shown ever
  lies).

- **2.10 Slice 11 — expanded queue (implemented 2026-07-16):**
  - **Design decisions confirmed with Rafael up front** (recorded in §16 and
    the design doc's new Slice 11 section): full-screen overlay over the
    whole shell; queue list = the whole queue in current play order (jump
    backwards allowed); rows are jump-only (no context menu this slice);
    Y toggles the overlay globally whenever a queue exists.
  - `Controls/ExpandedQueueView` (§10 expanded state): slide-up/down
    storyboards (300/250 ms, translate + fade); left pane = 400-epx artwork,
    title, artist · album, format · bitrate, elapsed/progress/total, and the
    full transport cluster (shuffle · previous · play/pause · next · repeat ·
    favorite — same handlers and XYFocus-pinned neighbors as the bar; the
    favorite keeps the silent snap-back failure mode and repaints the
    matching queue row in place); right pane = "Queue" header (context name +
    song count from `QueueContext`) over the row list in the album-track
    visual language (position column ↔ accent now-playing marker, starred
    heart, artist, duration). New `Models/QueueRow` (observable, TrackRow
    pattern, holds its `Song` as the identity-based jump target). Rows
    rebuild only when the play-order sequence actually changed
    (replace/shuffle toggle, detected by reference-comparing a snapshot);
    plain advancement repaints markers so scroll + focus survive track
    changes. On open the current row is scrolled into view and focused
    (deferred low-priority passes, ContentFocus-style retries).
  - `PlaybackService.JumpTo(Song)`: `MoveTo` on the live MediaPlaybackList —
    no rebuild, so shuffle order/repeat/prefetch survive; jumping to the
    current track restarts it. MoveTo's index space is **undocumented under
    shuffle** (Items vs ShuffledItems), so JumpTo indexes the shuffled order
    first, verifies MoveTo's returned item, and corrects once via the Items
    index on mismatch — deterministic either way, but the shuffle-on jump is
    an explicit console-verify item.
  - Shell wiring (`MainPage`): root became a Grid (NavigationView + overlay
    layer). **Focus trap = NavView.IsEnabled=false while open** (PaneFooter
    lesson: don't trust the XY engine to skip what merely looks covered);
    on `Closed` the shell re-enables and focus returns to the bar's queue
    button (or into the content if the queue died while open, e.g. the
    consecutive-failure stop — the overlay closes itself then). **Y toggles
    anywhere in the shell** (bubbling KeyDown on the root Grid); B closes
    (BackRequested, before frame history); Escape is the keyboard fallback;
    a chevron button (F2A4, cmap-verified) covers mouse users (§13.1).
  - Bar: queue button now enabled → raises `QueueExpandRequested`; tooltip
    "Queue (Y)".

- **2.11 Slice 12 — Settings: theme selector (implemented 2026-07-16):**
  - The other half of §12 (nav-visibility toggles + Search/Home protection)
    has existed since Slice 04 — this slice adds the theme selector and
    closes the section.
  - `Services/ThemeService` (+`ThemeMode` enum): preference persisted in
    LocalSettings (`theme.mode`, same pattern as `NavigationPreferences`,
    default Automatic). The theme is applied as **`RequestedTheme` on the
    root Frame** — `Application.RequestedTheme` cannot change at runtime
    (throws), and the root-frame value cascades to every page (setup
    included) and to flyouts/tooltips via their placement targets.
    Initialized in `App.OnLaunched` right after the frame is created, so
    the first rendered frame already has the chosen theme.
  - **Automatic resolves the system theme explicitly** (never
    `ElementTheme.Default`, which snapshots at launch): via
    `UISettings.GetColorValue(UIColorType.Background)` (black = dark) and
    re-resolves on `UISettings.ColorValuesChanged`, so a system theme
    change re-themes the running app. Two documented platform gotchas
    handled: the event fires **off the UI thread** (dispatched via the
    root's dispatcher) and the `UISettings` instance is held in a static
    field (a local one is GC'd and the event silently stops firing).
    Whether the event fires on the console when the Xbox theme changes
    mid-session is **not documented anywhere — explicit console-check
    item**; the launch-time snapshot is the guaranteed floor.
  - `SettingsPage`: "Theme" section with three **RadioButtons** (deliberate
    over a ComboBox: no popup focus scope, every option visible and one
    A-press away at 10 ft), instant apply on selection (the console's own
    theme setting behaves the same), hint line "Automatic follows the Xbox
    system theme." The existing XYFocus chain extends through the radios
    (PlaylistsToggle ↔ Automatic ↔ Dark ↔ Light, bottom edge pinned).
  - **No theming code-fix pass was needed:** an audit found every surface
    already on `ThemeResource` brushes; the only hardcoded color in the app
    is the FavoriteHeart's black rim, which is intentional cross-theme
    design (verify legibility in light theme on console).

- **2.12 Slice 05b — queue persistence + Home resume card (implemented
  2026-07-16):**
  - **Design recorded before coding** (30-playback-architecture.md, Slice
    05b section) and the one §6.1 ambiguity confirmed with Rafael: **the
    card's Play starts playback immediately** at the saved track + position;
    launch itself restores nothing, so "MUST NOT auto-play on launch" holds
    by construction (the literal restore-paused reading was rejected).
  - Core: `Playback/ResumeState` — the §6.1 contract + its reflection-free
    XML shape (RecentServers pattern: logic/serialization in Core, file I/O
    in the app). All-or-nothing parse: a malformed song, out-of-range index
    or non-permutation play order rejects the whole state. Song ids +
    display metadata only — **stream URLs never persist** (they embed the
    auth token; §4). `PlayQueue.Restore(state)` rehydrates tracks / exact
    play order / index / shuffle / repeat / context atomically. 13 new
    tests → **57 Core tests green** (was 44).
  - App `Services/ResumeStore`: structure in `resume-state.xml`
    (LocalFolder, rewritten only on structural changes — replace, shuffle
    toggle, repeat change); fast-moving index/position as LocalSettings
    scalars (written on track change, pause, suspend, and ~10 s throttled
    from `PositionChanged` on the media thread), overriding the file's
    snapshot at load. The file carries the owning server URL + username
    (via new `CredentialStore.TryGetActiveIdentity`, no vault hit) and is
    validated against the active login at load — mismatch or corruption
    reads as absent and deletes the file.
  - `PlaybackService`: persistence hooks in Play/SetShuffle/CycleRepeat,
    advancement writes index+0, `NotifySuspending` (App.OnSuspending)
    flushes the exact position; `Stop()` clears all persisted state
    (sign-out correctness; the consecutive-failure stop clears too —
    consistent with the queue vanishing from the UI). New
    `Resume(ResumeState)`: `Queue.Restore` → the normal `RebuildList`
    mirror → **one-shot pending seek** consumed on `MediaPlayer.MediaOpened`
    (position is only settable once media opens), pinned to the intended
    track so a failed-and-skipped first item can't start its successor
    mid-song; non-seekable streams degrade to track start.
  - `HomePage`: the §6.1 card (160-epx artwork with the standard music-note
    placeholder, context display name, "Title · Artist" of the saved
    current track, play glyph; the whole card is the button). Shown only
    when the state loads for this server AND no queue is active this
    session; hides live via `PlaybackService.Changed` if playback starts
    while Home is visible, with focus moved into the carousels.

- **2.13 Playlist detail/playback view (implemented 2026-07-17 — the piece
  deferred out of Slice 09, unblocked since Slice 10):**
  - `PlaylistDetailPage` mirrors `AlbumDetailPage` (§9 as the layout
    template): header with 320-epx **client-side composite artwork** —
    reuses `Models/PlaylistCard` + `Core/Playlists/PlaylistArtwork` at
    header size; the songs arrive with getPlaylist, so the covers resolve
    with the page load (no lazy fetch, unlike the browse tiles) — playlist
    name, "By {owner}" when the server sends one, song count + total
    duration computed from the returned songs (summary attributes as the
    empty-list fallback). **No favorite heart by design:** the Subsonic
    star/unstar endpoint covers songs, albums, and artists only — there is
    no playlist form, so the control is absent (nothing silently degrades;
    it never existed for playlists).
  - **Play/Shuffle replace the queue** with the playlist's songs in stored
    order (§10 replace-not-append; Shuffle randomizes the first track too,
    §8 semantics). `QueueContext` = playlist name + the **first
    constituent album cover** (the single-image cut of the §7.4
    composition) so the resume card and expanded-queue header render
    meaningfully.
  - Song list in stored playlist order: position numbers, starred-heart
    markers, the accent current-track marker, A/Enter = jump-to-track
    (queue = this playlist positioned at that song), and the
    console-verified ListView `ContextRequested` redirect with the
    standard menu — Play, favorite toggle (change-only-after-success,
    repaints the row and mutates the loaded `Song` so the queue/bar stay
    honest), **Go to album** AND **Go to artist** (both non-circular here;
    disabled when the server omits the id). `TrackRow` gained
    `AlbumId`/`HasAlbumId` for this (album detail now passes its ids too,
    though its menu still omits the circular "Go to album").
  - `PlaylistsPage` tiles are **live** (grid + list ItemClick → detail with
    the playlist id); the "opening a playlist … later update" note is
    gone. Editing stays out of v1 (read-only decision unchanged).
  - Loading / empty ("This playlist has no songs." + visibly disabled
    Play/Shuffle, §15) / retryable error states; `ContentFocus` on reveal.

- **2.14 Console checkpoint #6 feedback round (2026-07-17 evening — fixes
  code-complete, NOT yet device-verified):**
  - **Expanded-queue Left-from-row fix (checkpoint finding, was backlog
    item 1):** Left from a queue row went to whatever transport control was
    vertically nearest (rows 1–2 fine, row 3 → shuffle, rows 4–5 →
    previous). Every row container's `XYFocusLeft` is now pinned to the
    play/pause button via `ContainerContentChanging` (in-tree XYFocus hints
    are console-verified tech; the hint must sit on the focusable
    ListViewItem, not the template content). The trap itself and full
    reachability PASSED — `NavView.IsEnabled=false` is console-verified.
  - **Settings radio alignment (checkpoint note on item 2):** the OS
    RadioButton template top-anchors the bullet in a fixed 32-epx band
    (center y=16) and pads content `8,6,0,0` Top-aligned — tuned for the
    default 14-epx font, so the 20-epx labels sat ~4 epx low. Fix grounded
    in the 26100 SDK generic.xaml: `Padding="8,0,0,0"` +
    `VerticalContentAlignment="Center"` puts both centers at 16.
  - **"Play next" / "Add to queue" queue edits (feature request; decisions
    confirmed with Rafael, §16 + design-doc section):** on every song
    context menu (Songs rows, album track rows, playlist rows). Core:
    `PlayQueue.InsertNext` (at current+1 in play order; original order gets
    it at the same spot unshuffled / at the end under shuffle, so
    un-shuffling places it last; repeated inserts — most recent first) and
    `Append`; 8 new tests → **65 Core tests green**. Service:
    `PlayNext`/`AddToQueue` insert the `MediaPlaybackItem` into the live
    list (original-order index; never at/before the playing item), under
    shuffle re-express via `SetShuffledItems` (console-verified
    non-interrupting), persist structure; **empty queue → the song plays
    immediately as its own queue** (decision: no invisible state); an
    already-queued instance is cloned first (reference-identity maps).
  - **Context menus gained icons** (§13.3, cmap-verified in the shipped
    Regular TTF): play F606, play-next F56A, add-to-queue F7A8, heart F47A,
    album E010, person F5BE; separators group play / favorite / navigate.
  - **Controller hint lines (checkpoint request; decision: per-surface
    static text, the Songs-header pattern):** expanded queue ("Press A to
    jump to a song. Press Y or B to close the queue."), album + playlist
    detail song lists ("Press A to play… Press Menu for more options."),
    and the nav pane footer ("LB for this menu, RB for the content" / "Y
    for the queue during playback") — non-focusable text, so the 2026-07-14
    PaneFooter dead zone is irrelevant; hidden while the pane is collapsed
    (PaneOpening/PaneClosing).
  - **LB/RB quick nav (checkpoint request; decision 2026-07-17):** in the
    shell, LB focuses the nav pane's selected item (Home fallback when the
    section is hidden), RB moves focus into the page content
    (`ContentFocus`). Both inert while the queue overlay traps focus;
    key-repeat guarded.

- **2.15 Console checkpoint #7 feedback round (2026-07-18 — fixes/features
  code-complete, NOT yet device-verified):**
  - **Container queue edits** ("Play next"/"Add to queue" for whole
    albums, artists, and playlists — Rafael's request; detail-page
    placement confirmed): Core `PlayQueue.InsertNext/Append` gained
    **block overloads** (the block lands at current+1 in its internal
    order / at the end; original-order placement follows the single-song
    rules — same spot unshuffled, end under shuffle; empty queue → the
    block becomes the queue at index 0). 5 new tests → **70 Core tests
    green**. `PlaybackService.PlayNext/AddToQueue(IReadOnlyList<Song>,
    QueueContext)` mirror the block into the live list (per-song
    clone-on-collision incl. within-batch repeats; one SetShuffledItems +
    persist at the end); **empty queue → the container plays immediately
    from its first song** under its own context (no-invisible-state rule).
    New `Services/QueueActions`: fetch-on-demand helpers for album
    (getAlbum), playlist (getPlaylist, first-cover context), artist
    (getArtist + the §8 oldest-album-first getAlbum fan-out, now shared
    with ArtistDetailPage via `LoadArtistPoolAsync`); fetch failure =
    nothing happens (menu-closed contract, same as the menu favorite).
    **Surfaces:** card context menus (icons F56A/F7A8) on Albums browse
    grid+list, artist-detail album grid, Home carousels, Playlists
    grid+list, Artists grid+list — all via the console-verified
    ContextRequested redirect — plus **icon-only Play next / Add to queue
    buttons** next to Play/Shuffle on album, playlist, AND artist detail
    (artist detail reuses the pool fetch with all four buttons disabling
    during it).
  - **Monochrome controller-button badges** (checkpoint-#7 request:
    "Xbox-style icons, not colors"): new `Controls/ControllerGlyph` — an
    outlined 26-epx circle with the button letter (A/B/X/Y), a pill for
    LB/RB, the Fluent hamburger (F561, cmap-verified) in a circle for
    Menu; theme-foreground monochrome, non-focusable, dims with the
    parent hint's opacity. Not a second icon font (§13.3 intact). All
    static hint sentences replaced: Songs header, album + playlist detail
    lists, expanded queue, nav pane footer.
  - **LB/RB region cycling** (checkpoint-#7 request "quick option to
    reach the play bar"; decision 2026-07-18): RB cycles nav → content →
    bar → nav, LB the reverse; the bar is skipped while hidden; the
    focused region is detected by visual-tree ancestry; still inert while
    the queue overlay is open. `NowPlayingBar` gained
    `IsBarVisible`/`FocusPrimary()` (play/pause). From content, LB → nav
    and RB-with-no-bar → nav, so the console-verified checkpoint-#6
    behavior is a strict subset. Pane footer hint updated
    ("LB/RB Menu · content · playback", "Y Queue").
  - **TV-safe shell inset** (checkpoint-#7 finding: the nav pane was
    clipped by TV overscan — the 2026-07-14 edge-to-edge decision
    promised app-side padding "in Slice 14"; the bill arrived early):
    `ShellRoot` Padding **32,20,32,20**, sized so the pane's leftmost
    interactive pixel (item icons ~16 epx into the pane) lands at the
    48-epx TV-safe line and top/bottom clear 27 epx. The page background
    still paints the full canvas, so overscan crops only background;
    SetupPage already sat at 48/64 margins and needed nothing.

- **2.16 Console checkpoint #8 feedback round (2026-07-19 — nav pane
  visual refresh, code-complete, NOT yet device-verified):**
  - **Badge-letter centering (the one checkpoint-#8 note):**
    `ControllerGlyph` letters sat visibly low — the TextBlock's line box
    carries descender space below the caps, so centering the box seats
    capital letters low. `TextLineBounds="Tight"` centers the actual
    glyph bounds instead.
  - **Rounded floating nav pane (request: match the bar's radius):** the
    OS NavigationView paints its pane background as a hard rectangle, so
    the pane brushes (`NavigationViewDefaultPaneBackground` /
    `ExpandedPaneBackground`) are overridden to Transparent in App.xaml
    **theme dictionaries** — the one resource class app-side overrides
    reliably reach — and MainPage draws a `PaneBackdrop` Border behind
    the pane: radius 12 (the bar's), acrylic-element brush (the pane's
    former look), width following the pane state (280 open / 48 compact
    via the existing PaneOpening/PaneClosing handlers).
  - **Modern pane toggle (request: retire the hamburger):** the built-in
    toggle is hidden (its glyph was Segoe MDL2 — §13.3 violation all
    along) and replaced by an ordinary **first menu item** with the
    Fluent panel-contract/expand pair (E8B6/E8BA, cmap-verified; glyph
    swaps with pane state), `SelectsOnInvoked=False`, invoke toggles
    `IsPaneOpen`. A menu item — not a floated button, not PaneHeader —
    because ordinary items are the one pattern the gamepad focus engine
    is proven to handle (the 2026-07-14 PaneFooter lesson).
  - **Nav icons finally bigger (request: investigate deeper first; two
    prior on-device failures explained):** the left-pane item template
    renders the icon inside a **Viewbox** (`IconBox`) — a Viewbox scales
    its child, so `FontIcon.FontSize` was always a no-op (attempt #1) —
    and the box's size is 40 epx minus `NavigationViewItemIconBoxMargin`
    (10,12,16,12 → a ~16-epx icon), a Thickness defined at
    **generic.xaml's global scope**: resource references inside a system
    template resolve at the template's own parse scope first, so the
    App.xaml override was never consulted, on any device (attempt #2 —
    not a console quirk). Fix: `MainPage.EnlargeNavIcons` sizes each
    item's named `IconBox` directly after the template loads (28 epx in
    the 48-epx icon column, margin 10,6), deferred-retry like
    ContentFocus, re-run from ApplyNavVisibility for late-shown items.
    The dead App.xaml override was removed with a tombstone comment.

- **2.17 App rename + branding (2026-07-19 — code-complete, NOT yet
  device-verified):**
  - **Display name is now "SubMusic Box"** (§16 decision; supersedes the
    2026-07-07 BoxSubMusic naming). The rename is deliberately
    **display-layer only**: solution/project/namespace names, the
    PasswordVault resource (`BoxSubMusic.Server` — renaming it would
    orphan every stored credential), the package identity GUID, and the
    LocalSettings/LocalFolder shapes are all unchanged, so nothing is
    signed out or re-onboarded.
  - Manifest: `Properties/DisplayName` + `VisualElements DisplayName`
    (this is what the Xbox guide's media tile and My games & apps show),
    a real `Description`, and `BackgroundColor #1E1E1C` (sampled from the
    logo's background) on both the tiles and the splash screen.
  - **Assets regenerated from `logos/SubMusic Box - logo.png`** (1800²;
    Affinity source sits next to it): **every asset shows the complete
    logo, only scaled** — the first cut used an "S"-card crop for the
    square sizes, but the console pass showed it reading as a cut-off
    image; Rafael's call: always the whole logo (2026-07-19 feedback
    round). Wide tile and splash pad the square logo to aspect on
    #1E1E1C (seamless — the logo background IS that color). Generated
    with macOS `sips` (resize/pad); no new tooling.
  - **Console feedback round (same day):** (a) the crop→full-logo switch
    above; (b) the Xbox guide's app-list icon showed the OLD VS
    placeholder (the grey box-with-an-X) — that is the shell's icon
    cache serving the pre-branding asset, not a missing image; the
    package Identity Version was bumped **1.0.0.0 → 1.0.1.0** to force
    the shell to re-read the tiles on the next deploy. If the X
    persists after a versioned redeploy, uninstall the app on the
    console and F5 again (known Xbox shell tile-cache behavior).
  - `SetupPage` header: 88-epx logo image (`Assets/BrandLogo.png`, new
    csproj Content entry) beside the "SubMusic Box" title.
  - Subsonic client name (`c=` param): `BoxSubMusic` → `SubMusicBox`
    (server player lists will show a new player entry — cosmetic). The
    three URL tests now assert against `SubsonicApiClient.ClientName`
    instead of a literal, keeping tests name-agnostic.

- **2.18 Slice 13 — Search (implemented 2026-07-19 — code-complete, NOT
  yet device-verified):**
  - **All four §17 decisions locked with Rafael before coding** (recorded
    in §16): search3 endpoint, submit-triggered (no live-as-you-type),
    "All" view + scope filters, Songs-browse queue semantics for song
    results.
  - Client: `SearchAsync(query, artistCount, albumCount, songCount,
    offsets…)` + `SearchResult` model (artists/albums/songs mapped with
    the existing MapArtist/MapAlbum/MapSong). Rejects empty queries (the
    empty-query full-library contract stays GetSongsAsync's). 9 new tests
    → **75 SubsonicClient tests green** (was 66). **Live-verified against
    Navidrome**, which surfaced one behavior: Navidrome matches whole-word
    prefixes and **ignores single-character tokens** (a 1-char query
    returns nothing; 2+ chars work) — the app stays permissive (any
    non-empty query submits; a 1-char query truthfully shows "No
    results"), and the live test derives its query from a real song title.
  - App `SearchPage` (routed from the shell's `search` tag — the last
    SectionPage placeholder is out of the nav): query box (InputScope
    Search, submit on Enter/OSK accept, Search button for mouse) with the
    **SetupPage inline gamepad editing** (X backspace, LB/RB caret —
    deliberately outranks LB/RB region cycling while the box has focus;
    static badge hints in the pristine state); **scope filter row appears
    in the content area once a search ran** (§5): All / Artists / Albums /
    Songs segmented toggles, exclusivity in code. **All view** = one
    search3 request (6 artists / 6 albums / 10 songs), three sections in
    the browse visual language (horizontal card strips + the §7.3 song
    columns), empty sections collapse. **Scoped views** = full
    incrementally paged lists (fresh `IncrementalSource` per scope per
    query, 100/page via search3 offsets; scoped grids reuse the browse
    card templates). Artist/album results navigate to detail; song rows
    play (queue = that list's loaded rows at the chosen song, context
    "Search results"), with the full context menu — Play / Play next /
    Add to queue / favorite toggle / **Go to album AND Go to artist**
    (`SongRow` gained ArtistId/HasAlbumId/HasArtistId, read from the
    carried Song). Card menus carry the container queue edits. All
    console-verified patterns reused: ContextRequested redirect,
    XYFocusUp pins onto the filter row, ContentFocus on reveal (submit
    moves focus into results; scope switches don't steal it).
  - States: pristine hint, loading, per-scope empty ("No artists match
    …"), error + Retry (reloads the current scope from the top),
    **explicit unsupported state for pre-1.8 servers** (search3's
    introduction version; no Retry, §3).
  - **The page is navigation-cached** (`NavigationCacheMode.Required`) so
    B-back from a result's detail page returns to the same query and
    results — the one deliberate exception to refetch-on-navigation
    (staleness note below).
  - **Console checkpoint #11 (2026-07-19): all eight Slice 13 items
    verified by Rafael on device — the slice is closed.**

- **2.19 Slice 14 — resilience & polish (implemented 2026-07-19,
  console-verified at checkpoint #12 the same day; the last planned
  slice):**
  scope confirmed with Rafael up front (audits + no-silent-failure fixes
  + queue-row context menu; seek, dynamic input glyphs, and small visual
  polish deferred beyond v1). Design recorded in
  30-playback-architecture.md (Slice 14 section) + §16.
  - **Toast surface (§15 — closes the "most failure-prone silent path"):**
    new `Controls/ToastHost` (transient bottom-centered banner,
    ThemeResource brushes, fade in/out, 3.5 s dwell, newest message
    replaces the current one) hosted as the shell root's topmost layer —
    visible over the expanded queue, hit-test-invisible and never
    focusable, so the overlay focus trap is untouched — plus
    `Services/Toast` (static, any-thread, marshals to the UI dispatcher;
    a no-op before the shell registers, so setup keeps inline errors).
    Emitters: **queue-edit confirmations** in `PlaybackService.AddSongs`
    ("“Title” will play next" / "Name (12 songs) added to the queue";
    the empty-queue play-immediately path stays toast-free — bar reveal
    + audio are the feedback); **container-fetch failures** in
    `QueueActions` ("Couldn't load the album — nothing was added to the
    queue."); **per-track stream failures** in `OnItemFailed` ("Skipped
    “X” — the track could not be played.") and the consecutive-failure
    stop ("Playback stopped: no track in the queue could be played.
    Check the server connection."). A failure burst just replaces the
    toast each time.
  - **Queue-row context menu** (the Slice 11 deferral): expanded-queue
    rows carry the standard menu — Play (jump; pointer twin of A/Enter),
    favorite toggle (change-only-after-success; repaints every row
    holding the instance and syncs the left pane's heart when it is the
    current track), Go to album / Go to artist (disabled without ids) —
    via the console-verified ContextRequested redirect + §13.3 icons.
    "Go to" raises `AlbumRequested`/`ArtistRequested`; MainPage stashes
    the target, closes the overlay, re-enables the shell, THEN navigates
    (the `Closed` handler skips its queue-button focus restore for that
    pass, so the detail page's ContentFocus owns focus). `QueueRow`
    gained FavoriteLabel/HasAlbumId/HasArtistId; the overlay hint strip
    gained the Menu badge.
  - **Bounded fan-outs (performance audit):** new Core
    `Concurrency/BoundedParallel.MapAsync` (order-preserving, ≤N in
    flight, Task.WhenAll failure semantics; 6 new tests → **76 Core
    tests green**). The §8 artist getAlbum fan-out
    (`QueueActions.LoadArtistPoolAsync` — shared by artist detail
    Play/Shuffle and the card queue edits) and the Playlists page's
    per-tile getPlaylist fetches (SemaphoreSlim gate) both run at
    `QueueActions.MaxParallelFetches` = 4; tiles still fill in
    individually.
  - **Audits:** theming — zero hardcoded colors in XAML (the
    FavoriteHeart rim stays the one deliberate code-side constant);
    states/copy — inline page errors already consistent, guidelines
    codified in §16 and the §17 TODO closed; focus — nothing needed
    beyond the overlay-navigation handoff; memory — the §11 bounds
    (MaxPlayedItemsToKeepOpen=2, DecodePixelWidth caps) stand.
  - **Console checkpoint #12 (2026-07-19): all seven Slice 14 items
    verified by Rafael on device — the slice is closed.**
  - **Decode-failure mp3 fallback (added after checkpoint #12 — the
    §16 bitrate-restriction decision):** `SubsonicApiClient.GetStreamUri`
    gained optional `maxBitRate`/`format` parameters (3 new tests → **78
    SubsonicClient tests green**; the app itself still sends NO overrides
    by default — the server-side restriction is the authoritative quality
    control, Rafael's call, no client-side setting). `PlaybackService`:
    an `ItemFailed` with a decode-class error (DecodeError/
    SourceNotSupportedError — the signature of a server transcode format the
    Xbox lacks, e.g. Navidrome's Opus default) retries that track once
    in place with `format=mp3` (item swapped in the live list, MoveTo
    back, maps/shuffle order updated; not counted toward the
    consecutive-failure stop, no skip toast); after a fallback item
    audibly plays, the session builds further stream URLs as mp3
    directly. Network-class failures keep the normal skip/stop path.
    NOT yet device-verified — backlog items 1–4.

- **2.20 UI/UX refresh round (2026-07-19, third pass — code-complete,
  NOT device-verified):** Rafael's three requests, visual direction
  confirmed through a design Q&A (options A/B/C mockups; B chosen).
  - **"Play" on container card menus** (§10 semantics — replace the
    queue, play from the first song, identical to the detail-page Play):
    `QueueActions` refactored around a private `ContainerAction`
    (Play/PlayNext/Append) with new `PlayAlbumAsync`/`PlayPlaylistAsync`/
    `PlayArtistAsync`; failure toast "Couldn't load the … — playback
    didn't start." (menu-closed contract). Menu item (icon F606) added
    ABOVE Play next/Add to queue on every card surface: Artists/Albums/
    Playlists browse (grid+list), artist-detail album grid, Home
    carousels, Search artist+album cards.
  - **Browse-page initial focus lands below the view toggle:** the three
    toggle-carrying pages (Artists/Albums/Playlists) now pass the ACTIVE
    view (grid or list) to `ContentFocus.MoveInto` instead of the page,
    so first focus is the first card; the toggle stays one Up away
    (XYFocusUp pins unchanged).
  - **Retro theme:** App.xaml theme dictionaries carry the logo palette
    (dark: seafoam accent #7FADA9, cream #E9DCC0, petrol surface
    #253B3F, tan card frames; light: deep teal #2F6B66, aged tan,
    pale-seafoam surface). `SystemAccentColor` + the reachable control
    brush keys (SystemControlForeground/HighlightAccentBrush,
    ToggleButton checked incl. ink foregrounds, RadioButton checked —
    key names verified against the 26100 generic.xaml over SSH) are
    overridden per-theme; FavoriteHeart picks the override up through
    its existing SystemAccentColor read. Nav section icons painted
    RetroAccentBrush; PaneBackdrop + NowPlayingBar are RetroSurfaceBrush
    (bar border = tan frame); progress lines + play/pause fills are
    cream with per-theme ink (`RetroOnCreamBrush`).
  - **New controls:** `RetroStripes` (cream→seafoam triple underline;
    browse/Songs page titles, Home title + resume header, carousel
    titles, expanded-queue header), `VinylDisc` (Ellipse-drawn record,
    Viewbox-scaled; replaces every music-note ALBUM-artwork placeholder
    — carousel/browse cards, search album cards, artist-detail album
    grid, album-detail header, resume card, bar, expanded queue; artist
    "people" and playlist glyphs unchanged), `SpinningVinyl` (current-
    track marker in album detail, playlist detail, and queue rows —
    RenderTransform rotation storyboard, spins only while visible AND
    playing, freezes on pause, unsubscribes from
    PlaybackService.Changed on unload). **Same-day feedback fixes: (1) "the
    vinyl doesn't move" — a bare vinyl is rotationally symmetric, so
    its rotation was invisible at 22 epx; the animation also targets
    the documented independent property path with
    EnableDependentAnimation=True as a run-anyway guarantee. (2) The
    near-black record vanished on dark rows and hid the track number —
    the marker (only; static placeholders unchanged) now carries an
    accent ring + accent label + on-accent rotating stripe
    (RetroOnAccentBrush: ink on seafoam / pale on deep teal), sits at
    the number column's LEFT edge, and the number stays visible beside
    it (column widened 48→64 on the detail pages, 56→76 in the queue;
    the NumberVisibility/PositionVisibility model properties are no
    longer bound). (3) Nav icons, round two: centering
    the IconBox alone changed nothing — the template's ContentGrid is a
    40-epx-tall grid with a 48-epx icon column, so the old margin was
    already geometrically centered; the REAL offset is each glyph's
    metrics inside the FontIcon (the Viewbox scales the icon's whole
    text line box, bearings included — worst on the panel-toggle
    glyph). Fix: every nav FontIcon gets explicit square 24×24 bounds
    (FontIcon centers its glyph in its own box, so the Viewbox scales a
    true square), plus VerticalContentAlignment=Center in the item
    style so the label centering is explicit. (4) The marker was
    restyled again after the accent-badge cut stopped reading as a
    vinyl: it is now its own marker-tuned disc (dark record + soft
    cream rim + two brighter groove rings + small seafoam label with
    the rotating stripe + spindle hole), 26 epx, separate from the
    placeholder VinylDisc. Confirmed good by Rafael. (5) Nav icons,
    round three — the REAL causes, found by reading the 26100 SDK's
    `NavigationViewItemPresenter` template over SSH (round two's square
    FontIcon bounds was a real but insufficient fix, kept):
    **collapsed rail** — the template's icon column is HARDCODED to
    48 epx regardless of pane state, but `NavigationView.CompactPaneLength`
    was never set and defaults to 40 — an unrelated resource never kept
    in sync with the template's own column width, so every icon's true
    center sat 4 epx off the rail's actual center (uniform bias, reads
    as "unbalanced" rather than per-icon wobble). Fix: `CompactPaneLength="48"`
    on the NavigationView, matching the column exactly. **Expanded
    labels** — the item's text Content renders through an
    auto-generated TextBlock whose default line box carries descender
    space below the caps, the identical bug already fixed on
    `ControllerGlyph`'s button letters; `VerticalContentAlignment=Center`
    (round two) centers the line BOX against the icon but can't fix
    glyphs sitting low WITHIN that box. Fix: an explicit
    `NavigationViewItem.ContentTemplate` (`TextBlock` with
    `TextLineBounds="Tight"` + `VerticalAlignment="Center"`) on the item
    style.**
  - Browse card artwork (200/56), detail headers (320), and the
    expanded-queue artwork carry the 1-epx tan parchment frame.

**Verification state (2026-07-19, second pass):** checkpoint #12
closed the original Slice 14 round (**all seven backlog items
passed**); the same-day **decode-failure mp3 fallback is NOT yet
device-verified** (backlog items 1–4). Build green (x64 Debug over
SSH; APPX0703 packaging is the known command-line-only failure),
**76 Core + 78 SubsonicClient** tests green.

**Git state:** `main` holds everything through Slice 14 (merged via
PR #6 `7caa38b`; Slice 13 via PR #5, branding via PR #4 before it).
No open branches.

## Working mode (updated 2026-07-14: console checkpoints are BACK)

The Xbox is available for testing again (it was temporarily unavailable
2026-07-13→14, which produced the deferred backlog below). Back to
requirements §14 validation-throughout:

- **Milestone checkpoints:** when a slice (or coherent group of slices) lands,
  Claude MUST prompt Rafael to run the relevant console test items — do not
  silently defer them. Rafael drives F5 in VS (see "How to deploy" below).
- VM arm64 Local Machine F5 remains the pre-checkpoint smoke test, with its
  known blind spots: no Xbox mouse-mode class bugs, no
  Menu-button→ContextFlyout (console-only platform mapping — right-click
  stands in on desktop), no OSK, no real 10-ft/TV rendering, gamepad OK via
  Parallels sharing.
- New slices append their console items to the backlog below **and the next
  checkpoint prompt covers everything still unchecked there**.

**Bug-fix session closed 2026-07-15 (re-test #9): Settings toggle focus
chain verified; no edge clipping found. Nav icon size remains small after
two attempts and is DEFERRED to the refinement phase by Rafael's call
(low priority). Only the Release|x64 run is still outstanding as a
pre-Slice-10 checkpoint item.**

## Next step (start here next session)

**2026-07-19: checkpoint #12 closed Slice 14 and PR #6 is merged —
v1's planned scope (slices 00–14) is COMPLETE**, implemented and
console-verified end to end; the device backlog is empty.

There is no committed next slice; refinement-phase work has begun on
request. **Next console pass: the backlog below** — the decode-failure
mp3 fallback items (1–4) plus the 2026-07-19 UI/UX refresh round
(items 5–12, incl. the retro theme's light/dark 10-ft check and the
accent-brush override reach on the console OS build). Other candidates
remain: seek, dynamic input glyphs, transcoding-aware bitrate labels,
disc headers, Songs-column tuning, release-prep/packaging.

## On-device test backlog (next console pass)

**Checkpoints #3–#5 (2026-07-14 → 2026-07-16) and their re-tests are fully
closed**
(see git history / earlier revisions of this file for the item-by-item
detail). **Checkpoint #6 (2026-07-17) ran the accumulated items 1–17 —
Slice 11 focus trap, Slice 12 theme, Slice 05b resume, playlist detail —
and Rafael verified ALL of them** except two findings fixed the same day
(queue-row Left focus; radio label alignment — both in the new list below).
Notable closures: the `NavView.IsEnabled=false` focus trap works on
console; **Automatic theme live-follow WORKS on the Xbox** (the
`UISettings.ColorValuesChanged` open platform question is resolved: it
fires); the whole resume loop, playlist detail, and light theme passed.

**Checkpoints #7 (2026-07-18), #8 (2026-07-19), and #9 (2026-07-19) are
fully closed** — the TV-safe inset, all container queue edits, controller
badges, LB/RB region cycling, pane footer hints, badge-letter centering,
the rounded floating pane, the panel-contract pane toggle, and the
28-epx nav icons are all console-verified. The nav-icon-size item that
had been deferred since 2026-07-15 is CLOSED.

**Checkpoint #10 (2026-07-19) closed the 2.17 rename/branding round** —
full logo on every asset, guide app-list icon (fixed by the 1.0.1.0
versioned redeploy), seamless splash, and the `SubMusicBox` client name
in Navidrome all verified on console.

**Checkpoint #11 (2026-07-19) closed the Slice 13 Search round** — all
eight items verified on console (OSK submit flow, inline gamepad editing
with LB/RB precedence, All view + D-pad flow, scope filters with
incremental paging, result actions and context menus, navigation-cache
B-back, focus flow, empty/short-query states).

**Checkpoint #12 (2026-07-19) closed the Slice 14 round — all seven
items passed on device**: queue-edit confirmation toasts (song + container
forms, legible, focus untouched), toast visibility over the open expanded
queue, the container-fetch failure toast with the server unreachable,
per-track skip toasts + the failure-stop toast with the bar hiding, the
queue-row context menu (favorite sync incl. the left pane's heart,
Go to album/artist with overlay-close navigation and correct B-back),
acceptable large-artist start latency under the 4-request bound, and the
throttled playlist tile artwork filling in completely.

**Post-checkpoint-#12 addition (decode-failure mp3 fallback, §16
bitrate-restriction decision) — unverified on device:**

1. **Server-side bitrate restriction end-to-end:** set a max bitrate
   (no transcoding format, or a lossy one — NOT flac) on the
   **SubMusicBox [NSPlayer]** player entry in Navidrome, play something
   high-bitrate, and confirm the restriction is honored (Navidrome log
   shows "Transcode decision: transcode"; no "no compatible profile"
   warning).
2. **Ogg/Opus decode on Xbox — expectation: does NOT decode natively**
   (researched 2026-07-19: Microsoft's Xbox codec table lists no
   Opus/Ogg/Vorbis at all — the audio set is AAC/MP3/FLAC/ALAC/WAV/
   WMA/AC-3; Jellyfin's Xbox app has the same symptom, Opus tracks play
   silent, jellyfin-xbox#109). So with the restriction active the mp3
   fallback engaging is the EXPECTED path, not a question mark: the
   first track should hiccup once (brief silence/retry) and then play
   as mp3 at the capped bitrate; subsequent queues should play without
   the hiccup (session prefers mp3 after the first successful
   fallback). No "Skipped …" toast for the retried track; playback
   must not stop. If Opus somehow plays natively instead, that's fine
   too — the fallback stays dormant.
   *(Optional experiment while here: install the "Web Media
   Extensions" Store package on the console — an FFmpeg-based system
   codec provider that historically shipped for Xbox too and adds
   Ogg/Vorbis/Opus to UWP apps automatically — and re-test; if Opus
   then direct-plays, that's a zero-code quality win worth documenting
   for users.)*
3. **Delivered-stream labels (bar + expanded queue):** with the
   restriction active, the format/bitrate line must show what is
   actually playing (e.g. "OPUS", or "MP3 · <cap> kbps" once the
   fallback engages) instead of the file's "M4A · 256 kbps"; on an
   unrestricted server it should show the real codec (e.g. "AAC"
   for an m4a) with the file bitrate. The tag values may flash for
   an instant at track start (pre-open fallback) — acceptable.
4. **Network failures still behave:** with the restriction active and
   the server unreachable, the normal skip toasts + failure-stop must
   still occur (decode fallback must not swallow network errors).

**UI/UX refresh round (2026-07-19, third pass) — unverified on
device:**

5. **Card-menu "Play":** on an artist, album, and playlist card (browse
   pages, Home carousel, search results, artist-detail album grid) the
   new "Play" item replaces the queue and starts that container from
   its first song; bar reveals; large-artist Play shows the known
   pool-fetch delay before audio.
6. **Browse focus:** opening Artists/Albums/Playlists lands focus on
   the FIRST CARD (not the grid/list toggle); Up from the first row
   still reaches the toggle; the toggle still works.
7. **Retro accent reach:** hearts, starred markers, current-track
   markers, checked shuffle/view toggles, Settings radios, and the nav
   selection indicator all show the seafoam/teal accent — note ANY
   surface still showing the console's system accent color (the
   override's reach on the console OS build is the open question).
8. **Teal surfaces:** nav pane backdrop + now-playing bar read as the
   logo's petrol teal with legible text in BOTH themes (pane text
   contrast on teal is the dark-theme check; the pale-seafoam pane is
   the light-theme check). The pane backdrop lost its acrylic
   translucency by design (solid brush now).
9. **Stripe motif:** triple stripes under page titles, carousel
   titles, resume header, and the queue header — legible, not
   distracting, correctly colored in both themes.
10. **Vinyl placeholders:** artless albums show the vinyl disc at every
    size (cards, bar, 320/400 headers, resume card); loading tiles show
    it briefly before art paints.
11. **Spinning marker (CONFIRMED GOOD on device, 2026-07-19):** the
    current-track row in album detail, playlist detail, and the
    expanded queue shows a small vinyl-record disc with a rim, groove
    rings, and a seafoam label with a rotating stripe — clearly visible
    on the dark row background, spinning while playing, frozen on
    pause; the TRACK NUMBER stays visible next to it; scrolling a long
    list stays smooth (storyboards are gated to visible markers).
13. **Nav icon alignment (unverified):** FIRST confirm the icons are
    VISIBLE again (the fifth-pass ContentTemplate blanked them; reverted
    sixth pass). Then: compact strip — icons centered in the 48-epx rail
    (`CompactPaneLength="48"` fix, the real collapsed-rail cause);
    expanded pane — labels reasonably centered against their icons
    (`VerticalContentAlignment="Center"`; a residual descender offset is
    a known minor deferred item — do NOT reintroduce a bare
    ContentTemplate override, it hides the icons).
12. **Parchment frames:** the 1-epx tan border on cards/headers reads
    as intentional at 10 ft in both themes (not as a rendering
    artifact).

**Play-bar spinning vinyl artwork (2026-07-22) — CONSOLE-VERIFIED:**

14. **Now-playing-bar artwork is a spinning vinyl record** (new
    `Controls/SpinningAlbumArt`): the album art is the record LABEL in
    the centre of a vinyl disc (near-black body + rim highlight + three
    groove rings + spindle hole, Retro* brushes), and the whole disc
    ROTATES while the track plays (6 s/rev) and FREEZES in place on
    pause/stop. Same gating as the `SpinningVinyl` marker
    (PlaybackService.Changed + IsPlaying + own visibility). **Verified
    on device 2026-07-22:** the art rotates while playing and stops
    cleanly on pause, the artless blank-label case, 10-ft readability,
    and smooth art swap on track change all pass. (PR #8.)

**Nav/play-bar shortcut + alignment round (2026-07-22, code-complete,
NOT device-verified):** Rafael's two requests. Build green (x64 Debug
over SSH; APPX0703 is the known command-line-only packaging failure).

15. **Pane/bar bottom alignment:** the now-playing bar floats with a
    16-epx bottom margin but the pane backdrop stretched to the full
    content-area bottom, so the two floating cards ended 16 epx apart
    (read as unbalanced). `PaneBackdrop` now carries `Margin="0,0,0,16"`
    so both bottoms line up; the footer hint's bottom margin was raised
    to 28 to clear the shortened backdrop's rounded corner. Verify the
    pane and bar bottoms align and the footer hint isn't clipped.
16. **Dedicated View jump-key to the play bar:** the **View button**
    toggles focus onto the bar's play/pause and back to where the user
    was (a second press returns). No-op when nothing is playing. A
    subtle non-focusable **[View-icon] Now playing** badge sits in the
    bar's top-right corner as the on-bar indication. The View badge is a
    real drawn glyph — two overlapping panes, matching the physical
    button (added to `ControllerGlyph` 2026-07-22; the earlier build
    showed the literal word "View" because only "Menu" had a drawn glyph,
    everything else fell back to text). Verify: View from the nav/content
    jumps to the bar in one press; View again returns focus; the two-panes
    icon reads correctly and the badge doesn't crowd the metadata/time.
    Also: a small **(Y)** key badge is now overlaid on the bottom-right
    corner of the queue button (hit-test-invisible, Viewbox-scaled) so the
    global queue shortcut is discoverable on the control itself — verify
    it reads at 10 ft, sits clear of the View label above, and doesn't
    obscure the queue glyph or block the button.
17. **LB/RB are now direct region jumps, not cycling:** LB → nav pane,
    RB → page content (single-target; the 3-way cycle existed only to
    reach the bar, which View now covers). The nav footer was slimmed to
    a single **[LB] Menu** hint (the "direct to left nav" shortcut);
    the old two-row LB/RB/Y hint block is gone. Verify LB always lands
    on the nav pane and RB on the content from any region, and the
    slimmed footer no longer looks cramped. (Y still toggles the queue
    globally; its former footer hint was dropped — the bar's queue
    button tooltip still reads "Queue (Y)".)

*(Future work: append console items here.)*

## Known gaps / notes from this session

- **Retro theme notes (2026-07-19 third pass):** the palette lives in
  App.xaml's theme dictionaries as `Retro*` keys plus overrides of
  `SystemAccentColor` and the reachable control brush keys (ToggleButton
  checked, RadioButton checked, the two SystemControl accent brushes —
  names verified against the 26100 generic.xaml). Framework brushes
  composed from SystemAccentColor at generic.xaml scope may NOT follow
  the override (the known global-scope lesson) — any surface still
  showing the console accent on device gets its own brush-key override
  next round. The "zero hardcoded colors in XAML" audit stance is
  unchanged: every new color is a ThemeResource with dark+light values.
  `FavoriteHeart`'s code read of SystemAccentColor resolves through the
  app override but not per-runtime-theme (it reads the Default
  dictionary's value); the seafoam fill + black rim is legible on both
  themes, accepted. The pane backdrop is now a solid brush (acrylic
  gone by design). The spinning marker is a RenderTransform storyboard
  (independent animation), gated to visible + playing; if the console
  feel-test shows any cost, the gate can tighten further.
- **Container "Play" (2026-07-19):** card-menu Play fetches on demand
  like the queue edits (artist = the bounded §8 fan-out, so a large
  artist has a visible menu-click→audio delay); its fetch-failure toast
  says playback didn't start. Play from a card replaces the queue
  WITHOUT confirmation — §10 standard behavior, same as detail-page
  Play.

- **Playback is in (Slices 10–11 + 05b + playlist detail) — remaining
  playback gap:** no seek from either playback surface (not a §10
  requirement; the progress lines are display-only; SMTC/system seek
  still works where the system offers it).
- **Resume notes (Slice 05b):** the persisted position is ~10 s coarse
  during playback (exact on pause/suspend/track change) — a crash loses at
  most that window. The resume card's starred/metadata snapshot is as old
  as the last structural save; the queue refetches nothing at resume, so a
  song deleted server-side since then fails at stream time and is skipped
  by the normal ItemFailed path. `savePlayQueue`/`getPlayQueue` server
  sync stays out of scope (§6.1), but `ResumeState` carries everything it
  would need. The card intentionally shows only when nothing is playing
  this session. Suspend-position flush relies on OnSuspending actually
  firing — true for normal Xbox app lifecycle; a hard kill from the OS UI
  without suspension falls back to the 10 s scalars.
- **Theme notes (Slice 12):** Automatic's live-follow is **console-verified
  (checkpoint #6, 2026-07-17): `UISettings.ColorValuesChanged` DOES fire on
  the Xbox** when the console theme changes — the open platform question is
  resolved, no fallback needed. The event also fires for accent-color
  changes (harmless: the handler just re-applies the same resolved theme).
  `FavoriteHeart` reads `SystemAccentColor` once at construction, so an
  accent change mid-session repaints hearts only after their page/control
  is rebuilt — pre-existing behavior, unchanged by this slice. Light-theme
  10-ft contrast passed checkpoint #6 (item 3).
- **Expanded-queue scope notes (Slice 11, updated by Slice 14):** queue
  rows now carry the standard context menu (Play/favorite/Go to
  album/Go to artist) — the jump-only limitation is closed. No queue-edit
  items in that menu by design (the row is already in the queue). The
  overlay shows the whole queue; with very large queues (a big artist Play)
  the ListView virtualizes, but rebuild-on-shuffle re-creates all row
  models — revisit only if the console feel-test stalls. While the overlay
  is open the disabled shell underneath briefly shows its disabled visuals
  during the 300 ms slide; accepted. "Go to album/artist" from a row leaves
  focus briefly nowhere during the closing slide before the detail page's
  ContentFocus lands; accepted (same class as the loading-state quirk).
- **JumpTo under shuffle self-corrects:** MoveTo's index space with
  ShuffleEnabled is undocumented, so a platform mismatch would produce one
  imperceptible wrong-item MoveTo before the corrective one. Jump-to-track
  verified at checkpoint #5; no wrong-track blip reported.
- **Playback surfaces show the DELIVERED stream since 2026-07-19** (the
  "labels describe the file's tags" gap is CLOSED for the bar + expanded
  queue): `PlaybackService.TryGetDeliveredStreamInfo` reads the playing
  item's audio-track encoding properties (codec + declared bitrate), so a
  server-restricted stream shows e.g. "OPUS" / "MP3 · 128 kbps" instead
  of the file's "M4A · 256 kbps" (Rafael's on-device finding). Tag values
  appear only for the moment before the stream opens; VBR streams that
  declare no bitrate show the codec alone. Browse/search lists keep tag
  metadata by design — a row describes the library file, not a future
  playback's server transcode.
- **Bitrate restriction investigation (2026-07-19, live against Navidrome)
  → decode-failure mp3 fallback implemented in Slice 14** (§16: the
  server-side limit is authoritative; NO client-side quality setting,
  Rafael's call). Rafael's server-side per-player max-bitrate restriction
  was silently ignored (raw stream, Navidrome warning "Transcode
  decision: no compatible profile … audio bitrate not supported"). Two
  causes found: Navidrome 0.62.0 ignored player MaxBitRate on the legacy
  /stream path (their issue #5583, fixed in 0.63.0 — the server has
  since been upgraded to 0.63.2 and max-bitrate-only restriction was
  verified working live); and on 0.63.2 a player transcoding format of
  **flac** (lossless target from a lossy source) still silently serves
  raw with that exact warning — use max bitrate alone or a lossy format,
  set on the **SubMusicBox [NSPlayer]** player entry (Navidrome keeps
  one entry per client name + user agent; [NSPlayer] is the one that
  streams). App-side (Slice 14): a server-transcoded stream the Xbox
  cannot decode (Opus is Navidrome's default downsampling format;
  researched 2026-07-19 — Opus/Ogg absent from Microsoft's Xbox codec
  table, so the fallback engaging is expected) is retried once with
  `format=mp3` — the server
  still applies its own bitrate cap, so the restriction stays respected
  on every Subsonic/OpenSubsonic server; after the first fallback item
  plays, the session builds stream URLs as mp3 directly. Network-class
  failures keep the normal skip/stop path. Console-verify items: does a
  restricted (Opus) stream play natively; if not, does the fallback
  engage and keep the queue playing.
- **Artist Play fan-out is bounded since Slice 14:** at most 4 getAlbum
  requests in flight (`BoundedParallel` in Core; the playlist-tile fan-out
  is gated the same way). A very large artist now trades a little start
  latency for not hammering the server — start latency confirmed
  acceptable on console at checkpoint #12.
- **A failed stream item is skipped WITH a toast since Slice 14** ("Skipped
  '…'"); the consecutive-failure stop announces itself too. During a
  failure burst the newest toast replaces the previous one — there is no
  toast queue/history by design.
- **Repeat-one + SMTC previous/next:** manual next in repeat-one advances
  (conventional); only natural track end repeats.
- The Songs-row queue is **the rows loaded so far** — reaching the end of
  the queue does not fetch further pages (§16 decision 2026-07-15).
- **The bar's favorite heart reverts silently on failure** (no room for a
  status line — the snap-back is the feedback; detail pages keep their
  worded status). The context-menu toggle changes state only AFTER the
  server accepts, so its failure mode is "nothing happened". Starring from
  any surface mutates the loaded `Song` instance (`IsStarred` is settable
  since checkpoint #4) and repaints its own row, but OTHER already-rendered
  surfaces don't repaint (e.g. the bar's heart won't flip mid-track when
  its song is starred from a list row); every page refetches on navigation
  and the bar re-reads on track change, so staleness cannot outlive the
  current view.
- **Queue-edit notes ("Play next"/"Add to queue", 2026-07-17/18):** a song
  added twice (or added while already in the queue) is a **cloned** `Song`
  instance, so starring one copy repaints only that copy's rows until the
  next refetch — the same accepted staleness class as the cross-surface
  heart note above. Under shuffle the original order receives edits at the
  END, so un-shuffling shows them last regardless of where they sat in the
  shuffled order (documented Core semantics, unit-tested; blocks keep
  their internal order). The edits are structural resume writes — a
  resumed queue carries them. Since Slice 14 every queue edit confirms
  itself with a toast ("… will play next" / "… added to the queue"); the
  expanded queue (Y) remains the way to inspect the result.
- **Container queue edits (2026-07-18, updated by Slice 14):** card-menu
  adds fetch on demand — album/playlist = one call, artist = getArtist +
  the getAlbum fan-out (bounded to 4 since Slice 14; large artists still
  mean a visible delay between menu click and the queue growing, now
  bridged by the eventual confirmation toast). A fetch failure after the
  menu closed now SHOWS a failure toast ("Couldn't load the … — nothing
  was added to the queue.") — the former "most failure-prone silent
  path" is closed. Detail-page buttons reuse the already-loaded songs
  (album/playlist) or the shared pool fetch with button-disable + status
  line (artist).
- **Hints are static badges** (`ControllerGlyph`, 2026-07-18): monochrome
  outlined button shapes + short labels, shown even for keyboard/mouse
  users. Dynamic input-glyph swapping stays the deferred §13.1 item. The
  nav pane's footer hint hides while the pane is collapsed (it would clip
  in the 48-epx strip).
- **TV-safe inset (2026-07-18):** the shell content sits inside
  `ShellRoot` Padding 32,20 (pane icons land at the 48-epx safe line;
  27-epx top/bottom cleared); backgrounds still paint edge-to-edge. On
  displays WITHOUT overscan (VM, monitors) the app now shows a 32/20-epx
  background border around the shell — intended trade-off of the §2
  own-padding approach. The expanded queue overlay lives inside the same
  inset (its background matches the page brush, so the band is seamless).
- **Nav pane visual refresh notes (2026-07-19):** the rounded backdrop's
  width SNAPS between 280 and 48 on the pane events while the
  NavigationView's own items animate — a brief mismatch during the slide
  is possible (console feel-check; a width animation is the refinement if
  it reads as jank). The pane toggle is an icon-only menu item (no label,
  Win11 convention) that never joins the selection model; the built-in
  toggle is hidden, which also removed the app's last Segoe MDL2 glyph.
  `EnlargeNavIcons` depends on the template's Viewbox being NAMED
  "IconBox" — true in the 26100 SDK template and stable across the
  presenter styles, but a future OS template rename would silently return
  icons to small (fails soft, nothing breaks). The old
  `NavigationViewItemIconBoxMargin` App.xaml override is gone (proven
  unreachable — generic.xaml global-scope resource).
- **The now-playing report (`submission=false`) fires on every track start**,
  fire-and-forget like the play-count submission; both are silent on
  failure.
- **Mid-scroll page failure loses the scroll position:** a failed incremental
  page shows the page-level error, and Retry reloads from the top. Acceptable
  v1 trade-off, documented in §16.
- **Songs order is server-defined** (search3 has no client sort); Navidrome
  returns a stable album/path-ish order, not alphabetical by title. Revisit
  if it feels wrong on device.
- **Songs column widths are a first guess** (Title */Artist 180/Album 180/
  Length 70/Format 70/Bitrate 90 epx) — tune after the 10-ft check.
- **Multi-disc albums render as one flat list** ordered disc→track; no disc
  headers yet.
- **Favorite state is carried by the ToggleButton's checked visuals** (accent
  background + checked-foreground outline heart), not by painting the glyph:
  the shipped Fluent Regular font has no filled heart, and both hand-painting
  attempts made the heart invisible on console (Foreground=null when
  unstarred; accent-on-accent when starred). The glyph always inherits.
- Carousel focus is plain XY navigation (no focus engagement); crossing a
  long carousel vertically means stepping through whichever card is below.
  Same applies to the new browse grids. Revisit after the console feel-test.
- Settings/Account are ordinary menu items since 2026-07-14 (PaneFooter was a
  gamepad focus dead zone); they participate in the normal selection model.
  They sit at the bottom of the list, not pinned to the pane's bottom edge —
  accepted §16 trade-off.
- The content-owns-focus convention steals focus when a page's initial load
  completes; if the user is deliberately browsing the nav pane while a slow
  page loads, focus will jump into the content. Accepted for v1 — loads are
  fast and the alternative (guards) adds complexity.
- **Search notes (Slice 13):** the page is navigation-cached (the one
  refetch-on-navigation exception), so starred markers on kept results can
  lag behind a star/unstar done elsewhere until the next search — the same
  accepted staleness class as the cross-surface heart notes. While the
  query box has focus, LB/RB move the caret instead of cycling shell
  regions (deliberate; everywhere else they still cycle). Navidrome
  ignores single-character search tokens (verified live), so a 1-char
  query truthfully shows "No results" — the app imposes no minimum length
  of its own. The scoped song list's play-queue is "rows loaded so far",
  Songs-browse style; the All view's is just the top 10. `SectionPage`
  still exists as the shell's fallback page type but no nav item routes to
  it anymore.
- **Playlists are read-only but fully browsable (Slice 09 + the 2026-07-17
  detail view):** tiles open the detail/playback view; create/edit/delete
  stay out of v1. Composite tile artwork still loads lazily per tile (one
  getPlaylist per playlist, run after the grid paints — bounded to 4 in
  flight since Slice 14); a per-tile fetch failure silently degrades to
  the placeholder (decorative artwork, not a §15 case).
  Opening a tile issues its own getPlaylist (no cache
  handoff from the tile fetch) — refetch-on-navigation is the app-wide
  freshness pattern.
- **Playlist detail notes (2026-07-17):** a playlist can contain the same
  song twice — the current-track highlight matches by song id, so BOTH
  rows show the marker while that song plays (jump itself is index-based
  and lands on the pressed row); accepted for v1. The playlist itself has
  no favorite heart because the Subsonic star/unstar endpoint has no
  playlist form — the control is absent by design, not a degradation.
  The `QueueContext`/resume-card artwork is the first constituent album
  cover, not the 2–4-cover composite (the card renders one image).
- **Composite artwork is a live XAML layout, not a rendered bitmap:** the tile
  is a 2-column grid of ImageBrush-filled Rectangles whose row spans adapt to
  the cover count (2 = side-by-side halves, 3 = full-height + stacked,
  4 = 2×2), so no cell is ever empty; ImageBrush (unlike Image) center-crops,
  so partial cells show the middle of the artwork. No client-generated single
  flattened image. Server-provided playlist coverArt is intentionally NOT
  used (§7.4 requires client-side composition). The 3-cover "one big + two
  stacked" layout was evaluated against vertical thirds (≈66 epx strips —
  illegible at 10 ft) and kept.
- Hiding the currently open section from Settings leaves its content shown
  until the next selection. Acceptable for now.
- Detail pages reached from a hidden-then-shown section, and section history
  under B-back, use the frame's natural back stack — section switches
  accumulate history (standard NavigationView behavior).
- The last Slice 02 spike remnants are gone: `PlaybackService` was rewritten
  for the real queue and `Assets/TestTone.wav` deleted. Background audio with
  the test tone was console-verified in checkpoint #1; real-stream background
  audio awaits checkpoint #4.
- `PlaybackService.Instance` must be first touched on the UI thread (it
  captures that dispatcher); every current touch point is a UI handler or
  page/control Loaded, so this is a convention, not a runtime check.

## Milestones (compressed history)

- Slices 00–02 + on-device checkpoint #1 done (build loop, controller focus,
  background playback + SMTC).
- **Toolchain pivot:** modern .NET + Native AOT UWP won't activate on Xbox
  (0x80070002) → legacy **.NET Native** toolchain; libraries netstandard2.0.
  See docs/architecture/20-project-structure.md and [[xbox-uwp-toolchain]].
- **Mouse-mode fix:** `RequiresPointerMode = WhenRequested` in App ctor —
  D-pad works on console (mouse mode never engages on desktop).
- **Local VM F5 loop:** Directory.Build.props redirects App bin/obj to VM
  local disk (share fails with DEP0700). VS re-reads it only on solution
  load — restart VS after touching it.
- Phase 2 decisions locked 2026-07-09: XML/XDocument transport (no reflection
  serializers), LocalSettings + LocalFolder persistence, resume scope
  (queue+index+position+shuffle/repeat+context, resumes paused), Navidrome as
  primary test server. 2026-07-10: Fluent UI System Icons (§16).
- 2026-07-13: setup-flow Enter-advance + OSK chaining (console-verified);
  Songs row action, search3-empty-query data source, and incremental browse
  paging locked (§16); Slice 07 split 07a/07b; B-back wired in the shell.
- 2026-07-13: playlist editing scope resolved **read-only** for v1 (§16/§17);
  Slice 09 Playlists browse + client-side composite artwork
  (`Core/Playlists/PlaylistArtwork`, lazy per-tile), tiles inert until Slice 10.
- 2026-07-15: **Slice 10** — Core `PlayQueue` mirrored into a
  MediaPlaybackList (gapless + native SMTC), stream/scrobble client
  endpoints, `NowPlayingBar` in the shell, 06b/07b/Songs-row playback wired.
  Design doc: docs/architecture/30-playback-architecture.md.
- 2026-07-16: **Slice 11** — `ExpandedQueueView` full-screen two-pane
  overlay (whole queue in play order, jump-to-track via
  `PlaybackService.JumpTo`, global Y toggle, B/Esc/chevron close, focus
  trapped by disabling the shell); bar queue button enabled. Decisions in
  §16 + the design doc's Slice 11 section.
- 2026-07-16: **Slice 12** — theme selector (Automatic/Dark/Light radios on
  Settings, instant apply, LocalSettings persistence) via `ThemeService`:
  root-frame `RequestedTheme` + `UISettings`-resolved Automatic with
  `ColorValuesChanged` live-follow. Completes §12 (nav toggles existed
  since Slice 04). Unverified on device — backlog items 2–7.
- 2026-07-16: **Slice 05b** — §6.1 resume: Core `ResumeState` (XML, no
  stream URLs) + `PlayQueue.Restore`; app `ResumeStore` (LocalFolder file +
  LocalSettings scalars, server-identity-validated);
  `PlaybackService.Resume` with pinned one-shot seek; Home resume card
  (Play = restore + play immediately, confirmed with Rafael). Unverified
  on device — backlog items 8–12.
- 2026-07-17: **Playlist detail/playback view** (deferred out of Slice 09)
  — `PlaylistDetailPage` mirroring album detail with composite header
  artwork, Play/Shuffle queue replacement, jump-to-track + highlight,
  starred markers, full row context menu (Go to album AND artist); tiles
  live; no playlist favorite heart (no star/unstar form for playlists).
  Console-verified at checkpoint #6.
- 2026-07-17 evening: **Console checkpoint #6** verified everything from
  Slices 11 (focus trap), 12 (theme — incl. `ColorValuesChanged` firing on
  Xbox), 05b (resume), and playlist detail; two findings + feature
  requests answered the same day: **queue edits "Play next"/"Add to
  queue"** (Core `InsertNext`/`Append` + live-list mirror + menu items
  with cmap-verified icons on all three song menus), expanded-queue
  Left-from-row pinned to play/pause, Settings radio label centering,
  per-surface controller hint lines, and **LB/RB quick nav** (§16
  decisions 2026-07-17). Console-verified at checkpoint #7 (2026-07-18).
- 2026-07-18: **Console checkpoint #7** verified the whole checkpoint-#6
  fix round; the same-day answer to its feedback: **container queue
  edits** (block `InsertNext`/`Append` in Core + service, `QueueActions`
  fetch helpers, card menus on all album/playlist/artist surfaces +
  detail-page icon buttons), **`ControllerGlyph` monochrome button
  badges** replacing every hint sentence, **LB/RB region cycling** (nav →
  content → play bar), and the **TV-safe shell inset** (32/20 —
  checkpoint-#7 overscan finding; §16 decisions 2026-07-18).
  Console-verified at checkpoint #8 (2026-07-19).
- 2026-07-19: **Console checkpoint #8** verified the whole checkpoint-#7
  round (one note: badge letters low). Same-day nav-pane visual refresh:
  badge `TextLineBounds=Tight`, **rounded floating pane** (transparent
  pane brushes + radius-12 backdrop), **Fluent panel-contract/expand
  toggle as a first menu item** (hamburger and the last Segoe MDL2 glyph
  retired), and **28-epx nav icons** after finally isolating the root
  cause (template Viewbox + generic.xaml global-scope margin resource —
  app overrides unreachable by design; fixed via named-Viewbox sizing in
  code). §16 decisions 2026-07-19. Unverified on device — backlog items
  1–5.
- 2026-07-19: **Checkpoints #9/#10** closed the nav-pane refresh and the
  2.17 rename/branding rounds (branding merged via PR #4). **Slice 13** —
  Search: `SubsonicApiClient.SearchAsync` (search3, all three scopes +
  offsets) and `SearchPage` (submit-triggered query with inline gamepad
  editing, "All" view top 6/6/10 + Artists/Albums/Songs filters with
  incremental scoped paging, Songs-browse queue semantics, navigation
  cache for B-back, pre-1.8 unsupported state). §16/§17 decisions locked
  with Rafael first; merged via PR #5. **Checkpoint #11 verified all
  eight Search items on console — every planned feature slice (00–13) is
  done; Slice 14 (resilience/polish) remains.**
- 2026-07-19: **Slice 14** (resilience/polish, the last planned slice;
  scope confirmed with Rafael) — app-wide toast surface
  (`Controls/ToastHost` + `Services/Toast`: queue-edit confirmations,
  container-fetch failure reports, per-track skip notices, the
  failure-stop notice), the expanded-queue row context menu (Play /
  favorite / Go to album / Go to artist with overlay-close navigation
  handoff), bounded server fan-outs (Core `BoundedParallel`, ≤4;
  6 new tests → 76 Core green), audits clean (theming/states/focus/
  memory), §16 error-copy guidelines recorded, §17 TODO closed. Design
  in 30-playback-architecture.md (Slice 14 section). PR #6.
  **Checkpoint #12 (same day) verified all seven Slice 14 items on
  console — every planned slice 00–14 is implemented and
  console-verified. PR #6 merged (`7caa38b`): v1's planned scope is
  COMPLETE.**

## How to deploy (quick reference)

- **Console:** VS in the VM → Solution Platform **x64** → BoxSubMusic.App
  startup → run target **Remote Machine** `10.0.0.28`, auth **Universal
  (Unencrypted)** → Xbox on Dev Home → **F5**.
- **Local (VM):** Solution Platform **arm64** → run target **Local Machine** →
  F5. Requires the Directory.Build.props output redirect (in repo); VS only
  picks it up on solution (re)load.
- Full detail + the abandoned AOT path in CLAUDE.local.md.

## Reused, still-valid infrastructure

Mac↔VM SSH (`ssh winvm`), path mapping (`\\Mac\Repositories\...` over SSH),
Device Portal (https://10.0.0.28:11443), arm64-native MSBuild for command-line
smoke builds (compiles fine; appx packaging always fails with APPX0703 — that
step is VS-only). Parallels ownership quirk: files created from Windows are
root-owned on the Mac — create sources on the Mac side; see CLAUDE.local.md.
