using System;
using System.Collections.Generic;
using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.Core.Playlists
{
    /// <summary>
    /// Client-side composition of a playlist's tile artwork (requirements §7.4):
    /// a composite of up to 4 distinct album covers when the playlist spans
    /// multiple albums, collapsing to a single cover when every track shares one
    /// album. This is a client responsibility and does not depend on any server
    /// artwork endpoint. Kept in Core so it is unit-testable without a UWP host.
    /// </summary>
    public static class PlaylistArtwork
    {
        /// <summary>Maximum distinct album covers in a composite tile (§7.4).</summary>
        public const int MaxCovers = 4;

        /// <summary>
        /// Selects the cover art ids for a playlist's tile: one entry per
        /// distinct album (deduped by album, falling back to the cover id when
        /// the album is unknown), in the playlist's track order, skipping tracks
        /// with no cover art, capped at <see cref="MaxCovers"/>. A playlist whose
        /// tracks all share one album yields a single id (the §7.4 single-cover
        /// fallback); an artless playlist yields an empty list.
        /// </summary>
        public static IReadOnlyList<string> SelectCoverArtIds(IEnumerable<Song> songs, int max = MaxCovers)
        {
            if (songs == null) throw new ArgumentNullException(nameof(songs));
            if (max < 1) throw new ArgumentOutOfRangeException(nameof(max), max, "At least one cover is required.");

            var covers = new List<string>(max);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var song in songs)
            {
                if (song == null || string.IsNullOrEmpty(song.CoverArtId))
                {
                    continue;
                }

                var coverArtId = song.CoverArtId!;

                // Distinctness is per album; when the album is unknown the cover
                // id itself keys the dedupe so identical art is not repeated.
                var key = !string.IsNullOrEmpty(song.AlbumId) ? song.AlbumId! : coverArtId;
                if (seen.Add(key))
                {
                    covers.Add(coverArtId);
                    if (covers.Count >= max)
                    {
                        break;
                    }
                }
            }

            return covers;
        }
    }
}
