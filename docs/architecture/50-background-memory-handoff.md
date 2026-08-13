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
  `AfterDropCoversLate`, `AppMemoryUsageLimitChanging`, `AfterViewRelease`.

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
- **Releasing the entire visual tree reclaims ~0 MB.** Always. With
  `DisconnectChildrenRecursive`, with `GC.Collect`, with
  `WaitForPendingFinalizers`, with the cover cache dropped first. This is the
  most-repeated observation of the investigation.
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
- A 200-track queue: **~16–26 MB in items alone**, before any is opened
  (measured indirectly — see §5, candidate 1).

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

---

## 6. Open candidates, ranked

**Candidate 1 — the queue's per-item cost (most promising).**
`PlaybackService.RebuildList` materializes a `MediaPlaybackItem`, a
`MediaSource` **and** a full `ApplyDisplayProperties` (including an SMTC
thumbnail `RandomAccessStreamReference`) for *every* track up front. At the
measured rates that is ~16–26 MB for 200 tracks before anything is opened, and
it scales linearly with queue length — the exact shape of the reported bug.

Two sub-options, cheapest first:
- **Apply SMTC display properties lazily**, to the current and next items only,
  instead of all N. Low risk: it does **not** mutate `_list.Items`, so none of
  the instability that sank the recycling attempt applies.
- **A windowed list** (keep only a few items around the current index).
  Higher risk for exactly that reason.

Neither is justified until the cost is measured directly (see §7).

**Candidate 2 — the browsing surcharge (~40–50 MB).** The app-owned cover cache
(`Services/CoverArt` + `Controls/CachedCover`) exists to attack this: covers are
decoded from a stream so they have no URI, the framework caches nothing, and
`DropBitmaps()` on `EnteredBackground` is a real operation. **First console pass
was inconclusive** — it reported 11 MB released and `AppMemoryUsage` did not
move, through the GC, the finalizer drain and the view release.

**Candidate 3 — does released memory EVER register?** Blocks all release-based
work. Either the post-drop reading is taken too early (references are dropped
microseconds before it) or native commit is simply never returned to the OS —
which would explain every zero-reclaim reading in this investigation.

---

## 7. The next step — one instrumentation build, no behaviour change

1. **A delayed reading ~2.5 s after `DropBitmaps`** (reuse the
   `LogSettledSoon` one-shot timer pattern in `PlaybackService`). Settles
   candidate 3, which decides whether candidate 2 — and any future
   release-based idea — is worth anything.
2. **A memory bracket around `RebuildList`**: before, after the items are
   created, and after display properties are applied, with the track count.
   Measures candidate 1 directly and splits it into "items" vs "SMTC
   properties", which decides between the cheap and the expensive fix.

**Then one A/B in a single sitting**, non-debug: same browsing both times —
play a 10-track album → background → read `EnteredBackground`; then queue
200 tracks → background → read. The delta is the queue cost with nothing else
moving.

### Built 2026-08-13 — how to read what it logs

Instrumentation only; no behaviour changed. Two brackets and one delayed pair.

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
