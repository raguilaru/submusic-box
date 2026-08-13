using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.System.Threading;

namespace BoxSubMusic.App.Services
{
    /// <summary>
    /// Persistent lifecycle/memory log for the §11 background-playback
    /// investigation (2026-08-12).
    ///
    /// Why a file and not <c>Debug.WriteLine</c>: Xbox does NOT apply its
    /// background memory limits while a debugger is attached, so the only run
    /// that reproduces a background termination is a Release run with no
    /// debugger — exactly the run where Debug output goes nowhere. The
    /// evidence we need (usage vs. the new limit at the moment the limit
    /// drops) therefore has to reach disk.
    ///
    /// Constraints this shape is built around:
    /// - The limit-changing handler has ~2 s on Xbox to get under the new
    ///   limit, so <see cref="Log"/> never touches the disk on the calling
    ///   thread: it appends to an in-memory buffer and lets a serialized
    ///   background flush drain it.
    /// - Termination follows suspension, so <see cref="FlushAsync"/> is
    ///   awaited inside the suspend deferral — that is what makes the last
    ///   lines before a kill survive.
    /// - The log is a ring buffer on disk (trimmed to
    ///   <see cref="MaxLines"/>), so it cannot grow without bound on a
    ///   console that is never reinstalled.
    /// </summary>
    internal static class BackgroundDiagnostics
    {
        private const string FileName = "background-diagnostics.log";

        /// <summary>Lines retained on disk; a few sessions' worth.</summary>
        private const int MaxLines = 600;

        private static readonly object Gate = new object();
        private static readonly List<string> Pending = new List<string>();

        /// <summary>
        /// Serializes file access. Flushes are fire-and-forget from event
        /// handlers, so two could otherwise overlap on the same file.
        /// </summary>
        private static readonly SemaphoreSlim FileGate = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Records one line with a timestamp. Cheap and non-blocking: safe to
        /// call from the memory-pressure handlers, where the 2-second budget
        /// leaves no room for I/O.
        /// </summary>
        public static void Log(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            string line = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message;
            lock (Gate)
            {
                Pending.Add(line);
            }

            System.Diagnostics.Debug.WriteLine(line);
        }

        /// <summary>
        /// Records a memory snapshot: the two numbers whose relationship
        /// decides whether the OS lets the app keep playing.
        /// </summary>
        public static void LogMemory(string stage)
        {
            // The managed-heap figure is the important one for the open
            // question (2026-08-12): releasing the entire visual tree moved
            // AppMemoryUsage by 0 MB across three console runs, so the ~150 MB
            // that browsing adds is somewhere the tree does not own. Managed
            // vs. native splits the search in half — a large managed heap
            // points at our own collections, a small one at decoded bitmaps
            // and the media pipeline.
            string detail;
            try
            {
                var report = MemoryManager.GetAppMemoryReport();
                detail = ", managedHeap=" + Megabytes((ulong)GC.GetTotalMemory(false)) + " MB"
                    + ", privateCommit=" + Megabytes(report.PrivateCommitUsage) + " MB"
                    + ", totalCommit=" + Megabytes(report.TotalCommitUsage) + " MB"
                    + ", peakPrivate=" + Megabytes(report.PeakPrivateCommitUsage) + " MB";
            }
            catch (Exception ex)
            {
                detail = ", memoryReportFailed=" + ex.Message;
            }

            // The two leak counters ride on every reading rather than on a
            // few chosen lines: run 12 found the floor ratcheting 12-23 MB per
            // background round trip, and the point at which a count starts
            // climbing is not known in advance. Both are cheap, and both are
            // scaffolding to remove with the rest of the investigation.
            string leaks;
            try
            {
                leaks = ", changedSubs=" + PlaybackService.ChangedSubscribersForDiagnostics
                    + ", shells=" + MainPage.LiveShellCensus;
            }
            catch (Exception ex)
            {
                leaks = ", leakCountersFailed=" + ex.Message;
            }

            Log(stage
                + ": usage=" + Megabytes(MemoryManager.AppMemoryUsage) + " MB"
                + ", limit=" + Megabytes(MemoryManager.AppMemoryUsageLimit) + " MB"
                + ", level=" + MemoryManager.AppMemoryUsageLevel
                + detail
                + ", queue=" + PlaybackService.QueueCountForDiagnostics
                + leaks);
        }

