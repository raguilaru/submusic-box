# Current Status

Last updated: 2026-08-13 (background memory rounds two and three: the console
cleared artwork (~27 MB) and the overlay (free) as suspects and pinned the cost
on the resting floor: a track change costs 30–45 MB but that DRAINS a few
seconds later, so nothing leaks — the app simply rests at 182–194 MB against a
128 MB limit; recycling playback items was tried as a fix and reverted;
cover-art URLs were
unique per request — defeating both platform caches — so the strategy moved
from shedding memory to not allocating it; plus a diagnostic switch that runs
the app with no artwork, to measure what artwork actually costs. On top of
the 2026-08-12 background memory fix: the app now releases its view
to survive the Xbox 128 MB background limit — the cause of music stopping when
a game launches — plus a file-based diagnostic log; on top of
the light-theme rework + nav-pane brand mark, on top of
the Favorites tab focus / Show Favorites / overlay header spacing round, the
browse list rows + view-toggle restyle, and the Favorites and Songs-sorting
rounds; the file was condensed to a clean
final-state summary on 2026-07-23 — the previous chronological pass-by-pass log
lives in git history).

**SubMusic Box** (code name BoxSubMusic) — native Xbox Series X/S music app for
Subsonic/OpenSubsonic servers. This file is the running project-state summary;
the normative contract is `docs/requirements/`, and the decision log is
requirements §16.

## Where we are

