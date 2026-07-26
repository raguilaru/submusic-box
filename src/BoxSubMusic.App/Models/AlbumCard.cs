using System;

namespace BoxSubMusic.App.Models
{
    /// <summary>
    /// Display model for one album tile in a Home carousel (requirements
    /// §6.2–§6.4). Immutable; artwork is the authenticated getCoverArt URL
    /// or null when the server has none.
    /// </summary>
    public sealed class AlbumCard
    {
        public AlbumCard(
            string id, string name, string artist, Uri artworkUri,
            int? year = null, string artistId = null)
        {
            Id = id;
            Name = name;
            Artist = artist;
            ArtworkUri = artworkUri;
            Year = year;
            ArtistId = artistId;
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
    }
}
