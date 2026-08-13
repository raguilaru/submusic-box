# Background-memory investigation — handoff brief

**Status: the reported bug no longer reproduces (run 14, 2026-08-13); margin is
thin and the audit is open.** Last updated 2026-08-13, after 14 console runs.

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

**Playback does NOT stop on its own — the process is killed** (run 12,
2026-08-13, the state trace). The last line before the termination is
`PlaybackState=Playing`; there is no `Paused`, no `MediaFailed`, no
`ItemFailed`, no `MediaEnded`, and no `Suspending`. Audio was healthy right up
to the kill. **The stream and failure paths are exonerated: this is a memory
bug, and the browsing surcharge is the right target.**

**The ratchet is GONE, and it was never a leak** (run 14, 2026-08-13 — five
excursions, the first run in the investigation where the app survived every one
and playback never stopped). Post-release floors read **184 → 151 → 174 → 151 →
173 MB**: they track how much browsing preceded each excursion and come back,
with no monotonic climb. Background entry fell from run 13's 240 MB to
**163–197 MB**. A track even changed while backgrounded and `OverLimit`, and
playback carried on.

`shells` reads **`0 of 4`** at `AfterCollect` in two of the cycles, so every
released shell was genuinely collected; it ends at `1 of 5`. **Candidate 5(ii)
— leaked page trees — is closed.** The trees were always collectable; the
collection was simply running before the unregistrations that made them so
(see the ordering fact below). Both fixes landed together, so the split between
them is not measured.

**Still imperfect after run 14**, all three worth ranking in an audit:
- **The post-unload collection is a race, and loses more often than it wins.**
  `CoreDispatcherPriority.Low` put the collect after `MainPage.Unloaded` in
  only **2 of 5** cycles; in the other three `changedSubs` still read 85–87 at
  `AfterViewRelease`. Needs to be deterministic, not probabilistic.
- **The release runs TWICE per excursion.** `OnMemoryUsageIncreased` and
  `OnMemoryUsageLimitChanging` both fire and both call `ReduceMemoryUsage`:
  two tree drops and four GCs inside a ~2 s budget.
- **Virtualization is unconfirmed.** The 95-song playlist still reads
  `changedSubs=99`, exactly as in run 13, while a later PlaylistDetail visit
  reads 28 and AlbumDetail 19–36. The `ScrollViewer` is verifiably gone and
  `ContentFrame` sits in a height-constrained `*` row, so the layout is not the
  cause. Leading explanation: `changedSubs` counts *container instances ever
  created*, not visible rows — `SpinningVinyl` subscribes on `Loaded` and
  unsubscribes on `Unloaded`, and a recycled container never unloads, so a
  scrolled virtual list still accumulates one subscription per container.
  **The check that settles it** (no build): open that playlist, do not scroll,
  read `changedSubs`; scroll to the bottom, read again. ~15 then ~15 =
  virtualization works and the subscription is fine; ~15 then climbing toward
  95 = virtualization works but `SpinningVinyl` leaks per container; 99
  immediately = virtualization is still off.

**The floor ratcheted across background round trips before run 14** (run 12 —
four excursions, switching back and forth). Retained for the shape of the
failure; the cause is settled above.

| Cycle | Entered bg | After release (+2.5 s) | Rebuilt at | Foreground bout |
|---|---|---|---|---|
| 1 | 194 MB | **156 MB** | 155 MB | +27 MB |
| 2 | 182 MB | **179 MB** | 180 MB | +25 MB |
| 3 | 205 MB | **191 MB** | 192 MB | +13 MB |
| 4 | 205 MB | **204 MB** | *killed* | — |

12–23 MB per round trip is left behind that the next release cannot recover;
private commit tracks it (129 → 146 → 167 → 171). The release reclaims roughly
what that cycle's browsing added (38, 3, 14, 1 MB) and no more.

**Per-page costs** (run 12, the navigation readings)

| Page | First visit | Later |
|---|---|---|
| HomePage (New) | +27 to +31 MB | +16, then +5 |
| PlaylistDetailPage (New) | **+33 MB** | +3 MB |
| PlaylistsPage (New) | +1 to +12 MB | — |
| PlaylistsPage (**Back**) | **−2 MB** | — |

