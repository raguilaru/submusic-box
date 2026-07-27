using System;
using System.Collections.Generic;
using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.Core.Browsing
{
    /// <summary>One artist's summed library figures (§7.1).</summary>
    public struct ArtistTotal
    {
        public ArtistTotal(int albumCount, int songCount, TimeSpan duration)
        {
            AlbumCount = albumCount;
            SongCount = songCount;
            Duration = duration;
        }

        public int AlbumCount { get; }
        public int SongCount { get; }
        public TimeSpan Duration { get; }
    }

    /// <summary>
    /// Sums song counts and play time per artist from album pages (§7.1,
    /// decision 2026-07-27).
    ///
    /// getArtists carries only <c>albumCount</c>, so an artist's track count and
    /// total play time are not available per row; asking the server per artist
    /// costs one getArtist per row, which is RTT-bound and unacceptable off-LAN
    /// (measured: ~19 s for 500 artists at a 150 ms RTT). Every album in
    /// getAlbumList2 already carries <c>artistId</c>, <c>songCount</c> and
    /// <c>duration</c>, so one pass over the album list yields every artist's
    /// totals at ceil(albums / pageSize) requests — a cost that scales with
    /// albums, not artists.
    ///
    /// These are ALBUM-ARTIST sums: a track credited to a guest counts under the
    /// album's artist. That is the same grouping getArtists itself indexes by, so
    /// the totals stay consistent with the album count shown beside them.
    ///
    /// Only the running sums are kept — the album pages themselves are not
    /// retained, so the memory cost is one entry per artist regardless of library
    /// size (§11).
    /// </summary>
    public sealed class ArtistTotals
    {
        // Server ids are opaque strings: compare them exactly, never by culture.
        private readonly Dictionary<string, Accumulator> _byArtist =
            new Dictionary<string, Accumulator>(StringComparer.Ordinal);

        /// <summary>Artists seen so far — the sums cover this many.</summary>
        public int ArtistCount
        {
            get { return _byArtist.Count; }
        }

        /// <summary>
        /// Folds one page of albums into the running sums. Albums the server
        /// sent without an <c>artistId</c> are skipped: they cannot be
        /// attributed to a row, and inventing an owner for them would make the
        /// totals wrong rather than incomplete.
        /// </summary>
        public void AddPage(IReadOnlyList<Album> albums)
        {
            if (albums == null)
            {
                throw new ArgumentNullException(nameof(albums));
            }

            foreach (Album album in albums)
            {
                if (album == null)
                {
                    continue;
                }

                // Null/length rather than IsNullOrEmpty: netstandard2.0's
                // signature is unannotated, so the compiler cannot see that it
                // rules the id out as null.
                string? artistId = album.ArtistId;
                if (artistId == null || artistId.Length == 0)
                {
                    continue;
                }

                Accumulator accumulator;
                if (!_byArtist.TryGetValue(artistId, out accumulator))
                {
                    accumulator = new Accumulator();
                    _byArtist[artistId] = accumulator;
                }

                accumulator.AlbumCount++;
                accumulator.SongCount += album.SongCount;
                accumulator.Duration += album.Duration;
            }
        }

        /// <summary>
        /// The totals for one artist, or false when no album in the pass named
        /// them — which a caller must treat as "unknown", not as zero.
        /// </summary>
        public bool TryGet(string? artistId, out ArtistTotal total)
        {
            Accumulator accumulator;
            if (artistId == null
                || artistId.Length == 0
                || !_byArtist.TryGetValue(artistId, out accumulator))
            {
                total = default(ArtistTotal);
                return false;
            }

            total = new ArtistTotal(
                accumulator.AlbumCount, accumulator.SongCount, accumulator.Duration);
            return true;
        }

        // A class, not a struct: the dictionary hands back a copy of a struct
        // value, so accumulating in place would silently discard every update.
        private sealed class Accumulator
        {
            public int AlbumCount;
            public int SongCount;
            public TimeSpan Duration;
        }
    }
}
