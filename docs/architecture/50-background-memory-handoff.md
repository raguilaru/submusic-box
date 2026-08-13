# Background-memory investigation — handoff brief

**Status: OPEN.** Last updated 2026-08-13, after ~15 console rounds.

This file is deliberately self-contained: a new session should be able to
continue from this alone. The full chronological record is in requirements §16
(entries dated 2026-08-12 and 2026-08-13) and the durable notes in
`docs/STATUS.md`; read those only if this file leaves a question open.

---

## 1. The bug

Music stops when the app is backgrounded — usually when a game launches,
reliably after browsing and queueing a lot of tracks. On Xbox, backgrounding
drops the app's memory limit from ~1024 MB to **128 MB** with roughly 2 seconds
to comply. The app is over, and the OS terminates it.

**Not a crash.** There is no exception in any log; the process simply ends with
no `Suspending` line.

---

## 2. How to measure (read this before doing anything)

- **The bug is invisible under a debugger.** Xbox does not apply background
  memory limits to a debugged process. An F5 run always looks healthy. Deploy,
  **stop debugging**, and launch from the Dev Home tile. Every run stamps
  `debuggerAttached=False` — check it.
- **The log** is `background-diagnostics.log` in the app's LocalFolder, pulled
  via the Device Portal (https://10.0.0.28:11443 → File explorer → LocalAppData
  → the app → LocalState). `Services/BackgroundDiagnostics` writes it.
- **A diagnostic toggle** lives on **Account → Diagnostics → "Run without
  artwork"**. It persists across launches and is stamped into the log as
  `artwork=on` / `artwork=SUPPRESSED`. It is scaffolding — delete it when the
  investigation closes.
