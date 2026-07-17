using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BoxSubMusic.Core.Concurrency
{
    /// <summary>
    /// Bounded-concurrency fan-out (Slice 14 performance audit): runs one
    /// async worker per input with at most <c>maxConcurrent</c> in flight,
    /// preserving input order in the results. Exists because the §8 artist
    /// song pool fans out one getAlbum per album — unbounded, a 100-album
    /// artist would fire 100 simultaneous requests at what is typically a
    /// small self-hosted server. Kept in Core so it is unit-testable without
    /// a UWP host. A worker failure propagates after every started worker
    /// has settled (Task.WhenAll semantics); no new work starts once an
    /// index has been handed out past the end.
    /// </summary>
    public static class BoundedParallel
    {
        public static async Task<TResult[]> MapAsync<TSource, TResult>(
            IReadOnlyList<TSource> items,
            Func<TSource, Task<TResult>> worker,
            int maxConcurrent)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (worker == null) throw new ArgumentNullException(nameof(worker));
            if (maxConcurrent < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxConcurrent), maxConcurrent, "At least one worker is required.");
            }

            var results = new TResult[items.Count];
            if (items.Count == 0)
            {
                return results;
            }

            var cursor = new Cursor();
            var pumps = new Task[Math.Min(maxConcurrent, items.Count)];
            for (int i = 0; i < pumps.Length; i++)
            {
                pumps[i] = PumpAsync(items, worker, results, cursor);
            }

            await Task.WhenAll(pumps).ConfigureAwait(false);
            return results;
        }

        private static async Task PumpAsync<TSource, TResult>(
            IReadOnlyList<TSource> items,
            Func<TSource, Task<TResult>> worker,
            TResult[] results,
            Cursor cursor)
        {
            while (true)
            {
                int index = cursor.Take();
                if (index >= items.Count)
                {
                    return;
                }

                results[index] = await worker(items[index]).ConfigureAwait(false);
            }
        }

        /// <summary>Thread-safe claim-the-next-index counter shared by the pumps.</summary>
        private sealed class Cursor
        {
            private int _next = -1;

            public int Take()
            {
                return Interlocked.Increment(ref _next);
            }
        }
    }
}
