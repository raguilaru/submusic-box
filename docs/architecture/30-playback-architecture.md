# Playback & Queue Architecture (Slices 10–11)

Written 2026-07-15, before implementation. Scope and the four decisions below
were confirmed with Rafael at the start of the Slice 10 session. The Slice 11
expanded-queue section at the end was added 2026-07-16, its four decisions
likewise confirmed up front.

## Scope of this slice

**In:** the queue infrastructure (Core `PlayQueue`), the rewritten
`PlaybackService` (real streaming, SMTC, background audio), the persistent
now-playing bar (§10 compact form), and the retrofit wiring that was deferred
onto Slice 10: artist detail Play/Shuffle (06b), album detail Play/Shuffle +
jump-to-track + current-track highlight (07b), Songs row playback + context-menu
Play (Slice 08 row action), and sign-out stopping playback.

**Out (unchanged deferrals):** the expanded two-pane queue view + Y-button
mapping (Slice 11 — the bar's queue-expand control ships disabled with a
visible note, §15); queue persistence / resume state + Home resume card
(Slice 05b — the model carries everything persistence needs, nothing is
written to disk yet); playlist detail/playback (later slice; playlist tiles
stay inert); seek/scrub in the compact bar (not required by §10).

## Decisions (recorded also in requirements §16)

1. **Engine: Core-owned queue mirrored into a `MediaPlaybackList`.**
   The pure queue logic (order, shuffle mapping, repeat state, replace
   semantics) lives in `BoxSubMusic.Core.Playback.PlayQueue`, unit-tested
   without a UWP host. `PlaybackService` mirrors it into a
   `MediaPlaybackList`, which buys gapless playback + prefetch of the next
   track (no audible gap while the next stream URL is opened) and native SMTC
   next/previous via `MediaPlayer.CommandManager` — the pattern Microsoft's
   background-audio samples use. The alternative (set `Player.Source` per
   track, advance on `MediaEnded`) was rejected: simpler, but every track
   boundary would stall on network fetch.
2. **Queue persistence deferred to Slice 05b.** `PlayQueue` keeps full track
   references + context metadata (display name, cover art id) so §6.1
   persistence bolts on without model changes.
3. **Scrobbling is in.** Without play reporting the server's play counts —
   and therefore the Home "Most Played" carousel — never reflect listening
   done in this app. Convention: submit `scrobble` (`submission=true`) once a
   track passes 50% of its duration or 4 minutes, whichever comes first,
   once per queue advance. Fire-and-forget; failures are silent (telemetry,
   not UX). `scrobble` is core Subsonic API (since 1.5.0), no capability gate
   needed.
4. **Songs row queue semantics:** A/Enter on a Songs row replaces the queue
   with **the rows as currently shown**, the clicked song as the current
   track. Revised 2026-07-26 (§16): the page now loads the whole library up
   front so it can sort client-side, so this normally means every song, in
   the active sort order. The original "all rows loaded so far" wording
   (2026-07-15, when the page paged incrementally on scroll) still describes
   what happens while that load is running. No page is ever auto-fetched
   *because of* a play action. The same rule governs scoped Search results,
   which do still page incrementally.

## Core: `BoxSubMusic.Core/Playback/`

Core references SubsonicClient (dependency rule App → Core → SubsonicClient),
so the queue holds `Song` models directly — no duplicate track type.

