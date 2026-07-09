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
        public ArtistCard(string id, string name, string albumCountLabel, Uri artworkUri)
        {
            Id = id;
            Name = name;
            AlbumCountLabel = albumCountLabel;
            ArtworkUri = artworkUri;
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>Pre-formatted subtitle, e.g. "3 albums".</summary>
        public string AlbumCountLabel { get; }

        public Uri ArtworkUri { get; }
    }
}
