# Current Status

Last updated: 2026-07-16 (**Slice 12 implemented** — theme selector
(Automatic/Dark/Light, §12) on the Settings page, applied via the root
frame's `RequestedTheme`, Automatic follows the system theme live via
`UISettings.ColorValuesChanged`. Build + all 110 tests green. **The Xbox
is still unavailable**, so its console checks are queued in the backlog
(items 2–7) together with the Slice 11 focus-trap test (item 1); a VM F5
smoke pass can cover most of the theme behavior meanwhile. **Next console
pass: run backlog items 1–7. Next slice: 05b or playlist detail.**)

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

**Verification state (checkpoint #5, 2026-07-16):** the context-menu
favorite toggle, the row starred markers, and the expanded queue view
(open/close paths, jump-to-track, live updates) are **verified by Rafael**.
The **only open item is the overlay's focus trap** (backlog item 6 —
NavView.IsEnabled=false is a new trapping mechanism on console): the Xbox
is temporarily unavailable, so it carries to the next console pass. Build
green (x64 Debug over SSH; APPX0703 packaging is the known
command-line-only failure), **44 Core + 66 SubsonicClient** tests green.

**Slice 12 (2026-07-16) is code-complete but entirely unverified on
device** (Xbox still unavailable): its console checks are backlog items
2–7. Build + both test suites re-verified green after the slice. Most of
the theme behavior (radios, instant re-theme, persistence, Automatic
following the *Windows* theme live) is VM-F5-testable before the console
pass; the console adds the Xbox-theme integration and the 10-ft light-theme
readability check.

**Git state:** `main` holds Slice 10 (`f571bb4`), the checkpoint-#4 fix
commits, and Slice 11 on top (see log).

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

1. **When the Xbox is back: backlog items 1–7** — the Slice 11 focus-trap
   test plus all Slice 12 theme checks. A VM F5 pass on the theme items
   (2–5, 7 minus the Xbox-specific parts) is worthwhile earlier.
2. Then: **05b** (queue persistence + Home resume card — the model is
   ready, `QueueContext` is already carried), **playlist detail/playback
   view** (deferred out of 09, unblocked now), **13** (Search; §17 paging
   decision), **14** (resilience/polish incl. the deferred nav-icon-size
   refinement and a possible queue-row context menu).

## On-device test backlog (next console pass)

**Checkpoint #3 (2026-07-14) ran the previously accumulated items 1–18.**
Rafael reported three issues (fixed same day — see "Where we are"); no other
problems were reported. Still open:

