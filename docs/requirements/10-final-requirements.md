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
- Resume MUST restore the queue paused at the saved track and position; the app MUST NOT auto-play on launch.
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

## 17. Open Decisions / TODO

- TODO: choose exact search endpoints/scopes and pagination/virtualization behavior for large libraries — resolve before Slice 13.
- TODO: decide Songs row primary action (immediate playback on A/Enter vs secondary action/context menu) — resolve before Slice 08.
- TODO: decide playlist editing scope for v1 (read-only vs create/edit/delete) — resolve before Slice 09.
- TODO: define error-state copy guidelines and offline/unreachable-server retry UX in more detail.
