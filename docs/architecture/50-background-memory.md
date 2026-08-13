# Background memory on Xbox — findings and final state

**Status: CLOSED 2026-08-13**, after 16 console runs. The reported bug does not
reproduce. This file is the durable record: the platform facts, what fixed it,
what was tried and rejected, and how to diagnose a recurrence. The blow-by-blow
is in requirements §16 (entries dated 2026-08-12 and 2026-08-13) and in git
history.

---

## 1. The bug

Music stopped when the app was backgrounded — usually when a game launched,
reliably after browsing and queueing a lot of tracks. Never a crash: no
exception, the process simply ended.

On Xbox, backgrounding drops the app's memory limit from ~1024 MB to **128 MB**
with roughly 2 seconds to comply. The app was over, and the OS terminated it.

---

## 2. Platform facts worth keeping

- **128 MB is a threshold the OS weighs, not a line it enforces.** Observed:
  survived 5+ minutes at 133–143 MB and 36 s at 178 MB; killed at 201, 214, 259,
  270, 290 and 447 MB. The working target is **stay under ~180 MB**.
- **128 MB is unreachable for this app anyway.** The minimal floor — fresh
  launch, play one track, no browsing, artwork suppressed entirely — is
  **129 MB**. A XAML shell plus a media pipeline does not fit under that line.
  The goal is the tolerated band, not compliance.
- **The footprint is ~100 % native.** Managed heap never exceeded ~8 MB against
  200+ MB total, in every reading. Nothing on the managed heap is worth
  attacking.
- **Released memory registers over ~10 seconds, not immediately.** A drop that
  reads as 0 MB reclaimed will read as 20–35 MB reclaimed ten seconds later.
  The OS decides within ~2 s, so **releasing under pressure cannot save the
  app** — but "reclaimed nothing" readings taken microseconds after a release
  are measurement artifacts, and several months of this investigation rested on
  exactly that mistake.
- **A track change costs 30–45 MB and drains a few seconds later.** Not a leak.
  Backgrounding within seconds of one measures the app mid-spike.
- **Xbox does not enforce background limits under a debugger.** An F5 run always
  looks healthy. Any verification needs a non-debug launch from the Dev Home
  tile; every run stamps `debuggerAttached=False`.

---

## 3. What actually fixed it

In rough order of contribution.

**The realization cache.** `ItemsStackPanel`/`ItemsWrapGrid` default to
`CacheLength="4.0"` — five viewports of realized containers (two above the
visible one, two below). The console measured that ratio exactly twice: 85
realized rows for a ~17-row queue viewport, and all 95 rows of a playlist for a
~19-row one. A realized row here is expensive — it carries a `SpinningVinyl`
with a storyboard and a `MenuFlyout` of 8–12 items, all built when the template
inflates. Two shared templates in `App.xaml` (`TightItemsStack`,
`TightItemsWrapGrid`) set 1.0 and are applied to every items control that can
hold a large collection. Realized containers roughly halved. **Do not raise this
back to the default without re-measuring**, and note 1.0 is deliberate rather
than 0 — one viewport of buffer is what keeps fast scrolling from showing blank
rows.

**Collecting after the unload, not before.** `Window.Current.Content = null`
*schedules* the pages' `Unloaded` callbacks; it does not run them. Those
callbacks unregister the shell from process-lifetime statics, and until they run
the whole tree is still reachable — so the release's own `GC.Collect` had
nothing it was allowed to reclaim. This was true from the day the release was
written and explains every "reclaimed 0 MB" reading. `ReleaseViewContent` now
drops the tree, **waits on a signal raised at the end of `MainPage.OnUnloaded`**,
then collects. A `CoreDispatcherPriority.Low` hint was tried first and held in
only 2 of 5 cycles — a priority is not a happens-after guarantee.

**One reduction per excursion.** `AppMemoryUsageIncreased` and
`AppMemoryUsageLimitChanging` both fire and both called `ReduceMemoryUsage`, so
the app did two tree drops and four GCs inside a ~2 s budget. Guarded.

**Cover-art URL stability.** `GetCoverArtUri` went through the shared request
builder, which mints a fresh random salt per call, so every cover URL was
unique — XAML's URI-keyed image cache and the HTTP cache could never hit, and
the image cache accumulated an entry per request in native memory the app cannot
release. `Services/CoverArt` is now the app's **only** cover-URL builder, minting
one URL per (cover, tier) over a four-step ladder (100/220/320/400). Any other
endpoint whose URL is used as a cache key has the same hazard by construction.

**An app-owned bitmap cache.** Covers are decoded from a stream, so they have no
URI and the framework caches nothing — which makes `DropBitmaps()` on
`EnteredBackground` a real operation rather than a hope. 24 MB LRU, released
7–19 MB per backgrounding in practice.

