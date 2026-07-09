# Current Status

Last updated: 2026-07-10 (Slices 03, 04, 05a, 06a implemented; console checkpoint pending).

## Where we are

**Phase 1 (foundation + spikes) is DONE, including on-device checkpoint #1**
(app launches, D-pad navigates, background audio + SMTC verified on console
2026-07-09).

**Phase 2 steps 2.1–2.4a + 2.5a are code-complete:**

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
    the host page. Cards are focusable but inert — album detail is Slice 07.
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
    album grid in the browse-card style (cards inert — album detail is Slice
    07), disabled Play/Shuffle + note, retryable error state.
  - New Fluent glyphs all verified against the shipped TTF's glyph table
    before use (§13.3, never invented): heart F47A, grid F463, list F4EE,
    play F606, shuffle EF37.

**Verification state:** x64 Debug compile green over SSH (APPX0703 at the
packaging step is the known command-line-only failure); 23 Core + 36
SubsonicClient unit tests green in the VM incl. live Navidrome tests.
**Manual verification (VM arm64 F5 and the console run) has NOT happened yet —
that is the next step.**

**Git state:** `main` at `4a7509a` (Slice 05a). Slice 06a changes are in the
working tree, uncommitted, at the time of this STATUS write.

## Next step: on-device checkpoint (start here next session)

Console run (x64 / Remote Machine 10.0.0.28 / F5 — Rafael drives). Optionally
a quick VM arm64 Local Machine F5 first. Checklist:

1. **Setup flow (console-specific risks):** on-screen keyboard entry with the
   controller for all three fields; paste/hardware keyboard if available;
   wrong password → inline auth error; bogus host → inline connection error;
   real Navidrome connect → lands in shell. Use `http://10.0.0.5:4533` if
   `raspberrypi.local` fails on the Xbox (mDNS from UWP on console is
   unverified) — or the external `https://navidrome.raguilaru.xyz`.
2. **LAN capability:** connecting to the Pi verifies
   `privateNetworkClientServer` actually unlocks LAN access on console.
3. **Credential persistence:** quit + relaunch → straight to shell (no
   setup); Account shows server + username.
4. **Shell:** D-pad through nav in order; icons render (Fluent font); pane
   collapse/expand; footer Settings/Account respond to **A** (manual invoke
   path — watch this one); focus visible at 10 ft; both themes if quick.
5. **Toggles:** hide/show Artists/Albums/Playlists from Settings, confirm
   they disappear/reappear and persist across relaunch; Search/Home always
   present.
6. **Sign out:** returns to setup; B does not re-enter the shell; relaunch
   lands on setup again.
7. **Home (Slice 05a):** after relaunch (restored client), Home connects on
   first use and the three carousels load with real Navidrome albums; cover
   art renders (authenticated getCoverArt URLs straight into `BitmapImage` —
   first console test of that path); D-pad Left/Right travels a carousel,
   Down moves to the next one, page scrolls focused cards into view; wrong
   server reachable/unreachable → page-level error with a focused Retry;
   both themes if quick.
8. **Artists (Slice 06a):** nav to Artists → grid loads with real Navidrome
   artists + artwork; the grid/list toggle flips modes and stays visible;
   D-pad travels the grid, A opens an artist; detail shows artwork, name,
   counts, and the album grid; the favorite **heart** toggles and the change
   sticks across leaving/re-entering the artist (server star/unstar — first
   console test of that write path); Play/Shuffle are visibly disabled;
   connect/unreachable → focused Retry; both themes if quick.

Then: **2.5b (Slice 07 — Albums)** — the next slice in order (Slice 06b's
Play/Shuffle waits on the Slice 10 playback engine) — and fold the Release|x64
.NET Native validation into checkpoint #2 per the build plan.

## Known gaps / notes from this session

- **Home cards are inert:** focusable for D-pad travel but no click action
  until album detail lands (Slice 07). No now-playing bar yet either
  (Slice 10).
- **Artist detail Play/Shuffle are disabled (Slice 06b):** shown with a
  visible "playback arrives later" note; they wire up once the Slice 10 queue
  / now-playing infra exists. Album cards in artist detail are inert until
  album detail lands (Slice 07).
- **Favorite heart shows state by color, not a filled glyph:** the shipped
  Fluent Regular font has no filled heart, so starred = accent-color fill of
  the outline heart. Verify legibility at 10 ft on device.
- **View-mode toggle is inline in `ArtistsPage`**, not yet a shared control;
  extract a common toggle when Slice 07 (Albums) needs the same, to keep the
  §13.5 "one consistent toggle" promise.
- **Resume card (§6.1) intentionally absent** until Slice 05b (after queue
  persistence exists); acceptance criterion "shows resume card when
  persisted context exists" is vacuously satisfied — nothing can persist
  context yet.
- Carousel focus is plain XY navigation (no focus engagement); crossing a
  long carousel vertically means stepping through whichever card is below.
  Revisit after the console feel-test if needed.
- Footer nav items (Settings/Account) sit outside NavigationView's selection
  model; selection visuals are mirrored manually — verify they look right on
  device.
- Remaining placeholder section pages (Search/Albums/Songs/Playlists) have no
  focusable content, so Right-into-content has no target there (Home/Artists/
  Settings/Account do have focusables). Fine until real content lands.
- Hiding the currently open section from Settings leaves its content shown
  until the next selection. Acceptable for now.
- B-button back navigation inside the shell is not wired (not required by §5
  yet).
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
