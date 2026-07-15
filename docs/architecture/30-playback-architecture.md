# Playback & Queue Architecture (Slice 10)

Written 2026-07-15, before implementation. Scope and the four decisions below
were confirmed with Rafael at the start of the Slice 10 session.

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
   with **all rows loaded so far** (the incremental collection at click time)
   with the clicked song as the current track. No auto-fetching of further
   pages into the queue.

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

- Slice 11: expanded queue enables the bar's queue button + Y mapping.
- Slice 05b: persistence writer + resume card on top of `PlayQueue`.
- Transcoding-aware format/bitrate labels (needs the OpenSubsonic
  `transcoding` extension) — refinement-phase candidate, not v1.
- Artist Play fan-out: one `getAlbum` per album in parallel; fine for typical
  artists, revisit throttling if a 100-album artist stalls on device.