        public static string Megabytes(ulong bytes)
        {
            return (bytes / (1024UL * 1024UL)).ToString();
        }

        /// <summary>
        /// Takes a memory reading <paramref name="delay"/> after the call and
        /// flushes it. For measuring whether memory the app has just released
        /// ever registers as released — the open question that blocks every
        /// release-based remedy (§11 candidate 3): every post-release reading
        /// in this investigation has been taken microseconds after the
        /// references were dropped, so "reclaimed 0 MB" and "reclaimed
        /// something the reading was too early to see" are indistinguishable.
        ///
        /// A <c>ThreadPoolTimer</c>, not the <c>DispatcherTimer</c> that
        /// <c>PlaybackService.LogSettledSoon</c> uses: the callers here are the
        /// lifecycle and memory-pressure handlers, and those do not all arrive
        /// on the UI thread (<c>MemoryManager</c> raises its events on the
        /// threadpool). <see cref="LogMemory"/> reads only process-wide
        /// counters, so it has no thread affinity of its own.
        ///
        /// The line stamps the delay actually elapsed. A backgrounded app can
        /// be suspended before the timer fires — if a reading is missing, or
        /// its elapsed time is far past the requested delay, that is the
        /// explanation and the figure should not be trusted.
        /// </summary>
        public static void LogMemoryAfterDelay(string stage, TimeSpan delay)
        {
            var requestedAt = DateTimeOffset.Now;
            try
            {
                ThreadPoolTimer.CreateTimer(
                    _ =>
                    {
                        double elapsed = (DateTimeOffset.Now - requestedAt).TotalSeconds;
                        LogMemory(stage + " (+" + elapsed.ToString("0.0") + "s)");
                        FlushSoon();
                    },
                    delay);
            }
            catch (Exception ex)
            {
                // Diagnostics must never be the reason the app misbehaves.
                Log(stage + ": delayed reading could not be scheduled — " + ex.Message);
            }
        }

        /// <summary>
        /// Starts a flush without waiting for it. Used from lifecycle and
        /// memory events, which must return promptly.
        /// </summary>
        public static void FlushSoon()
        {
            _ = FlushAsync();
        }

        /// <summary>
        /// Drains the buffer to disk. Awaited inside the suspend deferral —
        /// a termination is preceded by suspension, so this is the call that
        /// makes the final lines readable after the app is killed.
        /// </summary>
        public static async Task FlushAsync()
        {
            string[] lines;
            lock (Gate)
            {
                if (Pending.Count == 0)
                {
                    return;
                }

                lines = Pending.ToArray();
                Pending.Clear();
            }

            await FileGate.WaitAsync().ConfigureAwait(false);
            try
            {
                StorageFile file = await ApplicationData.Current.LocalFolder
                    .CreateFileAsync(FileName, CreationCollisionOption.OpenIfExists)
                    .AsTask().ConfigureAwait(false);

                // Read-trim-rewrite keeps the file bounded. The volume here is
                // a few lines per app transition, so the cost is irrelevant
                // next to keeping the log readable.
                var existing = await FileIO.ReadLinesAsync(file).AsTask().ConfigureAwait(false);
                var all = new List<string>(existing);
                all.AddRange(lines);
                if (all.Count > MaxLines)
                {
                    all.RemoveRange(0, all.Count - MaxLines);
                }

                var text = new StringBuilder();
                foreach (string line in all)
                {
                    text.AppendLine(line);
                }

                await FileIO.WriteTextAsync(file, text.ToString()).AsTask().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Diagnostics must never be the reason the app misbehaves.
                System.Diagnostics.Debug.WriteLine("BackgroundDiagnostics flush failed: " + ex.Message);
            }
            finally
            {
                FileGate.Release();
            }
        }
    }
}
