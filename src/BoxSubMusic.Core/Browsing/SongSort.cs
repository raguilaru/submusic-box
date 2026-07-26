using System;
using System.Collections.Generic;
using System.Linq;
using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.Core.Browsing
{
    /// <summary>
    /// The Songs view's sort options (§7.3). The Subsonic API offers no song
    /// ordering — search3 has no sort parameter and no other endpoint lists
    /// the library in a chosen order — so sorting is client-side over the
    /// fully loaded library.
    /// </summary>
    public enum SongSort
    {
        /// <summary>The server's own order, exactly as returned. The default.</summary>
        DefaultOrder = 0,
        Title,
        Artist,
        Album,

        /// <summary>Newest first, on the <c>created</c> attribute.</summary>
        DateAdded,
    }

    /// <summary>
    /// Client-side ordering for the Songs view (§7.3). Lives in Core so the
    /// orderings are unit-testable without a UWP host.
    ///
    /// Every ordering is stable (LINQ's OrderBy is), so songs that tie on all
    /// keys keep the server's relative order — which also makes
    /// <see cref="SongSort.DefaultOrder"/> a pure pass-through.
    /// </summary>
    public static class SongSorter
    {
        // Alphabetical as a person reads it, so accented and cased titles land
        // where the user expects rather than where their code points fall.
        private static readonly StringComparer Text = StringComparer.CurrentCultureIgnoreCase;

        /// <summary>
        /// Orders <paramref name="items"/> by <paramref name="sort"/>, reading
        /// each item's song through <paramref name="selector"/> so display-row
        /// wrappers can be sorted without unwrapping them.
        /// </summary>
        public static IReadOnlyList<T> Sort<T>(
            IReadOnlyList<T> items, Func<T, Song> selector, SongSort sort)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            switch (sort)
            {
                case SongSort.DefaultOrder:
                    return items;

                case SongSort.Title:
                    return items
                        .OrderBy(i => selector(i).Title, Text)
                        .ThenBy(i => selector(i).Artist ?? string.Empty, Text)
                        .ThenBy(i => selector(i).Album ?? string.Empty, Text)
                        .ToList();

                // Grouping by artist is only useful if each artist's songs then
                // read in album order, so both artist and album sorts fall
                // through to disc/track — the §9 album ordering.
                case SongSort.Artist:
                    return items
                        .OrderBy(i => selector(i).Artist ?? string.Empty, Text)
                        .ThenBy(i => selector(i).Album ?? string.Empty, Text)
                        .ThenBy(i => selector(i).DiscNumber ?? 0)
                        .ThenBy(i => selector(i).Track ?? 0)
                        .ThenBy(i => selector(i).Title, Text)
                        .ToList();

                case SongSort.Album:
                    return items
                        .OrderBy(i => selector(i).Album ?? string.Empty, Text)
                        .ThenBy(i => selector(i).DiscNumber ?? 0)
                        .ThenBy(i => selector(i).Track ?? 0)
                        .ThenBy(i => selector(i).Title, Text)
                        .ToList();

                // Newest first — what "Date Added" means everywhere else. Songs
                // whose server omitted `created` sort as oldest, so they land
                // together at the end instead of scattering through the list.
                case SongSort.DateAdded:
                    return items
                        .OrderByDescending(i => selector(i).Created ?? DateTimeOffset.MinValue)
                        .ThenBy(i => selector(i).Title, Text)
                        .ToList();

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(sort), sort, "Unknown song sort.");
            }
        }

        /// <summary>Convenience overload for a plain song list.</summary>
        public static IReadOnlyList<Song> Sort(IReadOnlyList<Song> songs, SongSort sort)
        {
            return Sort(songs, s => s, sort);
        }

        /// <summary>
        /// The sort's menu label, kept next to the orderings so the two can
        /// never drift apart.
        /// </summary>
        public static string Label(SongSort sort)
        {
            switch (sort)
            {
                case SongSort.DefaultOrder: return "Default Order";
                case SongSort.Title: return "Title";
                case SongSort.Artist: return "Artist";
                case SongSort.Album: return "Album";
                case SongSort.DateAdded: return "Date Added";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(sort), sort, "Unknown song sort.");
            }
        }
    }
}
