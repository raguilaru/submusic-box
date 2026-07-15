using BoxSubMusic.SubsonicClient.Models;
using Windows.UI.Xaml;

namespace BoxSubMusic.App.Models
{
    /// <summary>
    /// Display model for one row of the Songs browse list (requirements
    /// §7.3: title, artist, album, length, format, bitrate). Immutable.
    /// Carries its <see cref="Song"/> since Slice 10 so the row's play
    /// action can queue the real track model.
    /// </summary>
    public sealed class SongRow
    {
        public SongRow(
            Song song,
            string id,
            string albumId,
            string title,
            string artist,
            string album,
            string durationLabel,
            string format,
            string bitRateLabel)
        {
            Song = song;
            Id = id;
            AlbumId = albumId;
            Title = title;
            Artist = artist;
            Album = album;
            DurationLabel = durationLabel;
            Format = format;
            BitRateLabel = bitRateLabel;
        }

        /// <summary>The underlying track, queued by the row's play action (§7.3).</summary>
        public Song Song { get; }

        /// <summary>
        /// Shows the row's favorite-heart marker (checkpoint #4 feedback:
        /// every song list shows a heart next to starred songs). Snapshot
        /// from load time; pages refetch on navigation.
        /// </summary>
        public Visibility StarredVisibility
        {
            get { return Song.IsStarred ? Visibility.Visible : Visibility.Collapsed; }
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