- **`RepeatMode`** — `Off`, `All`, `One`.
- **`QueueContext`** — originating-context metadata for the future resume
  card (§6.1): display name + optional cover art id (e.g. "Album · Abbey
  Road"). Carried, not yet persisted.
- **`PlayQueue`** — pure, event-free-threading (UI-thread-only like the rest
  of the app), raising `Changed` for mirrors:
  - `Replace(tracks, startIndex, shuffle, context)` — §10: every Play action
    replaces, never appends. `shuffle: true` puts the start track first and
    randomizes the rest; `shuffle: false` keeps the given order and starts at
    `startIndex`.
  - `Tracks` (original order), `PlayOrder` (current order — differs from
    `Tracks` only when shuffled), `CurrentIndex` (index into `PlayOrder`),
    `CurrentTrack`.
  - `SetShuffle(bool)` mid-play: on = current track keeps playing, upcoming
    order randomizes (current pinned first in the new order); off = original
    order restored, index re-pointed at the current track.
  - `CycleRepeat()` — Off → All → One → Off.
  - `NotifyAdvanced(track)` — index sync *from* the player mirror (the
    `MediaPlaybackList` advances itself on track end and on SMTC
    next/previous; the queue follows).

## SubsonicClient additions

- `GetStreamUri(songId)` — authenticated `/rest/stream?id=…` URL builder,
  same shape as `GetCoverArtUri`. No `format`/`maxBitRate` params in v1: the
  server's own player defaults decide (Navidrome streams raw by default).
  Known gap: if a server is configured to transcode, the bar's format/bitrate
  labels (tag metadata) describe the file, not the transcoded stream.
- `ScrobbleAsync(id, submission)` — the play-count report described above.

## App: `PlaybackService` (rewritten)

Single app-lifetime `MediaPlayer` (the Xbox background-audio pattern,
unchanged from the Slice 02 spike) — the spike's test tone and
`IsLoopingEnabled = true` go away.

- `Play(tracks, startIndex, shuffle, context)` → `PlayQueue.Replace` + mirror
  rebuild: one `MediaPlaybackItem` per song
  (`MediaSource.CreateFromUri(GetStreamUri(id))`), display properties set per
  item (title/artist/album + `RandomAccessStreamReference` cover thumbnail) so
  SMTC metadata is always current. `MaxPlayedItemsToKeepOpen = 2` bounds
  memory (Xbox background budget).
- The list always holds items in **original order**; shuffle is expressed via
  `ShuffleEnabled` + `SetShuffledItems(playOrder)` (min version 10.0.17134 <
  our 10.0.19041 floor), so toggling shuffle never interrupts the current
  track.
- `CurrentItemChanged` → `PlayQueue.NotifyAdvanced` + `CurrentTrackChanged`
  event for the UI. Repeat: `AutoRepeatEnabled` mirrors `RepeatMode.All`;
  `RepeatMode.One` is handled by re-`MoveTo`-ing the just-ended item when the
  change reason is `EndOfStream` (the list has no native repeat-one).
- `ItemFailed` → skip forward, guarded by a consecutive-failure counter (≥
  queue length ⇒ stop) so an unreachable server can't loop forever.
- Scrobble threshold checked on `PlaybackSession.PositionChanged` (media
  thread, cheap flag check — works while backgrounded, unlike a UI timer).
- `TogglePlayPause`, `Next`, `Previous`, `SetShuffle`, `CycleRepeat`,
  `Stop()` (sign-out: clears queue + source, hides the bar).
- UI-facing events marshal via the control's dispatcher on the consuming
  side, not inside the service.

## Now-playing bar (§10 compact form)

`Controls/NowPlayingBar` (UserControl), hosted in `MainPage` — the
NavigationView's content becomes a two-row grid: content `Frame` (`*`) above
the bar (`Auto`). Its own layout row — not an overlay — so list bottoms are
never hidden behind it; "floating" is visual (margins + corner radius +
elevated background), which §10's "persistent floating bar" allows while
keeping 10-ft legibility honest.

Layout (1920-epx canvas, ~112 epx tall + 16 epx margins):

```
┌───────────────────────────────────────────────────────────────────────┐
│ ▓▓ 80×80  Title (20 semibold)         ⤨  ⏮  (⏯ 64px)  ⏭  🔁      ☰   │
│ ▓▓ art    Artist · Album (16, 70%)                                    │
│           FLAC · 1043 kbps            1:23 ────────────────── 4:05    │
└───────────────────────────────────────────────────────────────────────┘
```

- Left: artwork thumb (80 epx, rounded, music-note placeholder), title,
  artist · album, format · bitrate — all §10-required metadata.
- Center: shuffle (ToggleButton EF37), previous F629, play/pause F606/F5A2
  (larger, visually primary), next F56A, repeat (cycling glyph F175 off /
  F172 all / EF34 one, off state dimmed). All glyph codepoints verified
  against the shipped `FluentSystemIcons-Regular.ttf` cmap (§13.3).
- Right: elapsed / thin progress bar (non-interactive in v1) / total, then
  the queue-expand button (F4EE) — present per §10 but disabled with a
  tooltip + a note in STATUS until Slice 11 (§15: visible degradation).
- Reveal: collapsed until the first `Play`; slides up (translate + fade,
  ~300 ms decelerate) and then persists for the app session, paused or
  playing; `Stop()` (sign-out) hides it again.
- Position/elapsed display: `DispatcherTimer` at 500 ms, running only while
  playing (display only — scrobbling does not depend on it).
- Focus: the bar is below the page in the same XY region, so D-pad down from
  page content reaches it naturally; theme-aware brushes only (§13.2), no
  custom focus visuals.
- Current-track highlight: `TrackRow` becomes observable (`IsCurrent`), album
  detail subscribes to `CurrentTrackChanged` and matches song ids (§9).

## Retrofit inventory (found 2026-07-15, all in this slice unless noted)

| Where | Change |
|---|---|
| `ArtistDetailPage` (xaml + cs) | Enable Play/Shuffle: pool = `getAlbum` per album (parallel), albums ordered oldest-first (year ascending, null-year last, then name), tracks in disc/track order (§8). Remove "Playback arrives in a later update" note; drop the `XYFocusDown` pin on `FavoriteToggle` (it exists only because Play/Shuffle were unfocusable). |
| `AlbumDetailPage` (xaml + cs) | Enable Play/Shuffle (album tracks only, §9); track rows: `IsItemClickEnabled` + click = replace queue with album from that track; context-menu "Play (arrives in a later update)" becomes live "Play"; current-track highlight; remove note + `XYFocusDown` pin. |
| `SongsPage` (xaml + cs) | Rows live: A/Enter = queue ← loaded rows, start at clicked (decision 4); context-menu "Play" live; header note reduced to the Menu-button hint. `SongRow` carries its `Song`. |
| `MainPage` (xaml + cs) | Content becomes Grid(Frame, NowPlayingBar); nothing else changes (B-back, nav resync untouched). |
| `AccountPage` | Sign out additionally calls `PlaybackService.Stop()` — the bar hides, the queue clears, no audio survives into the setup page. |
| `PlaybackService` | Spike replaced wholesale (no other code references it); `Assets/TestTone.wav` removed from the project. |
| `PlaylistsPage` | **No change** — tiles stay inert; its note ("opening a playlist and playback arrive in a later update") is still true. |
| `HomePage` | **No change** — resume card is 05b. |

## Test plan

- **Core.Tests:** replace semantics (order, start index, shuffled start-track
  pinning), SetShuffle on/off round trip (current track stable, original
  order restored), repeat cycling, NotifyAdvanced index sync, empty-queue
  edges.
- **SubsonicClient.Tests:** GetStreamUri shape (auth params, id), scrobble
  request (id + submission) — plus a live Navidrome scrobble round-trip if
  cheap.
- **VM (arm64 F5):** bar reveal/controls with the shared gamepad; known blind
  spots apply (no console mouse-mode class bugs, no OSK).
- **Console checkpoint (Rafael, F5):** background audio with real streams +
  SMTC metadata/controls (§11 was last device-verified with the test tone),
  every entry point (artist/album Play + Shuffle, track jump, Songs row),
  gapless album playback, repeat/shuffle from the bar, 10-ft legibility of
  the bar, sign-out kills audio. Plus the outstanding **Release|x64 .NET
  Native run** folded into this checkpoint.

## Known follow-ups created by this slice

- ~~Slice 11: expanded queue enables the bar's queue button + Y mapping.~~
  Done 2026-07-16 — see the Slice 11 section below.
- Slice 05b: persistence writer + resume card on top of `PlayQueue`.
- Transcoding-aware format/bitrate labels (needs the OpenSubsonic
  `transcoding` extension) — refinement-phase candidate, not v1.
- Artist Play fan-out: one `getAlbum` per album in parallel; fine for typical
  artists, revisit throttling if a 100-album artist stalls on device.

## Slice 11: expanded queue (added 2026-07-16)

§10's expanded state. Four decisions confirmed with Rafael up front:

1. **Presentation: full-screen overlay.** `Controls/ExpandedQueueView` slides
   up over the whole shell (nav pane and bar included) — the TV-app
   convention, and the simplest honest focus story: while open, `MainPage`
   disables the `NavigationView` underneath, so XY focus physically cannot
   leave the overlay (the PaneFooter lesson: never assume the engine skips
   what merely looks unreachable). On `Closed` the shell re-enables and
   focus returns to the bar's queue button (or into the content frame if the
   queue died while open). The shell root became a Grid
   (NavigationView + overlay) to give the overlay its layer.
