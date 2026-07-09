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
        public AlbumCard(string id, string name, string artist, Uri artworkUri)
        {
            Id = id;
            Name = name;
            Artist = artist;
            ArtworkUri = artworkUri;
        }

        public string Id { get; }
        public string Name { get; }
        public string Artist { get; }
        public Uri ArtworkUri { get; }
    }
}
