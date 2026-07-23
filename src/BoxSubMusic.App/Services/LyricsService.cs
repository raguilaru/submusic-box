using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.App.Services
{
    /// <summary>
    /// Fetches structured lyrics for the now-playing overlay's Lyrics tab
    /// (OpenSubsonic <c>songLyrics</c> extension). Kept as a thin service so
    /// the control stays a pure view: it exposes the capability gate and a
    /// per-song fetch with a one-entry cache, so re-reads driven by unrelated
    /// <see cref="PlaybackService.Changed"/> events do not refetch the same
    /// song. When the server lacks the extension, <see cref="IsSupported"/> is
    /// false and the lyrics affordance is hidden app-wide (§3).
    /// </summary>
    public static class LyricsService
    {
        private const string ExtensionName = "songLyrics";

        private static readonly object CacheLock = new object();
        private static string _cachedSongId;
        private static StructuredLyrics _cachedResult;

        /// <summary>True when the connected server advertises the songLyrics extension.</summary>
        public static bool IsSupported =>
            AppSession.Client?.Capabilities?.SupportsExtension(ExtensionName) == true;

        /// <summary>
        /// The lyrics to show for a song, or null when the song has none (or
        /// the server does not support the extension). Prefers a synced entry;
        /// falls back to the first entry the server returns. Fetch failures are
        /// treated as "no lyrics" — lyrics are a display extra, not a §15 case.
        /// </summary>
        public static async Task<StructuredLyrics> GetForSongAsync(
            string songId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(songId) || !IsSupported)
            {
                return null;
            }

            lock (CacheLock)
            {
                if (_cachedSongId == songId)
                {
                    return _cachedResult;
                }
            }

            StructuredLyrics selected = null;
            try
            {
                var all = await AppSession.Client
                    .GetLyricsBySongIdAsync(songId, cancellationToken)
                    .ConfigureAwait(false);
                selected = all.FirstOrDefault(l => l.Synced) ?? all.FirstOrDefault();
            }
            catch (Exception)
            {
                // Swallow: a lyrics fetch failure degrades to "no lyrics" in the
                // tab, never a page-level error (the tab is an extra surface).
                selected = null;
            }

            lock (CacheLock)
            {
                _cachedSongId = songId;
                _cachedResult = selected;
            }

            return selected;
        }
    }
}