Two consequences. The navigation cache works exactly as designed — a `Back`
into a cached page is free. And **Home is the app's most expensive page, which
is precisely the page the view release forces a rebuild of on every return**:
the release's own recovery path pays the largest render cost in the app, every
cycle.

**The release collected BEFORE the unregistrations ran** (run 13, and this had
been true since the release was written). `Window.Current.Content = null`
*schedules* the pages' `Unloaded` callbacks; it does not run them. The console
timeline: `AfterViewRelease` at 06:29:01.022 with **`changedSubs=87`** and
nothing reclaimed, `MainPage.Unloaded` at **06:29:01.133** (111 ms later), then
`changedSubs=0` and usage 238 → 220 MB by the delayed reading. All three GC
calls ran while every page and control was still subscribed to a
process-lifetime static, so there was nothing they were allowed to reclaim.
**This is the explanation for every "AfterViewRelease reclaimed 0 MB" reading
in the investigation.** Fixed 2026-08-13 by collecting in a second,
lower-priority dispatcher pass. `MainPage.Unloaded` DOES fire, so the
"DisconnectChildrenRecursive suppresses Unloaded" form of candidate 5(ii) is
disproven.

**Three detail pages had virtualization silently disabled** (run 13).
`changedSubs` turned out to be a row census — every song row carries a
`SpinningVinyl` that subscribes — and it read **99 for a 95-song playlist** and
**63 for a 59-song one**: every row realized, not the ~15 a virtualized list
builds. Cause, confirmed in the XAML: the items control sat inside a
page-level `ScrollViewer`, which measures it with infinite height and turns
virtualization off. Album, Artist and Playlist detail were all affected (Search
too, with bounded lists). Each realized row carries a `SpinningVinyl` and a
four-item `MenuFlyout`. Fixed 2026-08-13 by making the items control the scroll
host and moving the header into its `Header`. **The browse grids, Songs,
Favorites and the queue overlay were never affected.**

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

**Candidate 4 — was playback stopping on its own? CLOSED: no.** Run 12's state
trace shows `PlaybackState=Playing` as the last thing before the kill, with no
failure of any kind (§3). The OS terminated a healthy player. It is (a),
memory.

**Candidate 5 — NEW, and it is the lead: the release/rebuild cycle ratchets the
floor.** Run 12 measured 12–23 MB per background round trip that never comes
back (§3), which is what killed the app on the fourth excursion. Two mechanisms
fit, and they call for opposite responses:
- **(i) Native commit is simply not returned** across rebuild cycles
  (fragmentation). Nothing app-side fixes it, and the response would be to stop
  releasing and rebuilding at all.
- **(ii) Each cycle leaks a whole page tree.**
  `ReleaseViewContentOnUiThread` calls `DisconnectChildrenRecursive` *before*
  nulling the window content. If severing the tree first prevents `Unloaded`
  from firing, no page runs its unregistration, and
  `PlaybackService.Instance.Changed` — reached from a static that lives for the
  whole process — pins every shell from every cycle. The subscriptions are
  balanced 7-for-7 in code, so this would be a lifecycle failure, not a missing
  line. §9 already records that the release "works only because pages
  unregister in `Unloaded`" — that assumption has never been tested on device.

If (ii) holds, the release has been quietly self-defeating all along, which
would also explain why it has always appeared to reclaim so little.

---

## 7. Where it stands after run 14

**The reported bug no longer reproduces.** Five background excursions, five
survivals, playback continuous throughout. Background entry 163–197 MB against
a band that survives below ~180–200 MB, no ratchet, no leaked shells.

That is not the same as done. The margin is thin — 197 MB on cycle 3 is inside
the range that killed earlier runs — and three defects found in run 14 remain
(the collection race, the doubled release, unconfirmed virtualization). The
work from here is an audit under a memory lens rather than another round of
measurement: the instrumentation has answered what it can, and the remaining
candidates are structural.

Audit rule, learned the expensive way in this investigation: **a candidate
ships only with a predicted, measurable signature** — "if this is real, X falls
from A to B". Three fixes were built ahead of evidence here and two made things
worse.

## Superseded — run 14's brief (the first build that changed behaviour)

Everything before this was instrumentation. Run 13 found two real defects with
measurements behind them (§3), and both are now fixed. **Neither is claimed to
be sufficient** — run 13 entered the background at 240 MB, and these two
together will not account for that.