**Planned v1 scope (slices 00–14) is implemented and console-verified end to
end.** Every checkpoint (#1–#12) is closed; PR #6 (`7caa38b`) merged the last
planned slice. Since then, refinement rounds have landed on top:

- **Search** (Slice 13) — search3, submit-triggered, "All" view + scope filters
  with incremental paging. Console-verified (checkpoint #11).
- **App rename + branding** — display name "SubMusic Box" (display-layer only;
  internal identifiers keep BoxSubMusic). Console-verified (checkpoint #10).
- **Retro visual theme** — logo-derived palette (seafoam/teal accent, cream,
  petrol surfaces), triple-stripe motifs, vinyl-disc placeholders, spinning
  current-track marker, parchment frames. Partly device-verified (see backlog).
- **Lyrics display** — OpenSubsonic `songLyrics` in the now-playing overlay
  (synced auto-follow + unsynced), capability-gated. Overlay Queue⇄Lyrics tab
  with LB/RB switching (PR #13). Not yet device-verified.
- **Songs sorting + Play All / Shuffle All** (2026-07-26) — sort options
  (Default Order / Title / Artist / Album / Date Added) and whole-library
  Play All / Shuffle All on the Songs view. The API cannot sort songs, so
  ordering is client-side (`Core/Browsing/SongSorter`) over a now
  fully-loaded library; see §16 and "Known gaps". Not yet device-verified
  (backlog items 23–29).
- **Lyrics seek-to-line** (2026-07-26) — A/Enter on a synced lyric line now
  seeks playback to it (`PlaybackService.SeekTo`), revising §10.1's original
  read-only rule; reading with the D-pad still never moves playback, and
  "Jump to current line" stays the non-destructive way back. Not yet
  device-verified (backlog items 18–22).
- **Favorites section** (2026-07-26) — a new left-nav destination
  (`FavoritesPage`) listing the starred library via `getStarred2`, with
  Artists/Albums/Songs tabs and per-tab sort options; tab and sorts persist
  (`FavoritesPreferences`). No paging — getStarred2 returns the whole set in
  one request, so sorting covers everything without the whole-library load the
  Songs view needs. The Songs tab reuses `Core/Browsing/SongSorter`, so both
  views order songs identically. Play All / Shuffle All act on the whole
  active tab (bounded fan-out for the Artists/Albums tabs), and every result
  carries a full context menu including "Remove from favorites". Not yet
  device-verified (backlog items 30–37).
- **Browse list rows + view-toggle restyle** (2026-07-27) — the grid/list
  toggle is now a retro tonal-teal pill reading as a segmented control (two
  glyph segments, thumb under the current mode, mode name), still one
  focusable stop; Albums list rows carry year · genre · tracks · total play
  time plus a starred marker, Artists list rows the album count plus a
  starred marker, plus track count and total play time summed from one
  getAlbumList2 pass (`Core/Browsing/ArtistTotals`) that fills in behind the
  rows — getArtists carries no such totals, and a getArtist per row is
  RTT-bound. Playlists list rows carry owner + the playlist's own description
  and tracks · total play time (no starred marker — playlists have no favorite
  state). Artist detail's counts line gained total play time + the albums' year
  span; album detail gained genre. The "Artist" sort option's mic glyph became
  the person glyph in both sort menus. Shared logic moved to Core:
  `Browsing/DurationText` (+7 tests) and `Browsing/ArtistTotals` (+8 tests).
  Not yet device-verified (backlog items 38–43).
- **Segmented-row focus + Show Favorites + overlay header spacing**
  (2026-07-27) — the Favorites tab row and Search's scope filters now pin their
  own left/right focus (Left from Favorites' Songs tab reached the nav pane
  instead of Albums; Search had the same unpinned shape), and Favorites' sort /
  Play All / Shuffle All cluster pins leftward back into the active tab for the
  same reason; **Show Favorites**
  joins the Settings nav toggles, revising §5's original "Favorites is not
  hideable" clause (see §16); and the expanded overlay's Queue ⇄ Lyrics header
  got real spacing. Not yet device-verified (backlog items 44–46).
- **Light theme rework + nav-pane brand mark** (2026-07-27) — the light half
  is rebuilt on a warm parchment ground with the chrome deepened into real
  cards, the muddy aged-tan primary replaced by the deep teal accent, and an
  ambient record-groove watermark under the content (which is why every page
  inside the shell now paints `Background="Transparent"` and the shell paints
  the ground once). The app mark joins the nav pane header — the app's one
  branding placement, per the new §13.6. Dark is untouched apart from the very
  faint watermark. Not yet device-verified (backlog items 47–52).
- **Background survival under the Xbox memory limit** (2026-08-12) — the fix
  for "music stops when I launch a game". Backgrounding drops the app's limit
  from ~1 GB to **128 MB** with a ~2 s window to comply; `ReduceMemoryUsage`
  was still the Slice 02 skeleton (a bare `GC.Collect`), so the app usually
  failed to comply and was terminated. It now releases the visual tree
  (`Window.Current.Content = null`) under pressure and rebuilds it on
  `LeavingBackground`, which also sheds the `NavigationCacheMode.Required`
  Songs page holding the whole song library. Bitrate was investigated and
  ruled out as the mechanism; the manifest capability, the process-wide
  `MediaPlayer` and SMTC wiring were all already correct. Adds
  `Services/BackgroundDiagnostics` — a file log, because Xbox does not
  enforce background limits under a debugger, so this bug is only observable
  in a non-debug run. Not yet device-verified (backlog items 53–58).
- **Cover-art URL caching + request tiers** (2026-08-13) — the second
  background-memory round, and a change of strategy. Ten console rounds
  established that *releasing* memory does not work here (footprint is ~100 %
  native; dropping the whole visual tree reclaims ~1 MB), so this round attacks
  the allocation instead. Root cause found in the request path:
  `GetCoverArtUri` went through the shared request builder, which mints a
  **fresh random salt per call**, so every cover URL was unique — XAML's
  URI-keyed image cache and the HTTP cache could never hit, and the image cache
  accumulated an entry per request in native memory the app cannot release.
  All 16 call sites now route through `Services/CoverArt`, which mints one URL
  per (cover, tier) and rounds display sizes onto a four-step ladder, so one
  cover is one cached image instead of up to six. Request sizes also now
  match what is drawn (2026-08-12 halved the decode sizes but not the `size=`
  parameter, so covers still downloaded at 4× the area). Ships with a
  diagnostic **"Run without artwork"** toggle on Account — the measurement
  that bounds artwork's share of the ~139 MB floor. Partly device-verified
  (the measurement ran; see the next entry — backlog items 61–62 still open).
- **Console result + jump instrumentation** (2026-08-13) — the artwork
  measurement was run and it **answered its question and cleared its suspect**:
  same build, same 228-track queue, `EnteredBackground` reads **156 MB with
  artwork and 129 MB without**, so artwork is ~27 MB. With artwork fully
  suppressed the app still reached **290 MB** and was killed. The
  content/composition split also cleared the overlay: opening it costs
  **0–1 MB**, which **retracts the "overlay costs ~49 MB" reading** of
  2026-08-12 (that number was everything that happened in the eight seconds
  after it opened). The real culprit is one interaction, isolated by a control
  run: sitting in the overlay for 27 s costs **+7 MB**, while scrolling the
  228 rows and jumping to a track costs **+91 MB** (artwork on) and **+166 MB**
  (artwork off) — precisely the reported symptom. Two mechanisms remain (open
  `MediaPlaybackItem` buffers vs. the queue list realizing rows), so this round
  instruments rather than guesses: `JumpRequested` / `AfterJumpMoveTo` /
  `AfterJumpSettled` bracket the jump, and `MaxPlayedItemsToKeepOpen` drops
  from 2 to 0 permanently as a candidate fix that is also half the experiment.
  Not yet device-verified (backlog items 63–65).
- **App-owned cover cache** (2026-08-13) — the app now decodes its own covers
  from a stream instead of letting XAML load them from a `UriSource`, so
  "release the artwork" is finally a real operation. `CoverArt` holds a bounded
  24 MB LRU of `BitmapImage`s and `DropBitmaps()` runs on `EnteredBackground`
  (logged as `AfterDropCovers`) and again under memory pressure. A
  stream-decoded bitmap has no URI, so the framework's process-wide URI-keyed
  cache — the thing that made every view release reclaim 0 MB — never gets an
  entry. All 25 XAML sites moved to the `Controls/CachedCover` attached
  property (`DecodeWidth` + `SourceUri`, driven by `x:Bind` on both `Image` and
  `ImageBrush`, with a per-element token so a recycled row cannot show the
  wrong cover); the five code-behind sites call `GetBitmapAsync` directly.
  Purpose is the browsing surcharge, not the 128 MB line — see the durable
  notes. Not yet device-verified (backlog items 72–74).
- **Stream-open instrumentation; item recycling tried and reverted**
  (2026-08-13) — the jump bracket found that `AfterJumpMoveTo` costs **0 MB**
  (and returns ~10 MB, so `MaxPlayedItemsToKeepOpen = 0` works) while the
  settled reading 2.5 s later costs **+25 to +45 MB**, cumulative and never
  returned. Codec is not the driver — an m4a 256 kbps open cost more than a
  FLAC one. Recycling the retired item on each track change was tried as the
  fix and **made both symptoms worse** (peak 465 MB vs 268; plus multi-second
  stalls where `MoveTo` stopped taking effect), so it is reverted — see the
  §16 entry for why in-place item replacement is safe only where
  `RetryWithMp3` uses it. What the pass established is bigger than the failed
  fix: **natural advances cost exactly what jumps cost** (the session's very
  first track, untouched, went 109 → 152 MB), so this was never a jumping bug
  and a long background queue has always been accumulating. Kept:
  `MaxPlayedItemsToKeepOpen = 0`, the `_mapGate` lock, and `TrackChanged` /
  `AfterTrackSettled` on every stream open. Next step is a no-code
  measurement (backlog item 66).
- **Now-playing-bar spinning vinyl artwork** — console-verified (PR #8).
- **Nav / play-bar shortcuts + alignment** — View-button jump to the bar, LB/RB
  direct region jumps, pane/bar bottom alignment. Not yet device-verified.
- Recent tweaks: default theme changed to **Dark**; left-nav **chevron** pane
  toggle enlarged. (See `git log` for per-commit detail.)

**Build/test state:** builds green (x64 Debug over SSH; the `APPX0703` appx
packaging error is the known command-line-only failure — VS F5 packages fine).
**100 Core + 80 SubsonicClient tests pass** (9 SubsonicClient live-server tests
skip without `liveserver.local.txt`).

**Git state:** `main` holds v1 (slices 00–14, PR #6) plus the merged refinement
rounds above.

## Architecture at a glance

Three projects (`docs/architecture/20-project-structure.md`):

- `BoxSubMusic.App` — UWP XAML app, views/controls/platform services.
- `BoxSubMusic.Core` — queue/playback logic, setup + persistence helpers,
  playlist artwork composition; unit-testable, no UWP host.
- `BoxSubMusic.SubsonicClient` — typed API client; transport is the API's native
  XML via `XDocument` (no reflection serializers, decision 2026-07-09).

Key mechanisms:

- **Toolchain:** legacy **.NET Native** UWP (modern .NET + AOT won't activate on
  Xbox). See `xbox-uwp-toolchain` memory + the modernization plan
  (`docs/architecture/40-dotnet-modernization-plan.md`, planning-only).
- **Playback** (`docs/architecture/30-playback-architecture.md`): Core `PlayQueue`
  (replace-not-append, shuffle mapping, repeat) mirrored into a
  `MediaPlaybackList` by `PlaybackService` — gapless, next-track prefetch, SMTC.
- **Persistence:** `LocalSettings` for scalars (theme, nav toggles, server
  URL/username, resume index/position); `LocalFolder` XML for structured data
  (recent servers, resume-queue structure). Password only in the Credential
  Locker (PasswordVault).
- **Capability detection** gates every optional feature; unsupported features
  degrade by hiding/disabling (§3), never silently.

## On-device test backlog (next console pass)

Everything through checkpoint #12 is verified; the items below are the
code-complete-but-not-yet-device-verified rounds.

**Decode-failure mp3 fallback (§16 bitrate-restriction decision):**

1. **Server-side bitrate restriction end-to-end:** set a max bitrate (no format,
   or a lossy one — not flac) on the **SubMusicBox [NSPlayer]** player entry in
   Navidrome, play something high-bitrate, confirm the restriction is honored
   (log: "Transcode decision: transcode"; no "no compatible profile").
2. **Ogg/Opus decode on Xbox** (expected: does NOT decode natively — Opus/Ogg
   absent from Microsoft's Xbox codec table). With the restriction active the
   mp3 fallback engaging is the expected path: first track hiccups once, then
   plays as mp3 at the capped bitrate; later queues play without the hiccup
   (session prefers mp3 after the first fallback). No skip toast for the retried
   track; playback must not stop. (Optional: install "Web Media Extensions" on
   the console and re-test whether Opus then direct-plays.)
3. **Delivered-stream labels:** with the restriction active, the bar + expanded
   queue format/bitrate line shows what's actually playing (e.g. "OPUS", or
   "MP3 · <cap> kbps") not the file's tags; unrestricted servers show the real
   codec + file bitrate. Tag values may flash briefly at track start.
4. **Network failures still behave:** with the server unreachable, the normal
   skip toasts + failure-stop still occur (decode fallback must not swallow
   network errors).

**Retro visual theme (2026-07-19):**

5. **Card-menu "Play":** on artist/album/playlist cards (browse, Home carousel,
   search results, artist-detail album grid) "Play" replaces the queue and starts
   the container from its first song; bar reveals.
6. **Browse focus:** opening Artists/Albums/Playlists lands focus on the FIRST
   CARD; Up still reaches the view toggle.
7. **Retro accent reach:** hearts, starred/current-track markers, checked
   toggles, Settings radios, and the nav selection indicator all show the
   seafoam/teal accent — note ANY surface still showing the console system accent
   (the override's reach on the console OS build is the open question).
8. **Teal surfaces:** nav pane backdrop + now-playing bar read as petrol teal
   with legible text in BOTH themes (pane text contrast on teal is the dark
   check; pale-seafoam pane is the light check). Pane backdrop is solid (no
   acrylic) by design.
9. **Stripe motif:** triple stripes under page/carousel/resume/queue titles —
   legible, not distracting, correct in both themes.
10. **Vinyl placeholders:** artless albums show the vinyl disc at every size;
    loading tiles show it briefly before art paints.
11. **Parchment frames:** the 1-epx tan border on cards/headers reads as
    intentional at 10 ft in both themes.
12. **Nav icon alignment:** compact strip — icons centered in the 48-epx rail
    (`CompactPaneLength="48"`); expanded pane — labels reasonably centered
    against their icons (`VerticalContentAlignment="Center"`; a residual
    descender offset is a known minor deferred item — do NOT reintroduce a bare
    `ContentTemplate` override, it blanked the icons on a prior pass).

**Lyrics display (2.21):**

13a. **Bar button + entry:** lyrics button appears on the bar during playback
     (server supports songLyrics) and opens the overlay to the Lyrics tab; Y
     still opens Queue.
13b. **Synced lyrics:** active line highlights + auto-scrolls to center in time
     with playback; pausing freezes it; legible at 10 ft in both themes.
13c. **Reading navigation:** D-pad reads ahead/back and playback does NOT move
     on selection alone; follow pauses, "Jump to current line" appears; the
     button / ~4 s idle re-centers and resumes without moving playback.
     (A now seeks — see item 18.)
13d. **Unsynced lyrics:** plain text, scrollable, no highlight/hint.
13e. **No lyrics:** shows "No lyrics available for this track." after a brief
     "Loading lyrics…"; updates on track change.
13f. **Unsupported server (optional):** with no songLyrics extension the bar
     button and Lyrics tab are both absent (overlay queue-only).
13g. **Song change while open:** advancing on the Lyrics tab reloads that track's
     lyrics; focus/scroll behave; the Queue tab is unaffected.

**Nav / play-bar shortcut + alignment round (2026-07-22):**

14. **Pane/bar bottom alignment:** pane backdrop (`Margin="0,0,0,16"`) and the
    floating bar bottoms line up; the footer hint isn't clipped.
15. **View-button jump to the bar:** View from nav/content jumps focus to the
    bar's play/pause in one press; View again returns focus.
16. **LB/RB direct region jumps:** LB always lands on the nav pane, RB on the
    content, from any region; the slimmed single-hint footer isn't cramped.

**Play-bar shortcut-legend clarity pass (2026-07-23):**

17. **Top-strip shortcut legend:** the bar's two controller shortcuts now read
    as consistent labeled badge+text pairs in the top-right strip —
    **View · Now playing** and **Y · Queue** — replacing the old naked **(Y)**
    badge glued to the queue button (no label, overlapped its glyph). Confirm
    at 10 ft: both hint pairs are legible in both themes, each sits roughly
    above the region it targets (View over the transport, Y over the queue
    button), and neither crowds the vertically-centred metadata/time/utility
    clusters. The queue button's glyph is now unobstructed.

**Lyrics seek-to-line (2026-07-26, §10.1 revision):**

18. **A seeks to the picked line:** on synced lyrics, A/Enter (or a click) on a
    line moves the track to that line's time and playback continues from there;
    the highlight lands on the picked line with no visible bounce back to the
    old line, the elapsed time updates at once, and auto-follow resumes.
    Check both while playing and while paused (paused should reposition
    silently, not start playing).
19. **Reading still doesn't seek:** D-pad through several lines with playback
    running — the audio must not move; only pressing A does.
20. **Return affordance still non-destructive:** after reading away, the
    "Jump to current line" button (and the ~4 s idle resume) brings the view
    back WITHOUT moving playback.
21. **Edge cases:** A on the LAST line seeks near the end and the track keeps
    playing to its natural end (it must NOT skip to the next track); A on a
    blank/untimed spacer line lands on the nearest timed line above it; A on
    unsynced lyrics still just re-centers (no seek, not dead).
22. **Hint copy:** the lyrics-tab footer badge reads **A · Play from line**
    (was "Jump to current line"), legible at 10 ft in both themes.

**Songs sorting + Play All / Shuffle All (2026-07-26, §7.3):**

23. **Whole-library load:** opening Songs shows the first rows quickly, then
    "Loading all songs…" with Sort / Play All / Shuffle All disabled; when it
    clears, all three enable. Note how long the tail takes on the real library
    and whether the app stays responsive (scrolling, playback) throughout —
    this is the §11 memory/perf check for the new load.
24. **Each sort is correct and reachable:** the Sort button opens with A, the
    check mark tracks the current option, the button label updates, and Title /
    Artist / Album / Date Added visibly reorder the list. Default Order returns
    to the server's order. After a sort, focus lands back at the top of the list
    (not stranded in a recycled row). **Per-option icons** (list / Aa / mic /
    album / calendar) render alongside the check mark — the OS template shows
    both, but the icon sits in a 16-epx box, so confirm it reads at 10 ft and
    that no option shows a blank or a box glyph.
25. **Play All / Shuffle All:** both replace the queue and reveal the bar; Play
    All starts at the first row of the current sort; Shuffle All is randomized.
    Confirm the queue really holds the whole library (expanded queue is long),
    not just a page.
26. **Row play follows the sort:** with a non-default sort active, A on a row
    starts there and the up-next order matches what's on screen.
27. **Date Added availability:** the option is enabled against Navidrome (which
    sends `created`) and orders newest-first. If any test server omits it, the
    option must be disabled rather than silently doing nothing.
28. **Navigation cache:** Songs → a song's album (Go to album) → B back returns
    to the same sorted list, at once, with no reload. Known trade-off to eyeball:
    a star toggled on the album page won't repaint the kept Songs row.
29. **Focus/layout at 10 ft:** the three header controls read clearly, Up from
    the first row reaches Sort (not the nav pane), and LB/RB region jumps still
    behave with the new header row.

**Favorites section (2026-07-26, §7.5):**

30. **Nav + first open:** a Favorites item appears after Playlists with the
    heart icon; a first visit (nothing remembered yet) opens on the **Songs**
    tab, not Artists. Focus lands in the list itself, not the tab row, and Up
    from the content reaches the tabs.
31. **Tabs:** Artists/Albums/Songs each list the right starred content with
    the browse card language / §7.3 columns; switching tabs is instant (no
    spinner — one getStarred2 filled all three) and leaves focus on the tab
    row rather than yanking it into the results.
32. **Sort menu at 10 ft:** the sort button names the active sort; opening it
    shows only that tab's options (2 for Artists, 2 for Albums, 5 for Songs)
    with a legible checkmark AND a per-option icon on the current one (see
    item 24 — same 16-epx caveat). Confirm the flyout is gamepad-reachable and
    that Down from the button enters the list. The Songs tab's five options are
    the same `SongSorter` orderings and the same glyphs the Songs page uses
    (item 24) — worth confirming the two pages agree on the same library.
33. **Sorting is total, not partial:** with a large favorites set, changing
    the sort reorders EVERYTHING immediately and fetches nothing — scroll to
    the bottom and confirm the last item belongs there under that order.
    Check Albums "Year" (newest first, undated last) and Songs "Date Added"
    (disabled outright if the server omits `created`, as on the Songs page).
34. **Persistence:** pick a non-default tab and a non-default sort in each
    tab, navigate away and back — both restored; relaunch the app — still
    restored.
35. **Song row behavior:** A plays the favorites in current sort order from
    that row (bar reveals, queue shows "Favorites"); "Remove from favorites"
    makes the row DISAPPEAR (and the list falls back to the empty state if it
    was the last one); emptying a scope shows that scope's own message.
36. **Play All / Shuffle All:** on Songs both are instant and the queue
    matches the on-screen order exactly (Shuffle randomizes the whole pool,
    first track included). On Albums and especially **Artists** this is the
    round to watch: confirm both buttons AND the sort button disable behind
    the ring while it gathers, that the wait is tolerable for the real
    favorites library, and that the resulting queue order is artists → each
    artist's albums oldest-first → disc/track. Switching tabs mid-gather must
    still play the tab the button was pressed on. Unreachable server → toast,
    no playback, buttons come back.
37. **Card menus:** artist and album cards both offer Play / Play next / Add
    to queue / Remove from favorites, and album cards also Go to artist
    (disabled when the server sent no artist id). Removing a card makes it
    vanish from the grid on all three tabs alike.

**Browse list rows + view-toggle restyle (2026-07-27):**

38. **View toggle at 10 ft:** on Artists, Albums and Playlists the toggle reads
    as a teal pill with two glyph segments — the thumb sits under the CURRENT
    mode and the label names it ("Grid"/"List"). Confirm in both themes that
    the pill no longer looks like a switched-on system control, that the
    inactive glyph is visibly dimmer but still legible, and that the pill
    doesn't change width as it flips (the header must not twitch).
39. **Toggle states + focus:** pressing A flips the mode with no leftover
    "pressed" fill (the state bug the template's Checked* states guard
    against); the focus rectangle is still couch-visible; Up from the first
    card still lands on the toggle and Down from the toggle re-enters the
    active view.
40. **Albums list rows:** each row shows year · genre · tracks · total play
    time on the right, with a filled accent heart on favorites. Confirm the
    right-hand line is legible at 10 ft, that a long album/artist name
    ellipsizes instead of squeezing that line, and that an album missing
    genre/year shows a shorter line with NO stray "·" separators or zeros.
41. **Artists list rows + totals fill-in:** name on the left, starred heart,
    then "N albums" on the right, which becomes
    "N albums · N tracks · H hr M min" once the album-list pass finishes. Watch
    the fill-in on the REAL library: how long the pass takes, that the page
    stays responsive (scrolling, playback) while it runs, that no row ever shows
    a partial sum, and that leaving the page mid-pass leaves nothing half-baked
    on return. Spot-check one artist's totals against its albums, and confirm a
    compilation-heavy library doesn't produce obviously wrong sums (albums with
    no artistId are skipped by design). With the server unreachable mid-pass the
    rows must simply keep "N albums" — no error state, no toast.
42. **Detail views:** artist detail's counts line reads
    "N albums · N songs · H hr M min · 1994–2015" (each part present only when
    the server had the data), and album detail's header shows genre between the
    year and the song count. Sanity-check one artist's total against its albums
    and confirm the line doesn't wrap awkwardly for a prolific artist.
43. **Playlists list rows:** each row shows the composite artwork, the name,
    "by \<owner\>" (plus the playlist's description when it has one) and
    "N tracks · H hr M min" on the right. Confirm the description doesn't push
    the name off the row on a long one (both trim), that a playlist with no
    description shows just the owner, and that no starred marker appears
    (playlists have no favorite state by design).

**Favorites focus + Show Favorites + overlay header spacing (2026-07-27):**

44. **Segmented-row focus (Favorites tabs + Search scope filters):** on
    Favorites, with focus on the **Songs** tab, Left moves to **Albums**, and
    Left again to **Artists**; only Left from Artists leaves the row for the
    nav pane. Right walks back the same way. Right from Songs must still reach
    the sort / Play All / Shuffle All cluster — check this both idle and
    **while a Play All fan-out is running** (those controls are disabled then;
    focus must not vanish or stick). Coming back the other way, **Left from
    Shuffle All → Play All → Sort → the tab currently showing** (not the nav
    pane); switch tabs and confirm Left from Sort follows the active tab
    (Artists, Albums, Songs in turn). On **Search**, run a query and walk the
    All / Artists / Albums / Songs filters the same way: Left steps between
    them and only Left from **All** reaches the nav. Also confirm LB/RB still
    move the caret while the query box has focus (that mapping outranks region
    cycling there and is untouched by these pins).
45. **Show Favorites:** Settings now lists a fourth nav toggle after Show
    Playlists. Turning it off removes the Favorites nav item; turning it back
    on restores it; the choice survives a relaunch. Up/Down through the four
    switches into the theme radios still walks the column without straying
    into the nav pane. Known, unchanged quirk: hiding Favorites **while
    viewing it** leaves its content on screen until the next nav selection.
46. **Overlay header spacing:** open the expanded overlay on a server WITH
    lyrics — the LB badge, "Queue", the "Lyrics" pill and the RB badge should
    read as four separate elements with air between them, the stripe motif
    and the "<album> · N songs" line should each stand clear, and the
    Jump-to-current / collapse buttons shouldn't crowd the tabs. Check the
    Lyrics tab too, where the "Jump to current line" button appears in that
    row. On a server WITHOUT lyrics the single "Queue" title should still sit
    correctly (the tab strip is hidden, not restyled).

**Light theme rework + nav-pane brand mark (2026-07-27):**

47. **Light mode, the whole point of the round:** switch the theme to Light and
    walk Home → a browse grid → a detail page → Settings. The ground should read
    as warm parchment (never white), the nav pane and the play bar as distinct
    cards sitting ON it with a visible tan hairline, and the play/pause disc,
    the progress line and the detail Play buttons as deep teal — no brown
    anywhere except the card frames and the first stripe. Confirm at 10 ft that
    the parchment does not look dirty or yellow on the real TV (this is the one
    value that can go wrong on a differently-calibrated display) and that album
    artwork still reads cleanly against it.
48. **Dark mode is unchanged:** switch back to Dark and confirm nothing moved
    except the new watermark and the pane header — same black ground, same
    petrol surfaces, no border on the pane.
49. **Record-groove watermark:** faint rings sweep the right of every screen.
    Confirm in BOTH themes that it reads as texture and never as content —
    that no ring competes with a card edge or a focus rectangle, that the
    filled label reads as a record label and not a smudge, and that it is
    invisible enough on dark (7 % alpha) to be worth keeping there at all. Also
    check the setup screen, which now carries it too.
50. **Pages are transparent now:** navigate quickly between sections and in/out
    of detail pages. The known trade-off is that a Frame transition can briefly
    show two pages through each other — confirm it is not distracting on
    device. Nothing should ever show "no background".
51. **Nav pane brand mark:** the logo + "SubMusic Box" sit above the chevron
    toggle. Confirm the 40-epx mark is crisp (not soft — the asset is 176 px),
    that the wordmark is legible at 10 ft, and that the header steals no space
    the menu needs. **Collapse the pane**: the wordmark must disappear and the
    mark must centre on the 48-epx rail like the section icons, with no clipped
    sliver. Focus is the thing to watch — the header is inert decoration, so
    Up from Search must still reach the chevron item and never stop on the
    logo.
52. **Play-bar text contrast:** on the light theme's mid-tone bar, the
    artist · album line and the format/bitrate line (opacities nudged up this
    round) should both be comfortably readable at 10 ft.

**Background survival while a game runs (2026-08-12, §11):**

This round is different from every other backlog item: it **cannot be tested
under the debugger**. Xbox does not apply background memory limits to a
debugged process, so an F5 run will appear to pass no matter what. Deploy,
then **stop debugging and launch the app from the Dev Home tile**.

**A game is NOT required to exercise this** (established 2026-08-12 — retail
games can't run in Dev Mode anyway). The first console log shows the real
1024 → **128 MB** transition firing from an ordinary background excursion
(Guide / Dev Home), with `background=True`. That is the same code path a game
launch takes, so items 54–56 can be verified in Dev Mode as they stand. Only
item 53's "does the music actually survive a game" needs retail mode, and it
is the *consequence* of what the log measures rather than an independent
check.

53. **The bug itself:** play something, then launch a game and leave it
    running for several minutes. Music should keep playing every time — the
    symptom being fixed is that it usually stopped. Repeat from a heavy state
    (open **Songs** and let the whole library finish loading, browse a few
    grids so artwork is cached) and from a light one (play straight from Home
    after a fresh launch). The heavy state was the likely failure case, so it
    is the one that matters.
54. **Read the log — this is the real deliverable.** Pull
    `background-diagnostics.log` from the app's LocalFolder via the Device
    Portal (File explorer → LocalAppData → the app → LocalState). The line to
    find is `AppMemoryUsageLimitChanging`: it records usage, the old limit and
    the **new limit (expect ~128 MB)**. Then read the lines below it. Confirm
    in order: **"Releasing view content"** appears at all (its absence was the
    2026-08-12 failure — the release was running off the UI thread and doing
    nothing); **`AfterViewRelease`** shows usage having actually dropped; and
    **`AfterLimitChangingHandler`** is **comfortably under 128 MB**, not
    marginally under. A release that runs but reclaims little means something
    still holds the tree — look for a static capturing a page. If usage is
    already low and playback still dies, the cause is NOT memory and the
    `UnhandledException` lines are where to look next.
55. **Returning from the game:** switch back to the app. It should rebuild and
    be usable, landing on **Home** — the known trade-off is that navigation
    position and the loaded song library are lost when the view was released
    (`LeavingBackground` → "Rebuilding released view" in the log confirms this
    path ran). Playback must not glitch, restart, or lose its position during
    the rebuild, and the now-playing bar must show the correct track. Check the
    theme survived the rebuild (this is what the `ThemeService` idempotency
    fix guards) and that the nav toggles still reflect Settings.
56. **Short excursions are not punished:** open the Guide, or switch out and
    straight back. The view should NOT be torn down for a brief transition —
    the release is deferred to actual memory pressure precisely so returning
    stays fast. A "Releasing view content" line on every trivial excursion
    means the threshold is too aggressive.
57. **Bitrate is a control, not a suspect:** if a failure still occurs, repeat
    it at a low server-side bitrate cap. The research says bitrate is not the
    mechanism (the decode happens in the media pipeline, not on the app heap),
    so a failure that tracks bitrate would contradict the diagnosis and is
    worth knowing about.
58. **SMTC while backgrounded:** with the view released, confirm the Guide's
    media controls still show the track and still work (play/pause, next).
    They are owned by `MediaPlayer.CommandManager`, not the UI, so they should
    be untouched — this checks that assumption on device.

**Cover-art URL caching + the artwork measurement (2026-08-13, §11):**

This round is a measurement first and a fix second. Items 59–60 are the
measurement — they are the point of the deploy. Items 61–62 confirm the fix
broke nothing. Same rule as items 53–58: **not testable under the debugger**,
so deploy, stop debugging, and launch from the Dev Home tile.

59. **The artwork measurement — run it twice, this is the deliverable.**
    Fresh launch, then walk a FIXED path you can repeat exactly (say: Home →
    Albums, scroll a few screens → an album → back → Artists, scroll → play
    something). Background the app (Guide / Dev Home) and note the
    `EnteredBackground` usage. Now go to **Account → Diagnostics → Run without
    artwork = On**, restart the app, walk the *same* path, background, and note
    it again. The launch log stamps `artwork=on` / `artwork=SUPPRESSED` on each
    run so the two are never confused. The gap between the two numbers is
    artwork's entire contribution to the footprint — the question ten console
    rounds could not answer. If the suppressed run still lands near 139 MB,
    artwork is exonerated and the next round should look elsewhere; if it drops
    to 60–70 MB, an app-owned bitmap cache becomes the obvious next step.
60. **Did the URL fix move the floor by itself?** Compare this build's
    artwork-on reading against the ~139 MB the 2026-08-12 build recorded for a
    comparable session. Both platform caches can now hit, so a repeated pass
    over the same albums should cost far less than the first — worth walking
    the same grid twice in one run and watching whether the second pass adds
    anything at all. That second-pass number is the honest test of the caching
    claim; the first pass proves nothing.
61. **Artwork still looks right everywhere.** The request ladder means several
    surfaces now share one image, and one cover is served at a smaller
    requested size than before (grid cards asked for 400 and got 200). Check at
    10 ft for softness on: the browse grids and list-row thumbs, Home carousels
    and the resume card, artist/album/playlist detail headers, playlist
    composite tiles (grid AND list), the now-playing bar's spinning vinyl, and
    the expanded overlay's 400-epx artwork. Nothing should look softer than it
    did — if a header does, its tier is the thing to raise, not the decode size.
62. **The SMTC thumbnail still appears** in the Guide's media controls (it now
    shares the 320 tier with the detail headers), and **sign-out is clean**:
    sign out, sign in as the same or another user, and confirm covers still
    load. That path exercises the URL-cache reset — a stale cached URL would
    carry the previous login's credentials, and the symptom would be broken
    images rather than an error.

**Splitting the queue-jump spike (2026-08-13, §11):**

Items 59–60 are **answered** — artwork is ~27 MB and the URL fix is in; the
overlay itself is free. What remains is the 91–166 MB that scrolling the queue
and jumping to a track costs. Items 63–64 are two runs that separate the two
possible causes; run them in order and do not combine them, because combining
them is what produced the wrong answer last time. Non-debug launch as always.

63. **Scroll only, no jump.** Play a 200+ track queue, open the overlay, scroll
    all the way to the bottom of the rows, scroll back, then **close the
    overlay without selecting anything** and read `QueueOverlayClosed`. Then
    background and read `EnteredBackground`. If those numbers are tens of MB
    above the reading before you opened it, the cost is the queue `ListView`
    realizing rows — each carries a `SpinningVinyl` and a four-item
    `MenuFlyout` from the `DataTemplate` — and the fix is in the row template,
    not in playback. If they are flat, scrolling is innocent and item 64 has
    the answer.
64. **Jump with no scrolling.** Fresh launch, same queue, open the overlay and
    press A on a row that is **already on screen** — no scrolling at all. Read
    the three new lines in order:
    `JumpRequested #1` → `AfterJumpMoveTo #1` → `AfterJumpSettled #1` (2.5 s
    later). Growth between Requested and MoveTo means the platform allocated
    synchronously when it opened the target item; growth only by Settled means
    it was the new stream buffering; no growth at either means the jump is
    innocent and item 63 has the answer. Then jump twice more and watch whether
    each `AfterJumpSettled #N` steps up cumulatively — that is what tells us
    whether it accumulates per jump or reaches a ceiling. The line records the
    target's format and bitrate, so note whether the big steps are the lossless
    tracks.
65. **Did `MaxPlayedItemsToKeepOpen = 0` help, and what did it cost?**
    Compare item 64's numbers against the 91–166 MB the 2026-08-13 log
    recorded. Then check the trade it buys: **Previous** now has to reopen the
    stream, so confirm from the bar and from the Guide's media controls that
    pressing Previous still works and is not annoyingly slow (a modest delay is
    the accepted price; a multi-second stall is not). Gapless forward playback
    must be unaffected — this setting governs already-played items, not the
    next-track prefetch, so listen through two or three track boundaries to
    confirm nothing gaps.

**App-owned cover cache (2026-08-13, §11):**

Items 68 and 70 are **answered**: the minimal floor is 143 MB with artwork and
129 MB without, and — the finding that reprioritised everything — the app ran
**5+ minutes backgrounded at 133–143 MB**, so 128 MB is a threshold the OS
weighs rather than a line it enforces. The minimal case already survives. What
fails is browsing (183–194 MB) and post-jump spikes (250–290 MB). The cover
cache targets the first.

72. **Does it actually give the memory back?** Play something, then browse
    properly — several grids, scroll them, open a couple of detail pages — and
    background. In the log, find **`AfterDropCovers`**: it names the megabytes
    released and the usage either side. Compare `EnteredBackground` against the
    183–194 MB the same kind of session used to reach. Anything in the 140s–150s
    is the win. A released figure near 0 MB means the covers were not in the
    cache (everything on screen is still referenced by its element, so a small
    number on a *quiet* screen is expected — browse first).
73. **Artwork still looks and behaves right.** The load path changed for every
    cover in the app, so walk it: browse grids and **scroll fast** (the
    per-element token is what stops a cover landing on the wrong row — watch
    for any row briefly showing another album's art), switch grid/list modes,
    open artist / album / playlist detail headers, check playlist composite
    tiles in both grid and list, the now-playing bar's spinning vinyl across
    several track changes, the expanded overlay's 400-epx art, the Home resume
    card, and the Guide's SMTC thumbnail. A missing cover must still show the
    vinyl placeholder rather than a blank or an error.
74. **Coming back from the background.** Covers are dropped on the way out, so
    returning re-fetches them. Confirm that returning to a browse grid repaints
    within a beat rather than sitting empty, and that nothing flickers on a
    short Guide excursion (on-screen images keep their bitmaps — only the
    cache's retention is dropped, so a quick out-and-back should look
    unchanged).

**Detail-page virtualization + release ordering (2026-08-13, §11):**

The first background-memory build that changes behaviour rather than only
measuring. Items 75–76 are UI checks and need no special protocol; item 77 is
the memory measurement and follows the usual non-debug rule.

75. **The three detail pages still look and behave exactly as before.** Album,
    Artist and Playlist detail no longer wrap their list/grid in a
    `ScrollViewer` — the items control is now the scroll host and the header
    is its `Header`. Confirm on each: the page scrolls as ONE region (header
    scrolls away with the content, not pinned), the 48,36 inset is unchanged,
    Play / Shuffle / Play next / Add to queue still take focus and work, D-pad
    walks from the header buttons down into the rows and back up, and B-back
    returns cleanly. On Artist detail the album grid must still wrap into the
    same number of columns.
76. **Empty states.** A playlist with no songs, an album with no tracks, an
    artist with no albums: the header must still be visible with its
    "no songs/tracks/albums" line. This is the one behaviour that changed
    shape — the list is no longer collapsed when empty (collapsing it would
    now hide the header too), so an empty list must render as nothing at all
    rather than leaving a gap or a stray focus stop.
77. **The two measurements.** Non-debug launch, then read the log. Open a large
    playlist and check `changedSubs` on its `NavigatedSettled` line: **~15–20
    is the win, 99 means virtualization is still off**. Then background and
    check `changedSubs` on `AfterViewRelease`: **0 or near it** (was 87), with
    that line finally showing a real drop instead of zero. Then repeat the
    run-12 shape — browse, play, queue ~200 tracks, and switch
    background↔foreground four or five times with browsing in between — and
    watch `shells=` and the post-release floor across cycles.

**Realization cache + release ordering (2026-08-13, §11):**

78. **Scrolling still feels right.** `CacheLength` dropped from the framework
    default of 4.0 to 1.0 on every large list and grid, which is a ~5×
    reduction in pre-realized rows — the buffer that hides row construction
    during a fast scroll. Flick hard through Songs (the whole library), a big
    playlist, the Albums and Artists grids, and the expanded queue. Rows should
    appear instantly; **brief blank rows or visible pop-in is the failure**, and
    the fix is 2.0 rather than reverting. Also confirm focus still moves
    smoothly with the D-pad held down, and that grids keep the same number of
    columns.
79. **The three log signatures** (non-debug, background twice): PlaylistDetail
    `changedSubs` ~25 not 99; `Shell unload observed; collecting.` every cycle
    with `changedSubs=0` at `AfterViewRelease`; and exactly one
    `AfterViewDropped`/`AfterCollect`/`AfterViewRelease` triple per excursion
    alongside a `ReduceMemoryUsage: already reducing, skipped.` line.

*(Future work: append console items here.)*

## Background memory (§11) — OPEN, see the handoff brief

The investigation is unfinished. **`docs/architecture/50-background-memory-handoff.md`
is the self-contained brief**: established facts, what was tried and rejected,
claims that were retracted, ranked candidates, and the next step. Start there
rather than reassembling it from the notes below.

## Known gaps / durable notes

Design decisions and accepted trade-offs that should NOT be "fixed" without
revisiting the reasoning (many encode a hard-won console lesson).

**Playback / queue:**

- **No scrubbing surface** on either playback surface (not a §10 requirement;
  the progress lines stay display-only; SMTC/system seek still works where
  offered). The one in-track seek the app performs is **picking a synced lyric
  line** (§10.1, 2026-07-26) via `PlaybackService.SeekTo` — deliberately scoped
  to that gesture; adding a scrubber is a separate decision. `SeekTo` clamps
  500 ms short of the end (so the last lyric line cannot trip end-of-stream and
  skip the track) and no-ops when `MediaPlaybackSession.CanSeek` is false, which
  the lyrics list treats as "re-center instead". Accepted consequence: seeking
  forward past the min(50 %, 4 min) mark submits the scrobble — that threshold
  is a position check, not accumulated listening time.
- **Resume position is ~10 s coarse** during playback (exact on
  pause/suspend/track change) — a crash loses at most that window. The queue
  refetches nothing at resume; a song deleted server-side since the last save
  fails at stream time and is skipped by the normal `ItemFailed` path. Suspend
  flush relies on `OnSuspending` firing (normal Xbox lifecycle).
- **Playback surfaces show the DELIVERED stream** (`TryGetDeliveredStreamInfo`),
  not the file's tags — a restricted stream shows "OPUS"/"MP3 · 128 kbps".
  Browse/search lists keep tag metadata by design (a row describes the library
  file, not a future transcode).
- **Decode-failure mp3 fallback:** a stream the Xbox can't decode (server
  transcode in an unsupported format, e.g. Opus) is retried once with
  `format=mp3`; the server still applies its bitrate cap. Network-class failures
  keep the normal skip/stop path. No client-side quality setting — the
  server-side limit is authoritative (§16 2026-07-19).
- **Artist Play fan-out is bounded to 4** concurrent getAlbum requests
  (`BoundedParallel`); large artists trade a little start latency for not
  hammering the server (acceptable on console at checkpoint #12).
- **A failed stream item skips WITH a toast**; the consecutive-failure stop
  announces itself too. Newest toast replaces the previous — no toast queue.
- **JumpTo under shuffle self-corrects:** `MoveTo`'s index space with shuffle is
  undocumented, so a mismatch would produce one imperceptible wrong-item MoveTo
  before the corrective one (verified fine at checkpoint #5).
- **Repeat-one + SMTC:** manual next in repeat-one advances; only natural track
  end repeats.
- **Scoped-search queue is "rows loaded so far"** — reaching the end does not
  fetch further pages (§16 2026-07-15). **Songs no longer works this way**: it
  loads the whole library up front (see below), so a row's A/Enter queues every
  song in the current sort order.
- **Songs loads the entire library into memory** (§16 2026-07-26) — the price of
  client-side sorting, since the API cannot order songs. 500 rows per request,
  first page shown immediately and the rest streamed in behind it; sort and
  Play All / Shuffle All stay disabled with a visible caption until it finishes.
  Consequences: a large library has a real load tail; the page is
  **navigation-cached** (the second exception to refetch-on-navigation, after
  Search) so B-back from an album page doesn't redo it, which means starred
  markers on kept rows can lag; and a later page failing now drops the whole
  view to the inline error + Retry rather than just ending the scroll, because
  a silently truncated library would make the sort and Play All lie.
  This is the §11 background-memory item to watch on the next console pass.
- **Queue-edit clones:** a song added twice (or while already queued) is a cloned
  `Song`, so starring one copy repaints only that copy until the next refetch.
  Under shuffle, original-order edits land at the END (un-shuffling shows them
  last — documented, unit-tested).

**Favorites / staleness:**

- **Bar favorite heart reverts silently on failure** (snap-back is the feedback;
  detail pages keep a worded status). The context-menu toggle changes state only
  AFTER the server accepts. Starring mutates the loaded `Song` and repaints its
  own row; other already-rendered surfaces don't repaint mid-view, but every page
  refetches on navigation and the bar re-reads on track change, so staleness
  can't outlive the current view.
- **Favorite state is carried by the ToggleButton's checked visuals** (via
  `FavoriteHeart`), not by painting the glyph — the shipped Fluent Regular font
  has no filled heart, and both hand-painting attempts made it invisible on
  console.
- **Favorites is the one list that removes items on unstar** — everywhere else
  a toggled row repaints its heart and stays. There the row still belongs in
  the list; on Favorites the list *is* the starred set. All three tabs behave
  alike (song rows, artist cards, album cards); unstarring elsewhere in the
  app shows up here on the next navigation (this page refetches, unlike
  Search).
- **Favorites "Play All" on the Artists tab is the app's heaviest fan-out**:
  a getArtist per favorite artist plus a getAlbum per album of every one of
  them. It is bounded to 4 concurrent by flattening all artists' albums into
  ONE list (nesting two bounded fan-outs would multiply their limits), so a
  large favorites library trades wall-clock time for not hammering the
  server. The actions and the sort control disable behind a ring while it
  runs. Accepted for now — if it proves too slow on device the fallback is to
  cap the pool or drop Play All from the Artists tab specifically.
- **No per-card "Shuffle" in Favorites menus** — no card menu anywhere in the
  app has one, and adding it on this page alone would be inconsistent. It is
  an app-wide change if ever wanted.
- **Search page is navigation-cached** (the one refetch-on-navigation exception),
  so starred markers on kept results can lag until the next search. While the
  query box has focus, LB/RB move the caret instead of jumping regions.

**Background memory (§11):**

- **CORRECTION (2026-08-12, third console pass): releasing the view frees
  almost nothing, and the app's memory is NATIVE, not managed.** Measured on
  device at `EnteredBackground`: usage 236 MB, **managed heap 2 MB**, private
  commit 171 MB. Dropping the whole visual tree moved private commit from 171
  to 172 MB across three runs, with the finalizer drain and
  `DisconnectChildrenRecursive` both in place. So the Songs page's song
  library — the allocation the original diagnosis blamed — is not the problem;
  at 2 MB of managed heap, *nothing of ours* is. The view release is retained
  (it is correct, cheap and reclaims what it can) but it is NOT the fix.
- **The real lever is the peak footprint, not the release.** The app launches
  at 28 MB and reaches 178–270 MB after under a minute of browsing. Observed
  outcomes: **killed at 270 MB** (no `Suspending` line, fresh launch 26 s
  later), **survived at 178 MB** for 36 s. Xbox tolerates a moderate overage
  and kills a large one — which is exactly the reported "sometimes it keeps
  playing".
- **Artwork is ~27 MB — real, fixed, and not the cause** (measured 2026-08-13
  with the "Run without artwork" switch). Same build, same 228-track queue:
  `EnteredBackground` 156 MB with artwork, 129 MB without. With artwork fully
  suppressed the app still reached 290 MB and was killed, so no amount of
  artwork work would have solved this. Do not re-open artwork as the suspect
  without a measurement that contradicts this one.
- **Opening the expanded queue is free (0–1 MB), and the 2026-08-12 claim that
  it cost ~49 MB is RETRACTED.** That figure was everything that happened in
  the eight seconds after the overlay opened, attributed to the overlay. The
  `QueueOverlayOpening` → `QueueOverlayContentReady` pair now brackets it
  properly and reads 0–1 MB on every console run.
- **The cost is scrolling the queue and jumping to a track**, isolated by its
  own control run (2026-08-13): sitting in the open overlay for 27 s costs
  **+7 MB**; scrolling 228 rows and jumping costs **+91 MB** with artwork and
  **+166 MB** without. That is the reported symptom exactly — a long queue
  plays in the background indefinitely, and dies after you go back and pick
  another track. **Split by two further runs the same day** — both mechanisms
  are real and one is three times the other:
  - **scrolling** all 228 rows costs **+13 MB** (artwork off) to **+18 MB**
    (artwork on), and does not come back when the overlay closes. Secondary;
    the fix would be in the row template (each row carries a `SpinningVinyl`
    and a four-item `MenuFlyout`). Not addressed yet.
  - **every stream OPEN** costs **+25 to +41 MB**, cumulative and never
    returned — see the next note, which is the real bug.
- **CORRECTION (2026-08-13, clean runs): the per-open memory is TRANSIENT.**
  It drains a few seconds after track churn stops — ten rapid changes climbed
  to **283 MB** and were back at **182 MB** 2.5 s after the last one, with no
  queue replacement and no user action. Nothing is leaking. The notes below
  about "cumulative, never returned" describe the spike, not a leak, and the
  "it plateaus, so a windowed list is the fix" reading is **retracted**: it
  compared a clean 8-track run against a 228-track run taken on the build with
  the (reverted) item recycling, which inflated it. The two CLEAN runs, 8
  tracks and 22 tracks, both peak at 270–285 MB — **queue size does not drive
  the peak, churn rate does**. Replacing the queue does free memory (182 → 169
  MB), so that precondition passes; it is just no longer relevant.
- **128 MB is a threshold the OS weighs, not a line it enforces — but the
  tolerance runs out between ~180 and ~200 MB.** Observations, all 2026-08-13:
  survived **133–143 MB** for 5+ minutes still advancing tracks; survived
  **178 MB** for 36 s; **killed at ~201–214 MB**, and at 259, 270, 290 and
  447 MB. So the working target is **stay under ~180 MB**, not under 128 — and
  the minimal case (143 MB) already is, while a browsed session with a long
  queue (201 MB) is not.
- **The app-owned cover cache holds and drops covers, but dropping them did
  not move `AppMemoryUsage`** (first console pass, 2026-08-13):
  `AfterDropCovers` reported **11 MB released** and usage stayed at 201 MB —
  through the subsequent `GC.Collect`, `WaitForPendingFinalizers` and the
  visual-tree release. Two candidate explanations, undistinguished: the reading
  is taken microseconds after the references are dropped, or native commit is
  simply never returned to the OS (which would explain every zero-reclaim
  reading in this whole investigation). Note also that 11 MB is small — even a
  perfect release moves 201 to 190. **Do not treat the cache as a proven win
  until a delayed post-drop reading says the megabytes materialise.**
- **A 200-track queue costs roughly 16–26 MB in items alone**, before any of
  them is opened: `RebuildList` materializes a `MediaPlaybackItem`, a
  `MediaSource` and a full `ApplyDisplayProperties` (including an SMTC
  thumbnail reference) per track. Measured indirectly — queue 14 → 228 cost
  ~17 MB, and a 10 → 200 change cost ~68 MB alongside browsing. This scales
  linearly with queue length, which is the exact shape of the reported bug, and
  it is NOT the same thing as the (retracted) claim about opened items'
  buffers. Two candidate fixes, neither measured yet: a windowed list, or
  applying display properties only to the current and next items.
- **The minimal floor is 143 MB — 15 MB over the limit** (measured 2026-08-13:
  fresh launch, play with no browsing, 32 s untouched, stable and drained).
  Composition: ~29 MB process baseline, ~28 MB shell, ~46 MB reaching a playing
  track, ~42 MB playback. Browsing adds ~40–50 MB on top, which is how normal
  sessions reach 183–194 MB. Artwork is **14 MB** of the minimal case
  (143 vs 129 MB, on vs suppressed) and **~27 MB** of a browsed one (156 vs
  129) — it scales with how much you browsed, which is exactly why the
  app-owned cache is worth having: it gives back more the more you browsed. Decoded images live in
  XAML's process-wide URI-keyed cache, **not in the visual tree**, which is
  precisely why dropping the tree has reclaimed 0 MB in every console run since
  this began. Only a cache the app owns can be dropped on demand.
- **The real problem is the resting FLOOR, and it is the same number this
  investigation started from.** With a queue playing and everything settled the
  app rests at **182–194 MB against a 128 MB limit**. One clean run's
  breakdown: 28 MB at launch, 57 MB with the shell up, 140 MB after browsing to
  a playlist, 183 MB with one track playing — so browsing costs ~83 MB and
  playback ~43 MB, and the view release reclaims essentially none of it. Two
  failure modes follow: the floor (~55 MB over at all times, so backgrounding
  is never safe) and the transient (backgrounding within a few seconds of a
  track change measures the app mid-spike at 250–285 MB). The ~2 s the OS
  allows after `AppMemoryUsageLimitChanging` is **shorter than the drain**, so
  the app is measured during it.
- **The memory is spent when a stream OPENS, not when the jump happens**
  (2026-08-13). `AfterJumpMoveTo` costs 0 MB and even
  returns ~10 MB, so `MaxPlayedItemsToKeepOpen = 0` genuinely works; the cost
  lands at the settled reading 2.5 s later (+41, +25, +37 MB across three
  jumps: 161 → 224 → 240 → 268 MB, then killed at 259). **Codec is not the
  driver** — an m4a 256 kbps jump cost more than a FLAC one. The reason nothing
  ever came back: `RebuildList` materializes a `MediaPlaybackItem` and a
  `MediaSource` for EVERY track and they live in `_list.Items` for the life of
  the queue, so they are never garbage — which is exactly why `GC.Collect` +
  `WaitForPendingFinalizers` reclaimed **0 MB** on every console run since this
  began — but see the correction above: the spike drains on its own, so this
  describes what is charged during churn, not a permanent leak.
- **Recycling retired items was tried as the fix and REVERTED** (2026-08-13,
  one console pass). It made things worse on both axes: peak **465 MB, killed
  at 447** against 268/259 for the same exercise without it, plus multi-second
  stalls. Two mechanisms, both worth remembering:
  - **Replacing an item in `_list.Items` fires a second, spurious
    `CurrentItemChanged`** — visible in the log as paired `TrackChanged` lines
    where the first has no track (the replacement was not in the map yet). Each
    spurious change ran another recycle, created another item and opened
    another stream, so the "fix" added allocations.
  - **`MoveTo` stops taking effect while the list is being mutated.** Four
    consecutive jumps produced no track change at all and usage sat frozen for
    30 s. That is the "it takes 10+ seconds for the track to start" symptom.
  In-place item replacement is therefore safe **only** where `RetryWithMp3`
  uses it — a failed item, where playback has already moved on and a stall is
  invisible. Do not reach for it as a routine operation.
- **Natural advances cost exactly what jumps cost** (2026-08-13): the session's
  very first track, with no interaction at all, went 109 → 152 MB (**+43 MB**),
  and later unattended advances cost +61 and +30 MB. So this was never a
  jumping bug — jumping just reaches the ceiling faster — and a long queue
  playing in the background has always been accumulating. "200 tracks in the
  background is fine" only meant it had not changed tracks enough times yet.
- **Every cover-art URL used to be unique, defeating both platform caches**
  (found 2026-08-13). `GetCoverArtUri` goes through the same request builder
  as every endpoint, and token auth mints a **fresh random salt per call**, so
  the same cover at the same size produced a different URL every time. XAML's
  decoded-image cache is URI-keyed and process-wide and the HTTP cache is
  URL-keyed, so neither could ever hit: every appearance of a cover was a fresh
  download and decode, and the image cache accumulated an entry per request in
  native memory the app does not own. This is the best explanation yet for the
  three facts nothing else fit — the footprint is native, the tree release
  reclaims nothing, and browsing raises the floor. **`Services/CoverArt` is now
  the only place the app builds a cover URL**; do not call `GetCoverArtUri`
  directly again, and be aware that any *other* endpoint whose URL is used as a
  cache key has the same hazard by construction.
- **Cover requests round onto a four-step ladder** (100 / 220 / 320 / 400) so
  one cover is one cached image across surfaces; before this the app asked for
  a single cover at six distinct sizes (84, 300, 320, 400, 440, 640). Raising a display box above its tier
  silently doubles that cover's cached copies — change the tier, not just the
  box. Decode size remains a separate concern and still equals the display box.
- **The "Run without artwork" toggle on Account is scaffolding**, not a
  feature: it is how the artwork-vs-floor question gets measured, and it should
  be removed once answered.
- **Artwork was decoded at 2× its display size everywhere** (200-epx tiles at
  400, 56-epx rows at 112, 320-epx headers at 640, the 220-epx carousel at
  440), costing 4× the memory for no visible gain — layout scaling is
  disabled, so the app is 1 epx to 1 physical pixel at 1080p. Halved
  everywhere 2026-08-12. Decode sizes must stay equal to the display box; a
  future "make it sharper" change costs four times what it looks like.
  Footnote (2026-08-13): that round halved the DECODE sizes but left the
  server-side `size=` request at the old doubled values, so covers kept
  downloading at 4× the area needed for another day. Request and decode are
  two separate numbers and both have to be right.
- **High-bitrate FLAC is a secondary cost, not the driver.** A FLAC session
  measured 236 MB, inside the 178–270 MB range already seen with 256 kbps
  m4a; the variance tracks browsing, not codec. **Confirmed directly
  2026-08-13**: with each stream open individually measured, an m4a 256 kbps
  jump cost +37 MB and a FLAC 1218 kbps jump +25 MB. The per-open cost is not
  a function of the codec, so do not reach for bitrate as an explanation.
  Open `MediaPlaybackItem`s do
  hold native demux/decode buffers that are several times larger for FLAC,
  which is why **`MaxPlayedItemsToKeepOpen` is now 0 always** (2026-08-13),
  not only while backgrounded: at 2, a jump left the track you came from open
  on top of the new one and its prefetched successor. It buys a marginally
  faster Previous, which is the wrong trade against a 128 MB limit — but note
  this is a candidate fix under test, not a confirmed one (backlog item 65).
  The background-pressure path still sets 0 explicitly, so raising the
  constant again cannot silently un-fix the background case.

- **The app deliberately destroys its own UI while backgrounded.** Xbox drops
  the memory limit to 128 MB with ~2 s to comply, so under pressure
  `App.ReduceMemoryUsage` sets `Window.Current.Content = null` and
  `LeavingBackground` rebuilds via `CreateRootFrame`. Consequences that are
  intended, not bugs: **returning from a long backgrounding lands on Home**,
  losing navigation position and the loaded song library; the first frame back
  costs a page rebuild. Playback is never interrupted — `PlaybackService` and
  its `MediaPlayer` are process-wide and reference no visual element.
- **This works only because pages unregister in `Unloaded`.** One static
  holding a reference to a page (the `Toast` registration and
  `NavigationPreferences.Changed` are the two live examples, both dropped in
  `MainPage.OnUnloaded`) would keep the whole tree charged to the app and turn
  the release into a silent no-op. Any new static that captures UI must be
  unregistered the same way, or background playback regresses with no
  visible symptom until a console pass.
- **The reduction threshold is 75 % of the new limit**, not the limit itself:
  `AppMemoryUsage` lags what the graphics/XAML layers charge back, so
  "just under" is not safe. Releasing unnecessarily costs one rebuild;
  cutting it fine costs the process.
- **Bitrate is not the background-termination mechanism** (investigated
  2026-08-12). `MediaPlayer` streaming a network URI is designed to keep
  retrieving media in the background, and decoding happens in the media
  pipeline rather than on the app heap. The manifest capability, the
  process-wide player and `CommandManager.IsEnabled` were all already correct
  — do not "fix" them again.
- **This bug is invisible under the debugger.** Xbox does not apply background
  memory limits to a debugged process, so an F5 run always looks healthy.
  Verification requires a non-debug launch plus
  `background-diagnostics.log` from LocalFolder.
- **`Window.Current` is thread-static, and the memory events are not on the UI
  thread.** `MemoryManager` raises `AppMemoryUsageLimitChanging` /
  `AppMemoryUsageIncreased` on a threadpool thread, where `Window.Current` is
  **null** — so touching it directly there reads as "no view to release" and
  silently does nothing. This is exactly how the first version of the fix
  failed on device (2026-08-12 log: the 128 MB transition fired at 161 MB
  usage, no release, no change). The release and the `LeavingBackground`
  rebuild are both marshalled through a `CoreDispatcher` captured in
  `CreateRootFrame`. Microsoft's own sample writes
  `Window.Current.Content = null` inline in that handler without mentioning
  thread affinity — do not "simplify" back toward it.
- **The release blocks up to 1200 ms** waiting for the UI thread. Firing it
  asynchronously would return before the tree was actually dropped and race
  the OS's measurement, which lands within ~2 s of the limit change.
- **The log distinguishes "released but reclaimed little" from "never
  released"** via the `AfterViewRelease` line. The first console pass had no
  such line and the two cases were indistinguishable — which cost a
  device round trip.

**Shell / focus / theming:**

- **Content owns focus** (`ContentFocus`): every page moves focus into itself on
  reveal, because Frame navigation otherwise strands the controller in the nav
  pane. Trade-off: a slow page completing while the user browses the nav pane
  pulls focus into the content. Accepted for v1.
- **Settings/Account are ordinary menu items** at the bottom of the list (the
  PaneFooter was a gamepad focus dead zone) — not pinned to the pane bottom edge.
- **Horizontal segmented rows need explicit XYFocusLeft/Right pins.** The XY
  engine will step out of a tab/filter row into the nav pane rather than to the
  adjacent button (found on Favorites' tabs, 2026-07-27 — the same class as the
  now-playing bar's transport chain and the browse views' XYFocusUp pins). Only
  the row's FIRST button should fall through to the pane. Both segmented rows in
  the app are pinned this way: **Favorites' Artists/Albums/Songs tabs** and
  **Search's All/Artists/Albums/Songs scope filters**. Any future segmented row
  needs the same treatment — the default behavior is the bug. The same applies
  to whatever sits to the RIGHT of such a row: Favorites' sort / Play All /
  Shuffle All cluster pins leftward too (Shuffle All → Play All → Sort → the
  active tab), and **Sort's target is set in code** because it follows the
  showing tab. The three disable together, so a leftward pin can never point at
  a disabled control while its source is focusable — the reason Songs' Right
  into the cluster stays unpinned.
- **TV-safe inset:** shell content sits inside `ShellRoot` Padding 32,20;
  backgrounds paint edge-to-edge. Overscan-free displays show a uniform
  background border (intended §2 trade-off).
- **Automatic theme live-follow is console-verified** —
  `UISettings.ColorValuesChanged` fires on Xbox. `FavoriteHeart` reads
  `SystemAccentColor` once at construction (accent change repaints only after a
  page/control rebuild).
- **The light theme is not a tinted copy of the dark one** (2026-07-27): its
  ground is warm parchment rather than white, its chrome surface is deepened
  far enough to read as a card on that ground, and its primary fill is the deep
  teal accent — NOT the aged tan the "cream" role maps to elsewhere. The tan
  survives only in the card frames and the motif's first stripe. Reverting any
  of the three reintroduces the washed-out, flat look Rafael reported.
- **Pages inside the shell paint no background of their own** — they are
  `Background="Transparent"` and MainPage paints `RetroPageBackgroundBrush`
  once, so the record-groove watermark can sit under the content frame.
  SetupPage and the expanded-queue overlay stay opaque by necessity (neither
  is inside the content frame). Consequence to accept: a Frame navigation
  transition can briefly show two pages through each other.
- **The app mark has exactly one home: the nav pane header** (§13.6). Adding it
  to page headers, the play bar, or over content is an explicit non-goal — at
  10 ft a repeated logo is noise. The wordmark beside it is real text, not
  pixels in a scaled logo, which is why it survives at 10 ft.
- **Keeping the chrome dark in light mode was considered and rejected** — see
  §16 (2026-07-27). It needs the OS `NavigationViewItemForeground*` state
  family to be as override-reachable as the pane-background brushes were, and a
  wrong bet ships black text on dark teal that only a console pass would catch.
- **Retro accent override reach** is the open device question: framework brushes
  composed from `SystemAccentColor` at generic.xaml scope may not follow the
  app-level override — any surface still showing the console accent gets its own
  brush-key override next round.
- **Nav icon sizing** (`EnlargeNavIcons`) depends on the OS template's Viewbox
  being named "IconBox" (true on 26100 SDK; a future OS rename fails soft to
  small icons).

**Browse / detail:**

- **Artist list totals come from the album list, never per artist** (§7.1,
  2026-07-27): getArtists returns only `albumCount` and `starred`, so the rows'
  track count and total play time are summed by `Core/Browsing/ArtistTotals`
  from one getAlbumList2 pass (500/request) that runs after the rows are on
  screen. Cost is ceil(albums / 500) requests regardless of artist count.
  **Do not "simplify" this into a getArtist per row:** measured on Navidrome
  0.63.2, a warm getArtist is ~16–20 ms on LAN (so ~2.5 s for 500 artists) but
  ~19 s at a 150 ms RTT — the off-LAN case is what rules it out.
  Accepted trade-offs, each deliberate:
  - Totals appear only when the pass COMPLETES; a per-page fill would flash an
    artist's partial sum, and a wrong number is worse than the album count the
    row already shows honestly.
  - A failure or a mid-pass navigation applies nothing and says nothing — the
    rows keep their (correct) album counts. Not a §15 silent degrade: the page
    the user asked for loaded, and nothing requested went missing.
  - The figures are **album-artist sums** (a guest appearance counts under the
    album's artist) — the grouping getArtists itself indexes by, so they agree
    with the album count beside them and with artist detail's own sum. Albums
    the server sent without an `artistId` are skipped, not attributed.
  - Only the sums are retained, not the album pages, so memory is one small
    entry per artist (§11) — but the pass does read the whole album list, which
    is the thing to watch on a large library during the console pass.
  - The Favorites Artists tab does NOT show totals: it draws a different,
    usually much smaller set from getStarred2 and would need its own pass.
  Albums rows still look richer without any of this, because getAlbumList2
  happens to return year/genre/songCount/duration per album for free.
- **Playlist rows show owner + description, and never a heart** (§7.4,
  2026-07-27): star/unstar has no playlist form, so an empty heart column would
  imply a favorite state that does not exist. `created`/`changed` stay unparsed
  in the client — a "last updated" caption was judged weak information for a
  read-only browse list, not an oversight.
- **Songs order is server-defined by default** (search3 has no server-side
  sort); the Title/Artist/Album/Date Added options reorder client-side
  (§16 2026-07-26). Column widths are a first guess pending the 10-ft check.
- **Multi-disc albums render as one flat list** ordered disc→track; no disc
  headers yet.
- **Playlists are read-only** (browse + detail/playback; no create/edit/delete).
  No playlist favorite heart (Subsonic star/unstar has no playlist form). A
  playlist with the same song twice marks both rows while it plays (jump is
  index-based).
- **Composite playlist artwork is a live XAML layout** (ImageBrush-filled grid,
  center-cropping), not a rendered bitmap; server playlist coverArt is
  deliberately not used (§7.4 requires client-side composition). Tile artwork
  loads lazily per tile (bounded to 4 in flight); a per-tile failure degrades to
  the placeholder.
- **Carousel/grid focus is plain XY navigation** (no focus engagement).

**Conventions:**

- `PlaybackService.Instance` must be first touched on the UI thread (it captures
  that dispatcher) — a convention, not a runtime check.
- Hiding the currently open section from Settings leaves its content shown until
  the next selection.

## Code review notes (2026-07-23)

A review ahead of concluding the app found the code clean and disciplined
(balanced event subscriptions, stopped timers, documented threading, gated
capabilities). Latent items worth a future pass, none observed failing on
device:

- **DONE 2026-08-13 — `PlaybackService` item maps were read on the media
  thread** (`OnCurrentItemChanged`/`OnItemFailed`) while mutated on the UI
  thread, which a plain `Dictionary<>` does not support. Now guarded by
  `_mapGate` behind `MapItem`/`TryGetSong`/`TryGetItem`/`RemapItem`/
  `ClearMaps`. Item recycling rewrites both maps on every track change, so the
  window this review called tiny stopped being tiny.
- **STILL OPEN — `MediaPlaybackList` items aren't explicitly disposed.** This
  is not a tidiness item: the items are never garbage in the first place (they
  live in `_list.Items` for the life of the queue), so the GC can never reclaim
  them and every stream open leaks 30–45 MB. Disposing them by recycling the
  retired item was tried on 2026-08-13 and reverted — it destabilizes the list
  (see the §11 notes above). The remaining question is where the per-open
  memory actually goes; backlog item 66 splits it.
- **Cleanup done this pass:** removed the now-dead `SectionPage` (every section
  has a real page; it survived only as an unreachable routing fallback);
  refreshed `README.md` to the finished state.

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
