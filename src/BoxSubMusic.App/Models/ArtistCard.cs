using System;

namespace BoxSubMusic.App.Models
{
    /// <summary>
    /// Display model for one artist tile in the Artists browse view
    /// (requirements §7.1). Immutable; artwork is the authenticated getCoverArt
    /// URL or null when the server has none.
    /// </summary>
    public sealed class ArtistCard
    {
        public ArtistCard(string id, string name, string albumCountLabel, Uri artworkUri, int albumCount = 0)
        {
            Id = id;
            Name = name;
            AlbumCountLabel = albumCountLabel;
            ArtworkUri = artworkUri;
            AlbumCount = albumCount;
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

        public Uri ArtworkUri { get; }
    }
}
