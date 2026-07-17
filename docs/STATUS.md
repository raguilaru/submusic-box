# Current Status

Last updated: 2026-07-19 (**Console checkpoint #8 verified ALL seven
checkpoint-#7 items** — TV-safe inset, container queue edits from cards
and detail pages incl. edge semantics, controller badges, LB/RB region
cycling, pane footer hints — with one note: badge letters sat low in
their outlines. This round, all code-complete and NOT yet
device-verified: the **badge-letter centering fix**
(TextLineBounds=Tight), the **rounded floating nav pane** (transparent
pane brushes + a radius-12 backdrop matching the bar), the **modern pane
toggle** (Fluent panel-contract/expand as a first menu item, replacing
the Segoe MDL2 hamburger), and **bigger nav icons at last** — root cause
found: the item template scales icons inside a Viewbox whose margin
resource lives at generic.xaml's GLOBAL scope, unreachable by app
overrides on any device; fixed by sizing the named `IconBox` Viewbox
from code (16 → 28 epx). Build green (compile; APPX0703 stays the known
command-line-only packaging failure), **70 Core + 66 SubsonicClient
tests green**. **Next session: prompt Rafael for a VM F5 smoke pass or
console re-test of the new backlog items 1–5; THEN start Slice 13
(Search — §17 paging decision first) or Slice 14.**)

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

**Verification state (checkpoint #8, 2026-07-19):** all seven
checkpoint-#7 items **verified by Rafael on console** (badges passed with
the centering note fixed above). The 2.16 round is the new unverified set
(backlog items 1–5 below). Build green (x64 Debug over SSH; APPX0703
packaging is the known command-line-only failure), **70 Core + 66
SubsonicClient** tests green.

**Git state:** `main` holds Slice 10 (`f571bb4`), the checkpoint-#4 fix
commits, Slice 11 (`87c5f28`), Slice 12 (`df15b3f`), Slice 05b
(`edc5fde`), the playlist detail view (`5fdb5db`), and the checkpoint-#6
round (`d22f038`) on top; the 2.15 checkpoint-#7 round is uncommitted as
of this update (see log).

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

**2026-07-19: checkpoint #8 closed the whole checkpoint-#7 backlog**; the
same-day 2.16 round (badge centering, rounded pane, new toggle, big nav
icons) is the new unverified set.

1. **Prompt Rafael to verify the checkpoint-#8 round** — backlog items 1–5
   below. The icon-size fix and the pane visuals are best judged on the
   console (10 ft, real theme brushes); a VM F5 pass covers function
   first (toggle invoke, backdrop width tracking, badge centering).
2. Then: **Slice 13** (Search; lock the §17 endpoint/paging decision
   first), **14** (resilience/polish; a possible queue-row context menu).
   The long-standing nav-icon-size refinement item closes with this
   round if item 3 passes on device.

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

**Checkpoint #7 (2026-07-18) and checkpoint #8 (2026-07-19) are fully
closed** — the TV-safe inset, all container queue edits (cards + detail
pages + edge semantics), the controller badges, LB/RB region cycling, and
the pane footer hints are console-verified; checkpoint #8's only note
(badge letters low) is fixed in the round below. The long-deferred **nav
pane icon size** is no longer deferred: its root cause is understood and
fixed in code (see 2.16) — item 3 below is its device verification.

**Checkpoint-#8 fix/feature round (2026-07-19 — NOT yet run on any device;
pane visuals and icon size are best judged on console at 10 ft):**

1. **Badge letter centering (re-test of the #8 note):** the A/B/Y/LB/RB
   letters sit optically centered in their outlines everywhere badges
   appear (Songs header, detail lists, expanded queue, pane footer).
2. **Rounded floating pane:** the nav pane renders as a rounded card
   (radius 12, same as the bar) in both themes; no square background
   peeks out behind the rounding; collapsing the pane (toggle item)
   shrinks the card to the 48-epx compact strip and back without
   visual artifacts; the expanded-queue overlay still covers everything.
3. **Nav icon size (the long-deferred item, root cause finally fixed):**
   the pane item icons — including Settings, Account, and the new toggle
   — render visibly larger (28 epx, ~75% bigger than before) in both
   pane states; hiding and re-showing a section from Settings keeps its
   icon large; nothing clips inside the item rows.
4. **Pane toggle item:** the first pane row shows the panel-contract
   glyph (no hamburger anywhere); A on it collapses the pane (glyph
   flips to panel-expand, icons-only strip remains navigable), A again
   expands; the item never shows as "selected"; focus stays sane through
   toggling; mouse click works too.
5. **Pane visual regression sweep:** selection highlight, focus ring,
   and the separator lines still render correctly on items over the new
   backdrop; the pane footer hints still show/hide with the pane; LB
   still lands on the selected item.

*(Future slices: append your console items here.)*

## Known gaps / notes from this session

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
- **Queue-edit notes ("Play next"/"Add to queue", 2026-07-17/18):** a song
  added twice (or added while already in the queue) is a **cloned** `Song`
  instance, so starring one copy repaints only that copy's rows until the
  next refetch — the same accepted staleness class as the cross-surface
  heart note above. Under shuffle the original order receives edits at the
  END, so un-shuffling shows them last regardless of where they sat in the
  shuffled order (documented Core semantics, unit-tested; blocks keep
  their internal order). The edits are structural resume writes — a
  resumed queue carries them. No "added to queue" toast/confirmation
  exists; the expanded queue (Y) is the way to see the result — a
  §15-style feedback refinement candidate if it feels mute on device.
- **Container queue edits (2026-07-18):** card-menu adds fetch on demand —
  album/playlist = one call, **artist = getArtist + one getAlbum per album
  in parallel** (the known §8 fan-out cost, now reachable from any artist
  card; large artists mean a visible delay between menu click and the
  queue growing). A fetch failure after the menu closed changes nothing
  and shows nothing (menu-closed contract) — with the fan-out this is the
  most failure-prone silent path in the app; refinement candidate
  together with the toast note above. Detail-page buttons reuse the
  already-loaded songs (album/playlist) or the shared pool fetch with
  button-disable + status line (artist).
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
- The remaining placeholder section page (Search) has no focusable content,
  so Right-into-content has no target there. Fine until that slice lands.
- **Playlists are read-only but fully browsable (Slice 09 + the 2026-07-17
  detail view):** tiles open the detail/playback view; create/edit/delete
  stay out of v1. Composite tile artwork still loads lazily per tile (one
  getPlaylist per playlist, run in parallel after the grid paints); a
  per-tile fetch failure silently degrades to the placeholder. With a
  large number of playlists this fans out to many parallel getPlaylist
  requests on page open — acceptable for v1, revisit if it stalls the
  shell on device. Opening a tile issues its own getPlaylist (no cache
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
