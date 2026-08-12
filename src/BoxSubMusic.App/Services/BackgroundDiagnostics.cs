using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;

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
            Log(stage
                + ": usage=" + Megabytes(MemoryManager.AppMemoryUsage) + " MB"
                + ", limit=" + Megabytes(MemoryManager.AppMemoryUsageLimit) + " MB"
                + ", level=" + MemoryManager.AppMemoryUsageLevel);
        }

        public static string Megabytes(ulong bytes)
        {
            return (bytes / (1024UL * 1024UL)).ToString();
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