2. **Queue scope: the whole queue in current play order** (`PlayQueue
   .PlayOrder`), not just up-next — the acceptance criterion is "jump
   directly to ANY queued track", which includes backwards. The current
   track carries the same accent now-playing marker as the album track list
   and is scrolled into view + focused on open.
3. **Row action: jump-only.** A/Enter jumps playback (`PlaybackService
   .JumpTo`); no context menu on queue rows in this slice (refinement-phase
   candidate — the Songs/album-row menu pattern drops in unchanged).
4. **Y mapping: global toggle.** Y anywhere in the shell opens the overlay
   when a queue exists; Y or B inside closes it. Escape is the keyboard
   fallback; a visible chevron button (F2A4, verified against the Regular
   TTF cmap) covers mouse users (§13.1).

Layout (1920×1080 canvas): left pane 520 epx — 400 epx artwork, title,
artist · album, format · bitrate, elapsed/progress/total, and the full
transport cluster (shuffle · previous · play/pause · next · repeat ·
favorite, XYFocus-pinned neighbors like the bar); right pane — "Queue"
header with context name + song count, then the row list (position/marker,
title, starred heart, artist, duration — the album-track visual language).

Mechanics worth remembering:

- **`PlaybackService.JumpTo(Song)`** moves the existing `MediaPlaybackList`
  (no rebuild — shuffle order, repeat state and prefetch survive). The
  platform docs leave `MoveTo`'s index space unstated when `ShuffleEnabled`
  (Items vs ShuffledItems), so JumpTo indexes the shuffled order first and
  verifies against `MoveTo`'s returned item; a mismatch is corrected with
  the `Items` index. Without shuffle the orders coincide, so the first call
  always lands. Jumping to the current track restarts it. **Console-verify
  the shuffle-on jump explicitly.**
