using System;
using System.Text;
using BoxSubMusic.Core.Browsing;
using Windows.UI.Xaml;

namespace BoxSubMusic.App.Models
{
    /// <summary>
    /// Display model for one album tile in a Home carousel (requirements
    /// §6.2–§6.4) and for the Albums browse views (§7.2). Immutable; artwork is
    /// the authenticated getCoverArt URL or null when the server has none.
    /// </summary>
    public sealed class AlbumCard
    {
        public AlbumCard(
            string id, string name, string artist, Uri artworkUri,
            int? year = null, string artistId = null,
            int songCount = 0, TimeSpan? duration = null, string genre = null,
            bool isStarred = false)
        {
            Id = id;
            Name = name;
            Artist = artist;
            ArtworkUri = artworkUri;
            Year = year;
            ArtistId = artistId;
            SongCount = songCount;
            Duration = duration;
            Genre = genre;
            IsStarred = isStarred;
        }

        public string Id { get; }
        public string Name { get; }
        public string Artist { get; }
        public Uri ArtworkUri { get; }

        /// <summary>
        /// Release year, carried so a view can sort by it (the Favorites
        /// "Year" order) without re-mapping cards — re-mapping would mint
        /// fresh cover-art URLs and defeat the image cache. Optional: pages
        /// that never sort leave it unset.
        /// </summary>
        public int? Year { get; }

        /// <summary>
        /// Artist id for a "Go to artist" menu item; null when the server sent
        /// none or the page never needed it.
        /// </summary>
        public string ArtistId { get; }

        /// <summary>Disables "Go to artist" when there is no artist id.</summary>
        public bool HasArtistId
        {
            get { return !string.IsNullOrEmpty(ArtistId); }
        }

        /// <summary>
        /// Track count from the album's own <c>songCount</c>; 0 when the page
        /// did not carry it. Feeds <see cref="MetaLabel"/> (§7.2).
        /// </summary>
        public int SongCount { get; }

        /// <summary>
        /// Total play time from the album's own <c>duration</c>; null when the
        /// page did not carry it. Feeds <see cref="MetaLabel"/> (§7.2).
        /// </summary>
        public TimeSpan? Duration { get; }

        /// <summary>Genre when the server tagged one; else null (§7.2).</summary>
        public string Genre { get; }

        /// <summary>Favorite state, for the list row's starred marker.</summary>
        public bool IsStarred { get; }

        /// <summary>
        /// Shows the row's favorite-heart marker, matching the song lists'
        /// convention (checkpoint #4). Cards on pages that never carried the
        /// flag simply never show it.
        /// </summary>
        public Visibility StarredVisibility
        {
            get { return IsStarred ? Visibility.Visible : Visibility.Collapsed; }
        }

        /// <summary>
        /// The list row's secondary line (§7.2, 2026-07-27): year, genre, track
        /// count and total play time — everything getAlbumList2 already returns,
        /// so the richer row costs no extra request. Each part appears only when
        /// the server actually sent it, so a sparse library reads as a shorter
        /// line rather than as zeros (§3).
        /// </summary>
        public string MetaLabel
        {
            get
            {
                var parts = new StringBuilder();
                Append(parts, Year.HasValue ? Year.Value.ToString() : null);
                Append(parts, string.IsNullOrWhiteSpace(Genre) ? null : Genre);
                Append(parts, SongCount > 0
                    ? (SongCount == 1 ? "1 track" : SongCount + " tracks")
                    : null);
                Append(parts, Duration.HasValue && Duration.Value > TimeSpan.Zero
                    ? DurationText.Total(Duration.Value)
                    : null);
                return parts.ToString();
            }
        }

        /// <summary>Hides the meta line entirely when nothing is known.</summary>
        public Visibility MetaVisibility
        {
            get { return MetaLabel.Length == 0 ? Visibility.Collapsed : Visibility.Visible; }
        }

        // Same "  ·  " separator the detail headers use, so the two read as one
        // family.
        private static void Append(StringBuilder parts, string value)
        {
            if (value == null)
            {
                return;
            }

            if (parts.Length > 0)
            {
                parts.Append("  ·  ");
            }

            parts.Append(value);
        }
    }
}
