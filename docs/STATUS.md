# Current Status

Last updated: 2026-07-13 (Slices 07a + 08 committed `d0cb046`; working mode changed: build all features first, one big console test pass at the end).

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

**Verification state:** x64 Debug compile green over SSH (APPX0703 at the
packaging step is the known command-line-only failure); 23 Core + **52
SubsonicClient** unit tests green in the VM incl. live Navidrome tests (which
now cover getAlbum, alphabetical paging, and empty-query search3 paging).
The setup flow (incl. checkpoint #2 item 1) is console-verified 2026-07-13.
Slices 07a/08 are VM-desktop-checked only; their console items are in the
deferred backlog below.

**Git state:** `main` holds the setup-flow fix (`f5e1908`) followed by the
Slice 07a + 08 commit (this one); working tree clean.

## Working mode (decided 2026-07-13)

The Xbox is temporarily unavailable for testing. Development continues
**Xbox-first in design** (controller-first, 10-ft, XY focus, per CLAUDE.md)
but WITHOUT per-slice console checkpoints: implement the remaining slices in
order, then run **one consolidated on-device test pass at the end** using the
backlog below. This deviates from requirements §14 ("validation throughout")
deliberately and temporarily — hardware constraint, not a process choice.

Rules for every slice implemented in this mode:
- VM arm64 Local Machine F5 remains the interim smoke test, with its known
  blind spots: no Xbox mouse-mode class bugs, no Menu-button→ContextFlyout
  (console-only platform mapping — right-click stands in on desktop), no OSK,
  no real 10-ft/TV rendering, gamepad OK via Parallels sharing.
- **Every new slice MUST append its console-specific test items to the
  backlog below** instead of scheduling a checkpoint.

## Next step: continue slicing (start here next session)

**Slice 09 (Playlists browse + composite artwork)** — resolve the §17
playlist editing-scope decision (read-only vs create/edit/delete) with Rafael
first. Then **Slice 10 (now-playing bar + queue)** — the big unblocker for
05b/06b/07b and the Songs row action — then 11 (expanded queue), 12 is done
in essence (theme selector still missing — check §12), 13 (Search; §17
paging decision), 05b/06b/07b wiring, 14 (resilience/polish).

## Deferred on-device test backlog (run when the console is back)

One consolidated pass: VS in VM, Solution Platform x64, Remote Machine
10.0.0.28, F5, Xbox in Dev Mode. Also fold in the **Release|x64 .NET Native
build validation** (per the build plan) — Debug and Release differ under
.NET Native, so at least one full Release run.

**Carried over from checkpoint #2 (2026-07-10 list; setup flow item verified
2026-07-13, the rest not itemized — re-verify quickly):**
1. LAN capability: connect to the Pi (`http://10.0.0.5:4533` if
   `raspberrypi.local` fails — mDNS from UWP on console unverified).
2. Credential persistence: quit + relaunch → straight to shell; Account
   shows server + username.
3. Shell: D-pad nav order, Fluent icons render, pane collapse/expand, footer
   Settings/Account respond to A, focus visible at 10 ft, both themes.
4. Nav toggles: hide/show Artists/Albums/Playlists, persist across relaunch;
   Search/Home always present.
5. Sign out → setup; B does not re-enter the shell; relaunch lands on setup.
6. Home (05a): carousels load with real albums; cover art renders
   (authenticated getCoverArt → BitmapImage — first console test); D-pad
   travel; error states with focused Retry.
7. Artists (06a): grid + artwork; grid/list toggle (now the shared control);
   detail artwork/name/counts; favorite heart toggles and persists
   (star/unstar write path); heart legibility at 10 ft (color-only state).

**From Slices 07a + 08 (2026-07-13):**
8. Albums browse: grid loads A→Z with artwork; **scroll across a 100-item
   page boundary** (first console test of incremental loading — watch for
   hitches/focus jumps as pages append); toggle flips modes; A opens detail.
9. Album detail: artwork, title, artist, year, counts + total duration;
   favorite heart persists across leave/re-enter; track list in disc/track
   order (use a multi-disc album if available); Play/Shuffle visibly
   disabled; track rows focusable but inert.
10. Card navigation: Home carousel card → album detail; artist detail album
    card → album detail; Albums browse card → album detail.
11. B-button back: walks back through detail pages and sections; nav
    highlight resyncs; B on a section root does NOT exit the app to setup;
    after sign-out B does not re-enter the shell.
12. Songs: all six columns readable at 10 ft (widths are a first guess —
    expect tuning); scroll across a page boundary; **Menu button on a row
    opens the context menu** (console-only platform mapping, unverifiable on
    desktop — confirmed working only via mouse right-click in the VM);
    "Go to album" navigates; Play shows disabled; the "playback arrives
    later" note is visible.
13. Setup-flow regression (verified once 2026-07-13, re-check after changes):
    OSK accept/Menu-button advances URL → username → password → connect;
    keyboard re-opens on each next field.
14. Both themes across all new pages; focus visible everywhere.

*(Future slices: append your console items here.)*

## Known gaps / notes from this session

- **No playback yet (Slice 10):** Play/Shuffle on artist + album detail are
  disabled with a visible note (06b/07b); Songs rows and album track rows are
  focusable but inert; no now-playing bar. All of 05b/06b/07b and the Songs
  row action unblock on Slice 10.
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
- **Favorite heart shows state by color, not a filled glyph:** the shipped
  Fluent Regular font has no filled heart, so starred = accent-color fill of
  the outline heart. Verify legibility at 10 ft on device.
- **Resume card (§6.1) intentionally absent** until Slice 05b (after queue
  persistence exists); acceptance criterion "shows resume card when
  persisted context exists" is vacuously satisfied — nothing can persist
  context yet.
- Carousel focus is plain XY navigation (no focus engagement); crossing a
  long carousel vertically means stepping through whichever card is below.
  Same applies to the new browse grids. Revisit after the console feel-test.
- Footer nav items (Settings/Account) sit outside NavigationView's selection
  model; selection visuals are mirrored manually (now also resynced after
  B-back) — verify they look right on device.
- Remaining placeholder section pages (Search/Playlists) have no focusable
  content, so Right-into-content has no target there. Fine until those
  slices land.
- Hiding the currently open section from Settings leaves its content shown
  until the next selection. Acceptable for now.
- Detail pages reached from a hidden-then-shown section, and section history
  under B-back, use the frame's natural back stack — section switches
  accumulate history (standard NavigationView behavior).
- The Slice 02 spike UI (test-tone buttons) was removed with the MainPage
  rebuild; `PlaybackService` remains for Slice 10. Background audio itself
  was already console-verified in checkpoint #1.

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