- The overlay is a pure view over `PlaybackService.Changed`, like the bar.
  Rows rebuild only when the play-order *sequence* changed (replace/shuffle
  toggle — detected by reference-comparing a snapshot); plain advancement
  just repaints the `IsCurrent`/`IsStarred` markers, so scroll position and
  focus survive track changes.
- `Models/QueueRow` follows the `TrackRow` observable pattern; it holds its
  `Song` reference, which is also the jump target (identity-based — safe
  with duplicate titles).
- Open/close are storyboards (translate + fade, 300/250 ms); `Closed` fires
  after the close animation so the host restores focus exactly once. The
  favorite toggle mirrors the bar's silent-snap-back failure mode and
  repaints the matching row heart in place.

## Slice 05b: queue persistence + resume (added 2026-07-16)

§6.1's local resume contract on top of the Slice 10/11 machinery. One
decision confirmed with Rafael up front: **the Home resume card's Play
starts playback immediately** at the saved track + position ("MUST NOT
auto-play on launch" is satisfied because launch itself restores nothing —
only the card renders; the queue is rebuilt when the user presses Play).
The literal-paused reading (restore paused, press play again on the bar)
was explicitly not chosen.

Storage split (both under the 2026-07-09 §16 persistence decision):

- **Structure → `resume-state.xml` in LocalFolder** (reflection-free
  XDocument, Core-serialized): the full track list in original order with
  the §7.3/§10 display metadata per song, the shuffled play order (as
  original-order indices, present only when shuffle is on), shuffle flag,
  repeat mode, the `QueueContext` (display name + cover art id), and the
  owning server identity (URL + username). Rewritten only on structural
  changes: queue replace, shuffle toggle, repeat change — user-initiated
  and rare.