**Console-verified 2026-07-14 (re-tests #4–#6):** nav restructure
(Settings/Account reachable), XYFocusDown hints (toggle/heart), in-content
navigation focus (card → detail both paths, Songs, B-back), favorite heart
renders, main-content sizing at 1:1 scale, Songs AND album-track context
menus. Still open:

**Also console-verified (re-tests #8–#9, 2026-07-15):** starred-heart
visuals, edge-to-edge rendering, no edge clipping, Settings toggle focus
chain (up/down walks the switches, Left reaches the nav). Still open:

1. ~~Release|x64 .NET Native run~~ — **DONE, console checkpoint #4
   2026-07-15** ("plays music as expected"). Debug/Release parity risk for
   the media stack is retired.
2. **DEFERRED to the refinement phase (Rafael's call 2026-07-15, low
   priority): nav pane icon size.** Two attempts failed on device: the
   WinUI-only `NavigationViewItemOnLeftIconBoxHeight` resource (absent
   from the OS generic.xaml — verified in the 26100 SDK) and the
   `NavigationViewItemIconBoxMargin` override (`10,6,16,6`, still in
   App.xaml — the template math said 40-epx row minus margin = icon
   height, but on-device the icons did not grow). Next candidates when
   picked back up: verify whether the override is being consulted at all
   on the console's OS build (the 26100 *SDK* template may not match the
   console's), or bypass the template entirely — retemplate
   NavigationViewItem, or drop the Icon property and put the FontIcon in
   the item Content.

**Checkpoint #4 (2026-07-15) ran all Slice 10 items** — everything passed
except the four findings fixed the same day. **Re-test (same day):
scrobbling, bar Up/Left focus, bar favorite heart, and nav re-click all
verified working on console.** Two new findings from that pass (heart visual
redesign + browse-page Up-to-toggle) were fixed the same day. Open items for
the next console pass:

**Console-verified 2026-07-15 (third round):** heart redesign on all three
surfaces, browse-page Up-to-toggle (grid + list, all three sections), guide
media card (A on the media tile shows the full OS control set). Still open:

**Checkpoint #5 (2026-07-16) ran items 3–8:** context-menu favorite toggle
(3), row starred markers (4), and the expanded queue view (5, 7, 8 —
open/close paths, jump-to-track, live updates) all **verified by Rafael**.
One item could not be run (Xbox temporarily unavailable) and stays open:

1. **Focus trap while the overlay is open** (was item 6): focus lands on
   the current track's queue row on open (visible ring); XY can reach every
   transport control and the queue list, and **cannot escape into the nav
   pane or the page behind** — especially Left from the leftmost transport
   buttons and Up from the top row. The trap is `NavView.IsEnabled=false`,
   a mechanism not yet exercised on console (the PaneFooter history is why
   this deserves an explicit check). Also confirm that after closing, focus
   returns to the bar's queue button.

**Slice 12 (2026-07-16, theme selector — NOT yet run anywhere on device;
items 2–5 and 7 are also VM-F5 smoke-testable, substituting the Windows
theme setting for the Xbox one):**

2. **Theme radios by controller:** on Settings, D-pad Down from the
   Playlists switch reaches Automatic → Dark → Light (bottom edge pinned,
   no straying into the nav pane); Up walks back through the toggles; Left
   exits to the nav; A checks a radio and the check state is readable at
   10 ft.
3. **Instant re-theme, all surfaces:** selecting Dark/Light re-themes
   immediately with no restart and no artifacts — nav pane, all browse
   pages (grid + list), both detail pages, Songs, Playlists tiles,
   now-playing bar, expanded queue overlay, context flyouts, tooltips.
   In **light theme** specifically check: focus ring visibility at 10 ft,
   the FavoriteHeart (starred: accent fill + black rim; unstarred: dark
   outline) on all three surfaces, row starred markers, and the
   now-playing bar's chrome-brush background contrast.
4. **Persistence:** pick Dark (then Light), quit the app fully, relaunch —
   the choice survives, and the very first rendered frame is already the
   chosen theme (no flash of the other theme).
5. **Automatic at launch:** with the mode on Automatic, the app matches
   the console's Settings → General → Personalization → "My color &
   theme" at launch, for both console values.
6. **Automatic live-follow (the open platform question):** with the app
   running in Automatic, change the Xbox theme — does the app re-theme
   without a restart? `UISettings.ColorValuesChanged` on Xbox is
   undocumented; if it never fires, the launch snapshot is the accepted
   fallback and the §16 entry + this doc must be updated to say so.
7. **Setup page in light theme:** sign out while in Light — the setup
   page renders themed (the root-frame mechanism covers it; confirm).

*(Future slices: append your console items here.)*

## Known gaps / notes from this session

- **Playback is in (Slices 10–11) — remaining playback gaps:** no queue
  persistence/resume (05b), no seek from either playback surface (not a §10
  requirement; the progress lines are display-only; SMTC/system seek still
  works where the system offers it), and playlists still have no
  detail/playback view.
- **Theme notes (Slice 12):** Automatic's live-follow depends on
  `UISettings.ColorValuesChanged` firing on the console for an Xbox theme
  change — undocumented, console-check item 6; the launch-time resolve is
  the guaranteed floor either way. The event also fires for accent-color
  changes (harmless: the handler just re-applies the same resolved theme).
  `FavoriteHeart` reads `SystemAccentColor` once at construction, so an
  accent change mid-session repaints hearts only after their page/control
  is rebuilt — pre-existing behavior, unchanged by this slice. Light theme
  has never been seen on a real TV: every surface uses `ThemeResource`
  brushes so it renders, but 10-ft contrast (focus rings, the black-rimmed
  heart, the bar's chrome background) is exactly what checkpoint item 3
  verifies.
- **Expanded-queue scope notes (Slice 11):** queue rows are jump-only — no
  context menu (favorite/go-to from a queue row are refinement-phase
  candidates; the established row-menu pattern drops in unchanged). The
  overlay shows the whole queue; with very large queues (a big artist Play)
  the ListView virtualizes, but rebuild-on-shuffle re-creates all row
  models — revisit only if the console feel-test stalls. While the overlay
  is open the disabled shell underneath briefly shows its disabled visuals
  during the 300 ms slide; accepted.
- **JumpTo under shuffle self-corrects:** MoveTo's index space with
  ShuffleEnabled is undocumented, so a platform mismatch would produce one
  imperceptible wrong-item MoveTo before the corrective one. Jump-to-track
  verified at checkpoint #5; no wrong-track blip reported.
- **Stream format/bitrate labels describe the file's tags, not the delivered
  stream:** v1 sends no transcoding overrides, so a server configured to
  transcode will play a different bitrate than the bar shows. Revisit with
  the OpenSubsonic `transcoding` extension in the refinement phase.
- **Artist Play fans out one getAlbum per album in parallel** (same shape as
  the playlist-tile artwork fan-out). Fine for typical artists; a 100-album
  artist will hammer the server and delay start — revisit throttling if the
  console feel-test stalls.
- **A failed stream item is skipped silently** (the MediaPlaybackList moves
  on); only a whole queue's worth of consecutive failures stops playback.
  Per-track error surfacing is a §3 refinement candidate.
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
- **Resume card (§6.1) intentionally absent** until Slice 05b (after queue
  persistence exists); acceptance criterion "shows resume card when
  persisted context exists" is vacuously satisfied — nothing can persist
  context yet.
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
- The remaining placeholder section page (Search) has no focusable content,
  so Right-into-content has no target there. Fine until that slice lands.
- **Playlists are read-only (Slice 09):** tiles are focusable but inert — the
  detail/playback view is now UNBLOCKED (Slice 10 queue exists) but not yet
  built; its note still reads "arrive in a later update", which stays true.
  Composite tile artwork loads lazily per tile (one getPlaylist
  per playlist, run in parallel after the grid paints); a per-tile fetch
  failure silently degrades to the placeholder. With a large number of
  playlists this fans out to many parallel getPlaylist requests on page open —
  acceptable for v1, revisit if it stalls the shell on device.
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