- Useful log markers, in flow order: `OnLaunched`, `LeavingBackground`,
  `RebuildListStart` → `RebuildListItemsCreated` → `RebuildListDisplayPropsApplied`,
  `QueueAddStart` → `QueueAddComplete`,
  `TrackChanged #N` → `AfterTrackSettled #N` (2.5 s later, where a stream
  open's cost lands), `JumpRequested #N` → `AfterJumpMoveTo #N`,
  `QueueOverlayOpening` → `QueueOverlayContentReady` → `QueueOverlayClosed`,
  `EnteredBackground`, `AfterDropCovers` → `AfterDropCoversSettled` →
  `AfterDropCoversLate`, `AppMemoryUsageLimitChanging`, `AfterViewRelease`,
  `Suspending`.
- Added after run 11: `PlaybackState=<state>`, `MediaFailed`, `ItemFailed`,
  `MediaEnded`, and `Navigated → <Page>` → `NavigatedSettled → <Page>`.

---

## 3. Established facts — do not re-derive these

Every number below is a console measurement, not an inference.

**The tolerance band**

| Backgrounded at | Outcome |
|---|---|
| 133–143 MB | survived 5+ minutes, still advancing tracks |
| 178 MB | survived 36 s |
| **201–214 MB** | **killed** |
| 259 / 270 / 290 / 447 MB | killed |

128 MB is a threshold the OS weighs, not a line it enforces. **The working
target is to stay under ~180 MB.**

**Where the memory is**

- The footprint is ~100 % **native**: managed heap reads 1–4 MB against
  200+ MB total, in every single reading.
- **Releasing memory DOES register — over about 10 seconds** (measured
  2026-08-13, run 11, the delayed readings). Dropping the covers and releasing
  the view read 208 MB immediately, **194 MB at +2.5 s and 181 MB at +10 s**
  (private commit 172 → 160 → 147). ~27 MB genuinely came back.
  **This retracts the "releasing reclaims ~0 MB" fact that stood through this
  whole investigation** — every earlier reading was taken 30–200 ms after the
  references were dropped, which is far too early to see it.
  It changes nothing about the remedy: the OS decides within ~2 s, so
  releasing under pressure still cannot save the app. It is a measurement
  correction, not a new option.
- **There is nothing left to release.** At +10 s — visual tree gone, every
  cover dropped, two `GC.Collect`s and a finalizer pass — the app sits at
  **181 MB**, and was still at 181 MB at +26 s. Plateaued, not draining.
  Against the 143 MB minimal floor, ~38 MB of a browsed session is residue no
  app-side release can touch.
- Rebuilding the view costs ~1 MB.

**The floor** (fresh launch → play → 30 s settle, no browsing)

- **143 MB** with artwork, **129 MB** with artwork suppressed.
- Composition: ~29 MB process baseline, ~28 MB shell, ~46 MB reaching a
  playing track, ~42 MB playback.
- Browsing adds ~40–50 MB on top → normal sessions reach **183–201 MB**, which
  is where the app dies.

**Stream opens are transient, not a leak**

- Every track change costs **30–45 MB**, whether jumped to or reached
  naturally. The very first track of a session, untouched, went 109 → 152 MB.
- It **drains** a few seconds after churn stops: ten rapid changes climbed to
  283 MB and were back to 182 MB 2.5 s later, with no user action.
- Codec is not the driver: an m4a 256 kbps open cost **more** than a FLAC
  1218 kbps one.
- Queue size does not drive the peak; **churn rate** does. Clean 8-track and
  22-track runs both peak at 270–285 MB.
- The ~2 s the OS allows is **shorter than the drain**, so backgrounding within
  a few seconds of a track change measures the app mid-spike.

**Artwork**

- 14 MB in the minimal case (143 vs 129), ~27 MB in a browsed one (156 vs 129),
  11 MB actually held by the app cache in the last run.
- Cover URLs used to be **unique per request** — the shared request builder
  mints a fresh random salt per call, so XAML's URI-keyed image cache and the
  HTTP cache could never hit. Fixed: `Services/CoverArt` is now the app's only
  cover-URL builder.
- Cover requests used to be **2× the display size** (decode was halved earlier
  but `size=` was not). Fixed.

**Other measured costs**

- Opening the expanded queue overlay: **0–1 MB**.
- Scrolling all 228 queue rows: **+13–18 MB**, not returned when it closes.
- **The queue costs ~0 per item** (measured directly 2026-08-13, run 11):
  `RebuildList` read 98 → 98 → 98 MB for 10 tracks and 194 → 194 → 195 MB for
  59; `QueueAdd` read 198 → 199 MB for 95 items and 210 → 210 MB for 59. A
  200-track queue is about **1 MB**, and SMTC display properties are free.
  **This retracts the ~16–26 MB estimate**, which was inferred from deltas
  that had browsing and a stream open inside them.
- **A browsing bout costs ~40 MB and does not come back**: 57 MB with the
  shell up → 98 MB after browsing to an album; later 142 → 198 MB across
  another bout. Covers are only 13 MB of it and the queue ~1 MB, so most of
  the surcharge is still unattributed — which is what the page-navigation
  readings added in run 12 are for.

---

## 4. Tried and rejected — do not retry

**Item recycling (replace the retired `MediaPlaybackItem` in place and dispose
its `MediaSource`).** Reverted after one console pass. It made things worse on
both axes:

- Peak **465 MB, killed at 447**, against 268/259 for the same exercise
  without it.
- **`MoveTo` stops taking effect while `_list.Items` is being mutated** — four
  consecutive jumps produced no track change and usage sat frozen for 30 s.
  This is the "it takes 10+ seconds for the track to start" symptom.

In-place item replacement is safe **only** where `RetryWithMp3` uses it (a
failed item, playback already moved on, a stall is invisible). Not as a routine
operation.

**"Release memory under pressure" as a strategy.** Microsoft's documented
remedy — `Window.Current.Content = null` on `AppMemoryUsageLimitChanging` — is
implemented and correct, and reclaims ~1 MB. It is retained because it is cheap
and right, but **it is not the fix**.

---

## 5. Retracted claims — these were stated and are WRONG

Recorded so nobody rebuilds on them:

1. **"Opening the queue overlay costs ~49 MB."** It costs 0–1 MB. The 49 MB was
   everything that happened in the eight seconds after it opened.
2. **"It plateaus with a small queue, so the list retains opened items and a
   windowed list is the fix."** The comparison was contaminated: a clean
   8-track run against a 228-track run taken on the build with the item
   recycling that was later reverted.
3. **"The doubled `TrackChanged` pairs are caused by item recycling."**
   `MediaPlaybackList` transiently reports a null `NewItem` during a jump on
   its own. Recycling made them more frequent, not caused them.
4. **"High-bitrate FLAC is the driver."** It is not; see §3.
5. **"Per-open memory is cumulative and never returned."** It drains.
6. **"Releasing the visual tree reclaims 0 MB."** It reclaims ~27 MB, over
   ~10 s. Every reading that said otherwise was taken microseconds after the
   drop. The remedy is still useless — the OS decides in ~2 s — but the fact
   was wrong, and it was used to argue that nothing the app owns holds the
   memory.
7. **"A 200-track queue costs 16–26 MB in items alone."** It costs ~1 MB.
   Measured directly, not inferred.

---

## 6. Candidates — all three resolved by run 11 (2026-08-13)

**Candidate 1 — the queue's per-item cost. CLOSED, it is ~0.** Measured
directly by the `RebuildList` / `QueueAdd` brackets: ~1 MB for 200 tracks, and
display properties free (§3). **Do not build lazy SMTC display properties, and
do not build a windowed list** — both were ranked fixes for a cost that does
not exist.

**Candidate 2 — the browsing surcharge (~40 MB). STILL THE TARGET, but the
cover cache is not enough.** The cache works — 13 MB dropped, and run 11's
delayed readings prove the megabytes do come back. But covers are 13 MB of a
~40 MB bout and the queue is ~1 MB, so **most of the surcharge is still
unattributed**. Nothing measured to date says which surface spends it.

**Candidate 3 — does released memory ever register? ANSWERED: yes, over
~10 s.** See §3. Two consequences, and the second is the important one:
- The zero-reclaim readings were an artifact. The cover cache and the view
  release both work.
- **Release-based work is over anyway.** The OS decides within ~2 s, and at
  full drain — no view, no covers — the app still holds 181 MB. Nothing the
  app can release gets it into the surviving band. Only *not allocating* can.

**Candidate 4 — NEW, and it may outrank all of the above: playback may be
stopping on its own.** Run 11 ended with a clean `Suspending` at 181 MB after
26 s in the background — the first background failure in this investigation
with a `Suspending` line at all; every previous one was a silent kill. Audio
stopped, but the log cannot say in which order:
- **(a) memory** — the OS suspended an app that stayed over the limit, and
  suspension stops audio; or
- **(b) audio first** — a `backgroundMediaPlayback` app is exempt from
  suspension *only while audio is playing*, so if playback died for its own
  reason the suspension follows within seconds automatically.

(b) deserves real weight: 26 s is a long grace for an over-limit app when
comparable runs at 201–214 MB were killed outright, and there was no
`TrackChanged #5` — a track ending and the next stream failing to open would
have left **no trace whatsoever** in the log as it stood.

---

## 7. The next step — run 12, still no behaviour change

Two instrumentation points, built 2026-08-13 after run 11 closed candidates 1
and 3. **Point 1 is the one that matters**: it decides whether this is a memory
bug at all, and therefore whether point 2 is even the right question.

**1. Playback state and failures** (`PlaybackService`). Nothing subscribed to
`MediaPlayer.MediaFailed` before now, and `ItemFailed`/`MediaEnded` logged
nothing — so a stream failing in the background was invisible. Added, all
diagnostics-only (the established skip/stop behaviour is untouched):

| Line | Says |
|---|---|
| `PlaybackState=<state>` | every transition, deduped (Buffering flaps, and the log is a 600-line ring buffer) |
| `MediaFailed` | player-level failure, with HRESULT — previously silent |
| `ItemFailed` | per-item failure, with error code, track and the consecutive count |
| `MediaEnded (end of list)` | audio stopped for a legitimate reason |

**How to read it.** A `Paused` or `None` arriving **seconds before**
`Suspending` means candidate 4(b): playback died on its own, the app lost its
background-audio exemption, and the suspension is a *consequence*. The fix is
then in the stream/failure path and this was never primarily a memory bug. A
state change landing **at** `Suspending` means 4(a), memory, and the browsing
surcharge is the target.

**2. Per-page navigation readings** (`MainPage.LogNavigationMemory`). One hook
on the content frame's `Navigated`, so every page is covered without touching
any of them: `Navigated → <Page> (<mode>)` and `NavigatedSettled → …` 2.5 s
later. Attributes the ~40 MB browsing bout to actual surfaces, and shows
whether a cost is per-visit (accumulating) or first-render only. Watch the
`Back` navigations into the two `NavigationCacheMode.Required` pages (Songs,
Search): they re-render a page that was never released and should cost far less
than the `New` that built it.

**How to run it.** Non-debug as always. Browse a fixed path through several
sections, play something, queue a couple of hundred tracks, wait 30 s after the
last track change, background, and **leave it there for several minutes** —
long enough that either a suspension happens or it demonstrably survives.

### Built earlier on 2026-08-13 — how to read what it logs

Instrumentation only; no behaviour changed. Two brackets and one delayed pair.
**Both questions are now answered — run 11, results in §3 and §6.** The
prediction tables below are kept because they record what each outcome would
have justified: the delayed pair landed on row 2 (memory registers, but far too
slowly to matter), and the `RebuildList` split landed on row 3 (both deltas
tiny, candidate 1 is not the bug).

**Candidate 3 (does released memory ever register?)** — on `EnteredBackground`,
after `AfterDropCovers`, two further readings fire at **+2.5 s** and **+10 s**:
`AfterDropCoversSettled` and `AfterDropCoversLate`. Both stamp the megabytes
dropped and the usage before the drop, so each line reads on its own, and both
stamp the delay that actually elapsed (a suspended app can starve the timer —
a figure far past its requested delay is not trustworthy).

| Reading | Means |
|---|---|
| usage falls by roughly the dropped MB | the earlier readings were simply too early — release-based work is viable, and the cover cache is worth keeping |
| usage flat at +2.5 s but down at +10 s | it registers, just slowly — and slower than the OS's ~2 s budget, so releasing under pressure can never help; only *not allocating* can |
| flat at both, `privateCommit` too | native commit is never returned. **Candidate 2 is dead and so is every release-based idea**, including the cover cache. Delete it and attack allocation only |

`privateCommit` is the discriminator against `AppMemoryUsage` — LogMemory
prints both.

**Candidate 1 (the queue's per-item cost)** — `RebuildList` now reports three
readings with the track count: `RebuildListStart`,
`RebuildListItemsCreated` (items + `MediaSource`, no metadata) and
`RebuildListDisplayPropsApplied` (SMTC properties + the thumbnail stream
reference). The two deltas decide between the two fixes ranked in §6:

- **most of it in the display-props delta** → apply them lazily to the current
  and next items only. Cheap, and it does not touch `_list.Items`, so none of
  the instability that sank item recycling applies.
- **most of it in the items delta** → the cost is `MediaSource` per track and
  only a windowed list addresses it. Higher risk, for exactly that reason.
- **both small** → the ~16–26 MB estimate was wrong, candidate 1 is not the
  bug, and the browsing surcharge is the remaining target.

To make this split measurable, `CreateItem` was divided into `CreateItemCore`
and `ApplyDisplayPropertiesTo`, and `RebuildList` runs them as two passes.
Nothing else changed: the list is not yet the player's source during either
pass, and every other caller still gets both halves in one call.

The queue-edit path (`AddSongs`) is bracketed too — `QueueAddStart` /
`QueueAddComplete` — because a queue grown by "Add to queue" never goes through
`RebuildList`, and the A/B above would otherwise report no per-item cost at
all. It is **not** split into two passes: that list is live and attached to the
player, and mutating it is what destabilized the reverted recycling attempt.

---

## 8. Where the code is

| Concern | File |
|---|---|
| Cover URLs, request tiers, bitmap LRU, `DropBitmaps`, `Suppressed` toggle | `src/BoxSubMusic.App/Services/CoverArt.cs` |
| `CachedCover` attached property (25 XAML sites use it) | `src/BoxSubMusic.App/Controls/CachedCover.cs` |
| `RebuildList`, `CreateItem`, `JumpTo`, stream-open instrumentation, `MaxPlayedItemsToKeepOpen = 0`, `_mapGate` | `src/BoxSubMusic.App/Services/PlaybackService.cs` |
| Lifecycle, `ReduceMemoryUsage`, `ReleaseViewContent`, launch stamps | `src/BoxSubMusic.App/App.xaml.cs` |
| The log itself | `src/BoxSubMusic.App/Services/BackgroundDiagnostics.cs` |
| Diagnostics toggle UI (scaffolding — remove when done) | `src/BoxSubMusic.App/AccountPage.xaml` + `.xaml.cs` |

Build/test/deploy commands are in `CLAUDE.local.md` (gitignored). Command-line
MSBuild compiles but always fails appx packaging with `APPX0703`; **VS F5 is the
only working deploy path**, and Rafael drives it.

---

## 9. What already landed (keep, do not undo)

- `Services/CoverArt` as the only cover-URL builder — stable URLs (both
  platform caches can hit) and a four-step request ladder (100/220/320/400).
- Request size == display size everywhere; decode size == display box.
- App-owned bitmap cache + `CachedCover` attached property.
- `MaxPlayedItemsToKeepOpen = 0` (returns ~10 MB per jump, no downside seen).
- `_mapGate` lock on the item maps (they are written on the UI thread and read
  on the media thread).
- The view release + `BackgroundDiagnostics`, including the thread-marshalling
  fix (`Window.Current` is thread-static and the memory events are not on the
  UI thread).