**Releasing the visual tree under pressure.** Microsoft's documented pattern,
implemented in `App.ReduceMemoryUsage`. Correct and retained, but it is not what
fixed this — see §2 on the timing.

---

## 4. Tried and rejected — do not retry

**Recycling retired `MediaPlaybackItem`s** (replace in place, dispose the old
`MediaSource`). Reverted after one console pass: peak **465 MB, killed at 447**
against 268/259 for the same exercise without it. Two mechanisms, both worth
remembering:

- Replacing an item in `_list.Items` fires a **second, spurious
  `CurrentItemChanged`**, so each recycle triggered another recycle, another
  item, another stream open.
- **`MoveTo` stops taking effect while the list is being mutated** — four
  consecutive jumps produced no track change and usage sat frozen for 30 s.
  That is the "it takes 10+ seconds for the track to start" symptom.

In-place item replacement is safe **only** where `RetryWithMp3` uses it: a failed
item, where playback has already moved on and a stall is invisible.

**Lazy SMTC display properties, and a windowed `MediaPlaybackList`.** Both were
ranked fixes for a cost that does not exist: a 200-track queue costs **~1 MB in
items**, measured directly, and display properties are free.

**Reworking the per-row `MenuFlyout` and `SpinningVinyl`.** Dropped on evidence:
halving realized containers barely moved the footprint, so the per-row weight
they represent is not the binding term. Revisit only with a measurement that
contradicts that.

---

## 5. Claims that were stated and are WRONG

Recorded so nobody rebuilds on them:

1. "Opening the queue overlay costs ~49 MB." It costs 0–1 MB.
2. "Releasing the visual tree reclaims 0 MB." It reclaims 20–35 MB, over ~10 s.
   Every reading that said otherwise was taken microseconds after the drop.
3. "A 200-track queue costs 16–26 MB in items alone." ~1 MB.
4. "Per-open memory is cumulative and never returned." It drains.
5. "High-bitrate FLAC is the driver." An m4a 256 kbps open cost *more* than a
   FLAC 1218 kbps one.
6. "The Songs page's whole-library load is the largest allocation." Managed heap
   never exceeded 8 MB. It was the assumed villain for months and was never the
   problem.
7. "It plateaus with a small queue, so a windowed list is the fix." The
   comparison was contaminated by the item-recycling build.
8. "Playback stops on its own and the app then loses its background-audio
   exemption." The state trace showed `PlaybackState=Playing` as the last line
   before a kill. The OS terminated a healthy player.

---

## 6. Diagnosing a recurrence

`Services/BackgroundDiagnostics` writes `background-diagnostics.log` to the app's
LocalFolder; pull it via the Device Portal (File explorer → LocalAppData → the
app → LocalState). It is deliberately minimal now — lifecycle, the memory
pressure path, and real playback failures.

Flow of a healthy backgrounding:

```
EnteredBackground → AfterDropCovers → AppMemoryUsageLimitChanging (new=128 MB)
  → Releasing view content → AfterViewDropped → MainPage.Unloaded
  → Shell unload observed; collecting. → AfterViewRelease
  → AfterLimitChangingHandler
```

What to check, in order:

- **`debuggerAttached=False`.** If not, the run proves nothing.
- **`EnteredBackground` usage.** Under ~180 MB is the survivable band.
- **`Shell unload observed`** present, and `AfterViewRelease` following it. If
  the unload is missing, something is holding the shell and the release is a
  no-op.
- **`MediaFailed` / `ItemFailed`.** Their presence means the stop was a playback
  failure, not memory — an entirely different investigation.
- **No `Suspending` line before the log ends** means the OS killed the process.
  A clean `Suspending` with no audio playing is normal, not a failure.

---

## 7. Where the code is

| Concern | File |
|---|---|
| Cover URLs, request tiers, bitmap LRU, `DropBitmaps` | `src/BoxSubMusic.App/Services/CoverArt.cs` |
| `CachedCover` attached property (25 XAML sites) | `src/BoxSubMusic.App/Controls/CachedCover.cs` |
| Realization-cache panel templates | `src/BoxSubMusic.App/App.xaml` (`TightItemsStack`, `TightItemsWrapGrid`) |
| `RebuildList`, `MaxPlayedItemsToKeepOpen = 0`, `_mapGate` | `src/BoxSubMusic.App/Services/PlaybackService.cs` |
| Lifecycle, `ReduceMemoryUsage`, `ReleaseViewContent`, the unload signal | `src/BoxSubMusic.App/App.xaml.cs` |
| The shell's unregistrations + `NotifyShellUnloaded` | `src/BoxSubMusic.App/MainPage.xaml.cs` |
| The log | `src/BoxSubMusic.App/Services/BackgroundDiagnostics.cs` |
