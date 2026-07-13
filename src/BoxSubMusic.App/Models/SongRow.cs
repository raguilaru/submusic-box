namespace BoxSubMusic.App.Models
{
    /// <summary>
    /// Display model for one row of the Songs browse list (requirements
    /// §7.3: title, artist, album, length, format, bitrate). Immutable.
    /// </summary>
    public sealed class SongRow
    {
        public SongRow(
            string id,
            string albumId,
            string title,
            string artist,
            string album,
            string durationLabel,
            string format,
            string bitRateLabel)
        {
            Id = id;
            AlbumId = albumId;
            Title = title;
            Artist = artist;
            Album = album;
            DurationLabel = durationLabel;
            Format = format;
            BitRateLabel = bitRateLabel;
        }

        public string Id { get; }

        /// <summary>Album id for "Go to album"; null when the server gave none.</summary>
        public string AlbumId { get; }

        public string Title { get; }
        public string Artist { get; }
        public string Album { get; }
        public string DurationLabel { get; }

        /// <summary>Display format, e.g. "FLAC"; empty when unknown.</summary>
        public string Format { get; }

        /// <summary>Display bitrate, e.g. "256 kbps"; empty when unknown.</summary>
        public string BitRateLabel { get; }
    }
}