- **Fast-moving scalars → LocalSettings** (`resume.index`,
  `resume.positionSeconds`): updated on track change (index, position 0),
  on pause, on suspend, and throttled to ~10 s of playback from
  `PositionChanged` (media thread — ApplicationData is agile/thread-safe).
  At load they override the XML's snapshot values when valid.

Rules that matter:

- **No stream URLs in the file** — they embed the auth token+salt (§4:
  secrets never in plain storage). Only song ids persist; URLs are rebuilt
  from the connected client at resume.
- **Server identity is validated at load**: a state saved for a different
  server/user (stale file surviving an interrupted sign-out) reads as
  absent and is deleted. Normal sign-out clears everything via
  `PlaybackService.Stop()` → `ResumeStore.ClearAsync()`; the
  consecutive-failure stop clears too (accepted: the queue is gone from
  the UI, persistence stays consistent with what the user sees).
- **All-or-nothing parse**: any malformed song entry, index out of range,
  or non-permutation play order rejects the whole state (Core
  `ResumeState.Parse` returns null) — a partially restored queue would
  lie about what "where you left off" was. Corrupt = no card, file
  deleted best-effort (RecentServersStore convention).
- `PlayQueue.Restore(...)` (Core, tested) rehydrates tracks/order/index/
  shuffle/repeat/context in one atomic mutation; `PlaybackService.Resume`
  then reuses the normal `RebuildList()` mirror and seeks via a one-shot
  pending-seek consumed on `MediaPlayer.MediaOpened` (a failed seek
  degrades to track start).
- The Home card renders only when the persisted state loads AND no queue
  is active this session (an active queue means the bar already shows the
  same truth; after Resume the card hides for the same reason).

## Queue edits: "Play next" / "Add to queue" (added 2026-07-17)

Console checkpoint #6 feature request; decisions confirmed with Rafael and
recorded in §16. These are the first mutations of a *live* queue other than
advancement — everything else replaces wholesale.

- **Core** (`PlayQueue`, tested): `InsertNext(song)` inserts at
  `CurrentIndex + 1` in the play order — repeated inserts each land at
  current+1, so the most recent insert plays first. With shuffle off the
  original order receives the song at the same spot (the two orders are
  identical then); with shuffle on the original order receives it at the
  end, so un-shuffling places it last (the original order never contained
  it anywhere else). `Append(song)` appends to both orders. On an empty
  queue both degrade to a single-track queue at index 0 (the service
  normally routes that case through `Play` instead — see below).
- **Service mirror** (`PlaybackService.PlayNext/AddToQueue`): with no
  active queue the song simply plays as its own queue with a song-titled
  `QueueContext` (decision: never build invisible/silent state). Otherwise
  the new `MediaPlaybackItem` is inserted into the live list at the song's
  original-order index — the list always sits in original order, so the
  non-shuffled insert lands at current+1 and never at or before the
  playing item; under shuffle the play order is re-expressed via
  `SetShuffledItems` (console-verified not to interrupt the current
  track). No `Play()` call — the playback state is untouched.