**Fix A — collect after the unload, not before.** `ReleaseViewContent` now runs
two dispatcher passes: `High` drops the tree (`AfterViewDropped`), then `Low`
collects (`AfterCollect`, `AfterViewRelease`), so the `Unloaded` callbacks land
in between. Budget split 900 ms + 700 ms, still inside the OS's ~2 s.

*Verify:* `changedSubs` on `AfterViewRelease` should now read **0 or close to
it** (was 87), and that line should show a real drop rather than the usual
zero. If `changedSubs` is still in the tens, something other than the shell
handlers is holding the tree.

**Fix B — restore virtualization on the three detail pages.** The items control
is now the scroll host with the header in its `Header` property; layout and the
single scroll region are unchanged.

*Verify:* open a large playlist and read `changedSubs` on the settled
navigation line — it should be **~15–20, not 99**. That number is the direct
readout of whether virtualization is back.

**Then re-measure the ratchet**, which run 13 could not: it died on the first
excursion, so `shells` never went past `1 of 1`. Repeat run 12's shape — browse,
play, queue a couple of hundred tracks, then background↔foreground four or five
times with a browsing bout in between each — and watch `shells=` and the
post-release floor across cycles.

**If a comparable session still enters the background near 200 MB**, the
remaining question is whether the app should keep releasing and rebuilding the
view at all: the release costs a full Home rebuild on every return (+27 to
+48 MB, the app's most expensive page) and the ratchet may be the price of the
cycle itself. That is a bigger behaviour change and wants its own A/B — same
session shape with the release disabled — before it is made.

## Answered by run 13 — was the ratchet a leak or unreturned commit?

Three counters, no behaviour change, aimed squarely at candidate 5. Two of them
ride on **every** `LogMemory` line, because the point at which a count starts
climbing is not known in advance.

| Field | Where | Reading |
|---|---|---|
| `changedSubs=N` | every memory line | handlers on `PlaybackService.Changed`. Balanced code means the **same number at every rebuild**. Climbing by a constant each cycle ⇒ mechanism (ii), leak confirmed, fix identified |
| `shells=A of B` | every memory line | shells still uncollected, of shells ever built. Weak references, so the census cannot itself pin one; no finalizers, which would change what is measured. Only meaningful just after a collection — `RebuildViewIfReleased` forces one before its reading |
| `MainPage.Unloaded — …` | the release | tests the `DisconnectChildrenRecursive` hypothesis head-on. **"Releasing view content" with no `MainPage.Unloaded` line after it** proves the unregistrations never run |

**How to run it.** Same shape as run 12, which is what exposed the ratchet:
browse a few sections, play, queue a couple of hundred tracks, then **switch
between background and foreground four or five times**, with a real bout of
browsing in between each. Watch `shells=` and `changedSubs=` at each
`AfterViewRebuild`.

**What each outcome justifies.** Both counters flat while the floor still
climbs ⇒ mechanism (i), nothing is leaking, and the open question becomes
whether to keep releasing the view at all — a behaviour change, and one that
wants its own A/B (same session with the release disabled) before it is made.
Either counter climbing ⇒ mechanism (ii): fix the release so `Unloaded` fires
(or unregister explicitly before disconnecting), and re-measure the ratchet.

## Built for run 12 — playback state and page attribution

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

**ANSWERED (run 12).** The last state before the kill was `Playing`, with no
failure of any kind — so candidate 4(a): memory. The trace stays in, because
"playback was healthy when the OS killed it" is worth re-confirming on every
future run rather than assuming.

**2. Per-page navigation readings** (`MainPage.LogNavigationMemory`). One hook
on the content frame's `Navigated`, so every page is covered without touching
any of them: `Navigated → <Page> (<mode>)` and `NavigatedSettled → …` 2.5 s
later. Attributes the ~40 MB browsing bout to actual surfaces, and shows
whether a cost is per-visit (accumulating) or first-render only. Watch the
`Back` navigations into the two `NavigationCacheMode.Required` pages (Songs,
Search): they re-render a page that was never released and should cost far less
than the `New` that built it.

**ANSWERED (run 12)** — per-page figures are in §3. `Back` into a cached page
is free, and Home is the app's most expensive page. Both readings stay in:
they are what will show whether any future change actually moves the browsing
surcharge.

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
