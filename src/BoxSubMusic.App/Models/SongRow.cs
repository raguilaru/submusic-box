using System.ComponentModel;
using BoxSubMusic.SubsonicClient.Models;
using Windows.UI.Xaml;

namespace BoxSubMusic.App.Models
{
    /// <summary>
    /// Display model for one row of the Songs browse list (requirements
    /// §7.3: title, artist, album, length, format, bitrate). Carries its
    /// <see cref="Song"/> since Slice 10 so the row's play action can queue
    /// the real track model; observable since the context menu can toggle
    /// the favorite state in place (checkpoint #4 third round).
    /// </summary>
    public sealed class SongRow : INotifyPropertyChanged
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
        /// every song list shows a heart next to starred songs). Reads the
        /// carried Song; call <see cref="NotifyStarredChanged"/> after
        /// mutating it so the row repaints.
        /// </summary>
        public Visibility StarredVisibility
        {
            get { return Song.IsStarred ? Visibility.Visible : Visibility.Collapsed; }
        }

        /// <summary>Context-menu label for the favorite toggle.</summary>
        public string FavoriteLabel
        {
            get { return Song.IsStarred ? "Remove from favorites" : "Add to favorites"; }
        }

        /// <summary>Repaints the starred marker + menu label after Song.IsStarred changed.</summary>
        public void NotifyStarredChanged()
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(nameof(StarredVisibility)));
                handler(this, new PropertyChangedEventArgs(nameof(FavoriteLabel)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

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
