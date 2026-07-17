using System.ComponentModel;
using BoxSubMusic.SubsonicClient.Models;
using Windows.UI.Xaml;

namespace BoxSubMusic.App.Models
{
    /// <summary>
    /// Display model for one row of the expanded queue (Slice 11,
    /// requirements §10). Holds its <see cref="Song"/> so A/Enter can jump
    /// the player to exactly this queue entry. Observable like
    /// <see cref="TrackRow"/>: <see cref="IsCurrent"/> swaps the position
    /// column to the now-playing marker, and <see cref="IsStarred"/> keeps
    /// the heart marker honest when the current track is starred from the
    /// overlay's own favorite toggle. Slice 14 added the row context menu,
    /// so the row also exposes the menu's favorite label and whether the
    /// server sent album/artist ids to navigate to.
    /// </summary>
    public sealed class QueueRow : INotifyPropertyChanged
    {
        private bool _isCurrent;

        private bool _isStarred;

        public QueueRow(Song song, int position, string title, string artist, string durationLabel)
        {
            Song = song;
            Position = position.ToString();
            Title = title;
            Artist = artist;
            DurationLabel = durationLabel;
            _isStarred = song.IsStarred;
        }

        /// <summary>The queue entry itself — the jump target.</summary>
        public Song Song { get; }

        /// <summary>1-based position in the current play order.</summary>
        public string Position { get; }

        public string Title { get; }

        public string Artist { get; }

        public string DurationLabel { get; }

        public bool IsStarred
        {
            get { return _isStarred; }
            set
            {
                if (_isStarred == value)
                {
                    return;
                }

                _isStarred = value;
                var handler = PropertyChanged;
                if (handler != null)
                {
                    handler(this, new PropertyChangedEventArgs(nameof(IsStarred)));
                    handler(this, new PropertyChangedEventArgs(nameof(StarredVisibility)));
                    handler(this, new PropertyChangedEventArgs(nameof(FavoriteLabel)));
                }
            }
        }

        public Visibility StarredVisibility
        {
            get { return _isStarred ? Visibility.Visible : Visibility.Collapsed; }
        }

        /// <summary>Context-menu favorite label, tracking state like the
        /// other song-row menus.</summary>
        public string FavoriteLabel
        {
            get { return _isStarred ? "Remove from favorites" : "Add to favorites"; }
        }

        /// <summary>"Go to album" is disabled when the server sent no album id.</summary>
        public bool HasAlbumId
        {
            get { return !string.IsNullOrEmpty(Song.AlbumId); }
        }

        /// <summary>"Go to artist" is disabled when the server sent no artist id.</summary>
        public bool HasArtistId
        {
            get { return !string.IsNullOrEmpty(Song.ArtistId); }
        }

        /// <summary>True while this row is the queue's current track.</summary>
        public bool IsCurrent
        {
            get { return _isCurrent; }
            set
            {
                if (_isCurrent == value)
                {
                    return;
                }

                _isCurrent = value;
                var handler = PropertyChanged;
                if (handler != null)
                {
                    handler(this, new PropertyChangedEventArgs(nameof(IsCurrent)));
                    handler(this, new PropertyChangedEventArgs(nameof(PositionVisibility)));
                    handler(this, new PropertyChangedEventArgs(nameof(NowPlayingVisibility)));
                }
            }
        }

        /// <summary>The position column swaps to the now-playing marker when current.</summary>
        public Visibility PositionVisibility
        {
            get { return _isCurrent ? Visibility.Collapsed : Visibility.Visible; }
        }

        public Visibility NowPlayingVisibility
        {
            get { return _isCurrent ? Visibility.Visible : Visibility.Collapsed; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
