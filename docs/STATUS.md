# Current Status

Last updated: 2026-07-27 (browse list rows + view-toggle restyle, on top of the
Favorites and Songs-sorting rounds; the file was condensed to a clean
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
  starred marker (getArtists carries nothing more — per-artist totals would
  cost one request per row, so they moved to artist detail); artist detail's
  counts line gained total play time + the albums' year span and album detail
  gained genre; the "Artist" sort option's mic glyph became the person glyph
  in both sort menus. Playlists list rows carry owner + the playlist's own
  description and tracks · total play time (no starred marker — playlists have
  no favorite state). Duration wording moved to Core `Browsing/DurationText`
  (+7 tests). Not yet device-verified (backlog items 38–43).
- **Now-playing-bar spinning vinyl artwork** — console-verified (PR #8).
- **Nav / play-bar shortcuts + alignment** — View-button jump to the bar, LB/RB
  direct region jumps, pane/bar bottom alignment. Not yet device-verified.
- Recent tweaks: default theme changed to **Dark**; left-nav **chevron** pane
  toggle enlarged. (See `git log` for per-commit detail.)

**Build/test state:** builds green (x64 Debug over SSH; the `APPX0703` appx
packaging error is the known command-line-only failure — VS F5 packages fine).
**92 Core + 80 SubsonicClient tests pass** (9 SubsonicClient live-server tests
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
41. **Artists list rows:** name on the left, starred heart, album count on the
    right — the same row geometry as Albums. (Song count / play time are
    deliberately absent here; see the known-gaps note.)
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

*(Future work: append console items here.)*

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

**Shell / focus / theming:**

- **Content owns focus** (`ContentFocus`): every page moves focus into itself on
  reveal, because Frame navigation otherwise strands the controller in the nav
  pane. Trade-off: a slow page completing while the user browses the nav pane
  pulls focus into the content. Accepted for v1.
- **Settings/Account are ordinary menu items** at the bottom of the list (the
  PaneFooter was a gamepad focus dead zone) — not pinned to the pane bottom edge.
- **TV-safe inset:** shell content sits inside `ShellRoot` Padding 32,20;
  backgrounds paint edge-to-edge. Overscan-free displays show a uniform
  background border (intended §2 trade-off).
- **Automatic theme live-follow is console-verified** —
  `UISettings.ColorValuesChanged` fires on Xbox. `FavoriteHeart` reads
  `SystemAccentColor` once at construction (accent change repaints only after a
  page/control rebuild).
- **Retro accent override reach** is the open device question: framework brushes
  composed from `SystemAccentColor` at generic.xaml scope may not follow the
  app-level override — any surface still showing the console accent gets its own
  brush-key override next round.
- **Nav icon sizing** (`EnlargeNavIcons`) depends on the OS template's Viewbox
  being named "IconBox" (true on 26100 SDK; a future OS rename fails soft to
  small icons).

**Browse / detail:**

- **Artists list rows carry less than Albums rows, by design** (§7.1,
  2026-07-27): getArtists returns only `albumCount` and `starred`, so an
  artist's song count and total play time would cost one getArtist per visible
  row — rejected on the same perf grounds that bounded the §8 fan-out. Those
  totals live on the artist detail page, where the albums are already loaded.
  Albums rows look richer only because getAlbumList2 happens to return
  year/genre/songCount/duration for free.
  Measured on Navidrome 0.63.2 (2026-07-27): a warm getArtist is ~16–20 ms on
  LAN, so per-row totals are ~2.5 s for 500 artists on LAN but ~19 s at a
  150 ms RTT — the off-LAN case is what kills it. **If those totals are ever
  wanted here, don't add the per-row fetch:** sum getAlbumList2 pages by
  `artistId` instead (every album carries artistId + songCount + duration), which
  costs ceil(albums / 500) requests regardless of artist count and reuses the
  paging the Albums page already does. The sums would be album-artist sums —
  the same grouping getArtists indexes by, so consistent with the albumCount
  beside them — and rows would show instantly with the totals filling in behind.
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

- **`PlaybackService` item maps (`_songToItem`/`_itemToSong`) are read on the
  media thread** (`OnCurrentItemChanged`/`OnItemFailed`) while mutated on the UI
  thread — a plain `Dictionary<>` is not safe for concurrent read+write. Tiny
  window; `_retriedSongs` already uses `_retryGate` for exactly this. Cheapest
  fix: guard those map reads/writes with the same lock.
- **`MediaPlaybackList`/items aren't explicitly disposed on rebuild/stop** —
  GC reclaims them; explicit disposal would return decoder buffers
  deterministically under the Xbox memory budget.
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
