using System;
using Windows.UI.Xaml;

namespace BoxSubMusic.App.Models
{
    /// <summary>
    /// Display model for one artist tile in the Artists browse view
    /// (requirements §7.1). Immutable; artwork is the authenticated getCoverArt
    /// URL or null when the server has none.
    /// </summary>
    public sealed class ArtistCard
    {
        public ArtistCard(
            string id, string name, string albumCountLabel, Uri artworkUri,
            int albumCount = 0, bool isStarred = false)
        {
            Id = id;
            Name = name;
            AlbumCountLabel = albumCountLabel;
            ArtworkUri = artworkUri;
            AlbumCount = albumCount;
            IsStarred = isStarred;
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>Pre-formatted subtitle, e.g. "3 albums".</summary>
        public string AlbumCountLabel { get; }

        /// <summary>
        /// The raw count behind <see cref="AlbumCountLabel"/>, so a view can
        /// sort by it (the Favorites "Most Albums" order) without re-mapping
        /// cards — re-mapping would mint fresh cover-art URLs and defeat the
        /// image cache. Optional: pages that never sort leave it unset.
        /// </summary>
        public int AlbumCount { get; }

        /// <summary>
        /// Favorite state, for the list row's starred marker. getArtists gives
        /// the album count and this flag and nothing else — an artist's song
        /// count and total play time would cost one getArtist per row, so they
        /// live on the artist detail page instead (§8, decision 2026-07-27).
        /// </summary>
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

        public Uri ArtworkUri { get; }
    }
}