- **Reference identity**: the queue, the item maps, and the row models all
  key tracks by reference. Adding an instance that is already queued
  ("Play next" on the playing album's own row, or the same row twice)
  clones the `Song` first, so duplicates are distinct instances. A cloned
  duplicate does not share `IsStarred` mutations with its sibling —
  same accepted staleness class as the cross-surface heart repaints.
- **Persistence**: both edits are structural, so `PersistQueueStructure`
  rewrites the resume file; `ResumeState` needs no schema change (tracks +
  play-order indices already express arbitrary orders).
- **UI**: the expanded queue detects the play-order change by its normal
  snapshot comparison and rebuilds rows; menu items (with cmap-verified
  Fluent icons) exist on Songs rows, album track rows, and playlist rows.

### Container form (albums / playlists / artists, added 2026-07-18)

Checkpoint #7 extended the edits from single songs to whole containers;
the design deliberately reuses every rule above:

- **Core**: `InsertNext`/`Append` gained `IReadOnlyList<Song>` block
  overloads (the single-song forms delegate to them). The block lands at
  current+1 / at the end **in its internal order** — album = disc/track,
  playlist = stored, artist = the §8 oldest-album-first pool. Original-
  order placement and the empty-queue behavior are the single-song rules
  applied to the block. Unit-tested (block order, shuffle placement,
  un-shuffle at end, empty-list no-op, empty-queue block).
- **Service**: `PlayNext/AddToQueue(IReadOnlyList<Song>, QueueContext)` —
  clone-on-collision runs per song (including repeats *within* the batch),
  the items are inserted at their original-order indices (a contiguous
  ascending block, so insertion order is stable), and SetShuffledItems +
  the resume persist run once per batch. Empty queue → `Play(block, 0,
  unshuffled, context)` — the container plays immediately under its own
  name/artwork.
- **`Services/QueueActions`**: the card menus carry only ids, so the songs
  are fetched on demand — getAlbum / getPlaylist (first-cover context) /
  getArtist + the parallel getAlbum fan-out (`LoadArtistPoolAsync`, now
  also used by ArtistDetailPage's Play/Shuffle — the page's four playback
  buttons share one fetch path). Fetch failure after the menu closed
  changes nothing (the menu-favorite contract); the artist fan-out makes
  this the most failure-prone silent path — flagged as a refinement
  candidate alongside the missing "added to queue" toast.
- **Surfaces**: card context menus on Albums browse (grid/list), the
  artist-detail album grid, the Home carousels, Playlists (grid/list),
  Artists (grid/list) — all through the console-verified ListView/GridView
  `ContextRequested` redirect — plus icon-only buttons next to
  Play/Shuffle on the album, playlist, and artist detail pages (these use
  the already-loaded songs; no refetch).

## Slice 14: feedback surface + queue-row menu + bounded fan-outs (added 2026-07-19)

The resilience slice closes the silent paths this doc flagged (the missing
"added to queue" toast, the silent container-fetch failure, the silent
per-track skip) and pays down the two deferred queue refinements:

- **Toast surface**: `Controls/ToastHost` (one transient bottom-centered
  banner; fade in/out, 3.5 s dwell, a newer message replaces the current
  one) + `Services/Toast` (static, any-thread — marshals to the UI
  dispatcher; a no-op until the shell registers, so the setup flow keeps
  its inline errors). Hosted as the shell root's LAST child: topmost, so
  toasts stay visible over the expanded-queue overlay; hit-test-invisible
  and never focusable, so the overlay's focus trap and XY navigation are
  untouched. Emitters:
  - `PlaybackService.AddSongs` — queue-edit confirmations ("“Title” will
    play next" / "Name (12 songs) added to the queue"). The empty-queue
    play-immediately path stays toast-free: the bar reveal + audio are
    the feedback.
  - `QueueActions` — container-fetch failures ("Couldn't load the album —
    nothing was added to the queue."): the menu-closed contract now says
    that nothing happened instead of implying success.
  - `PlaybackService.OnItemFailed` — "Skipped “X” — the track could not
    be played." per skip (media thread; Toast marshals), and the
    consecutive-failure stop announces itself ("Playback stopped: no
    track in the queue could be played. Check the server connection.").
    A failure burst simply replaces the toast each time.
- **Queue-row context menu** (deferred out of Slice 11): the standard
  song menu on expanded-queue rows — Play (jump; the pointer twin of
  A/Enter), favorite toggle (change-only-after-success; repaints every
  row holding the instance and syncs the left pane's heart when the row
  is the current track), Go to album / Go to artist (disabled without
  ids). Same `ContextRequested` redirect and §13.3 icons as every other
  row menu. Navigation hands off through the host: the overlay raises
  `AlbumRequested`/`ArtistRequested`, MainPage stashes the target, closes
  the overlay, and navigates once the shell is re-enabled — the `Closed`
  handler skips its queue-button focus restore for that pass, so the
  detail page's `ContentFocus` owns focus (a brief nothing-focused gap
  during the 250 ms slide is accepted).
- **Bounded fan-outs**: `Core/Concurrency/BoundedParallel.MapAsync`
  (order-preserving results, at most N workers, `Task.WhenAll` failure
  semantics; unit-tested) caps the §8 artist getAlbum fan-out, and a
  `SemaphoreSlim` gate caps the Playlists page's per-tile getPlaylist
  fetches — both at `QueueActions.MaxParallelFetches` = 4. Tiles still
  fill in individually as their fetches complete.

## Lyrics seek-to-line: `PlaybackService.SeekTo` (added 2026-07-26)

The §10.1 lyrics view stopped being strictly read-only (requirements §16,
2026-07-26): A/Enter/click on a **synced** lyric line now moves playback to
that line. This is the app's only in-track seek — the compact bar and the
overlay's progress lines remain display-only, and no scrubber is introduced.

- **`PlaybackService.SeekTo(TimeSpan) -> bool`** (UI thread) is the whole
  engine-side surface. It refuses (returning false, playback untouched) when
  there is no open item, when `MediaPlaybackSession.CanSeek` is false, or when
  `NaturalDuration` is not yet known — so the caller can degrade instead of
  guessing. Otherwise the position is clamped into
  `[0, NaturalDuration - SeekEndGuard]`, with `SeekEndGuard` = 500 ms so a seek
  to the last lyric line cannot trip end-of-stream and hand the queue an
  unintended track advance. It then re-baselines the §6.1 resume scalars
  (`OnPositionChanged`'s ~10 s throttle would otherwise keep a stale position
  after a short backward seek; `_lastSavedPosition` stays media-thread-owned
  and is left alone — a large seek trips its own threshold next tick) and
  raises `Changed` so every surface repaints at once.
- **`ExpandedQueueView.SeekToLine(index)`** resolves the target: the picked
  line's `StartMs`, or — for a blank/untimed spacer — the nearest timed line
  above it; nothing above means no seek. The playback position that lands ON a
  line is `StartMs - offset`, mirroring the highlight's
  `position + offset` comparison. On success it resumes auto-follow, clears the
  idle timer, and paints the destination through the extracted
  `SetCurrentLyricIndex` rather than waiting for `UpdateLyricHighlight` to
  derive it — the session's position readback settles a few ms after a seek, so
  deriving would briefly show the old line.
- **Every refusal falls back to `RecenterOnCurrentLine`** (unsynced lyrics, no
  reachable timestamp, non-seekable stream), so A is never a dead press. The
  "Jump to current line" button keeps its §10.1 role as the *non-destructive*
  return affordance; D-pad selection movement still never seeks.
