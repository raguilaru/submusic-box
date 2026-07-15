using System.ComponentModel;
using Windows.UI.Xaml;

namespace BoxSubMusic.App.Models
{
    /// <summary>
    /// Display model for one row of the album track list (requirements §9).
    /// The artist is empty when it matches the album artist, so regular
    /// albums stay uncluttered while compilations still credit each track.
    /// Observable since Slice 10: <see cref="IsCurrent"/> follows the
    /// playback service so the currently playing track is highlighted (§9).
    /// </summary>
    public sealed class TrackRow : INotifyPropertyChanged
    {
        private bool _isCurrent;

        private bool _isStarred;

        public TrackRow(string id, string number, string title, string artist, string durationLabel, string artistId, bool isStarred)
        {
            Id = id;
            Number = number;
            Title = title;
            Artist = artist;
            DurationLabel = durationLabel;
            ArtistId = artistId;
            _isStarred = isStarred;
        }

        /// <summary>
        /// Favorite state (checkpoint #4: song lists mark starred songs).
        /// Mutable + observable since the context menu can toggle it in
        /// place (checkpoint #4 third round).
        /// </summary>
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

        /// <summary>Context-menu label for the favorite toggle.</summary>
        public string FavoriteLabel
        {
            get { return _isStarred ? "Remove from favorites" : "Add to favorites"; }
        }

        public string Id { get; }

        /// <summary>Id for the context menu's "Go to artist"; may be null.</summary>
        public string ArtistId { get; }

        public bool HasArtistId
        {
            get { return !string.IsNullOrEmpty(ArtistId); }
        }

        /// <summary>Track number as display text; empty when the server has none.</summary>
        public string Number { get; }

        public string Title { get; }

        /// <summary>Per-track artist; empty when it matches the album artist.</summary>
        public string Artist { get; }

        public string DurationLabel { get; }

        /// <summary>True while this track is the queue's current track (§9 highlight).</summary>
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
                    handler(this, new PropertyChangedEventArgs(nameof(NumberVisibility)));
                    handler(this, new PropertyChangedEventArgs(nameof(NowPlayingVisibility)));
                }
            }
        }

        /// <summary>The number column swaps to the now-playing marker when current.</summary>
        public Visibility NumberVisibility
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
