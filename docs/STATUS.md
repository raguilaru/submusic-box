# Current Status

Last updated: 2026-07-10 (Slices 03 + 04 implemented; console checkpoint pending).

## Where we are

**Phase 1 (foundation + spikes) is DONE, including on-device checkpoint #1**
(app launches, D-pad navigates, background audio + SMTC verified on console
2026-07-09).

**Phase 2 steps 2.1–2.3 are code-complete:**

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

**Verification state:** x64 Debug smoke build green over SSH; 23 + 27 unit
tests green in the VM. **Manual verification (VM arm64 F5 and the console
run) has NOT happened yet — that is the next step.**

**Git state:** `main` at `ae9573e` (Slice 03). Slice 04 changes are in the
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

Then: **2.4 (Slice 05 — Home)**, and fold the Release|x64 .NET Native
validation into checkpoint #2 per the build plan.

**Client API surface ready for Home (2.4):** `GetAlbumList2Async(
AlbumListType.{Frequent,Newest,Random})`, `GetCoverArtUri(id, size)`;
`AppSession.Client` is restored (unconnected) at launch — Home must call
`ConnectAsync` (or handle the not-yet-connected state) before album calls.

## Known gaps / notes from this session

- Footer nav items (Settings/Account) sit outside NavigationView's selection
  model; selection visuals are mirrored manually — verify they look right on
  device.
- Placeholder section pages have no focusable content, so Right-into-content
  has no target there (Settings/Account do have focusables). Fine until real
  content lands.
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
