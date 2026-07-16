using System;
using System.Globalization;
using System.Text;
using BoxSubMusic.Core.Playback;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using BoxSubMusic.App.Services;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace BoxSubMusic.App.Controls
{
    /// <summary>
    /// The persistent now-playing bar (Slice 10, requirements §10). Purely a
    /// view over <see cref="PlaybackService"/>: it re-reads the queue and
    /// session on every <see cref="PlaybackService.Changed"/> (always raised
    /// on the UI thread) and runs a half-second DispatcherTimer for the
    /// elapsed-time display while playing. Repeat state is signaled by glyph
    /// + opacity only — never by painting the glyph's Foreground, which was
    /// invisible on console both ways (favorite-heart lesson, 2026-07-14).
    /// </summary>
    public sealed partial class NowPlayingBar : UserControl
    {
        // Codepoints verified against the shipped Fluent TTF's cmap (§13.3).
        private const string PlayGlyph = "\uF606";
        private const string PauseGlyph = "\uF5A2";
        private const string RepeatAllGlyph = "\uF172";
        private const string RepeatOneGlyph = "\uEF34";

        // Thumbnail is 84 epx; request double for the TV's pixel density.
        private const int ThumbArtSize = 168;

        private readonly DispatcherTimer _timer;
        private PlaybackService _playback;
        private bool _revealed;
        private bool _suppressShuffleEvents;
        private bool _suppressFavoriteEvents;
        private string _artworkSongId;

        /// <summary>
        /// The queue-expand button was invoked (§10). The shell owns the
        /// expanded view (Slice 11), so the bar only reports the request.
        /// </summary>
        public event Action QueueExpandRequested;

        /// <summary>
        /// Focus target for the shell when the expanded queue closes: the
        /// control the user used to open it.
        /// </summary>
        public void FocusQueueButton()
        {
            QueueButton.Focus(FocusState.Keyboard);
        }

        public NowPlayingBar()
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (s, e) => UpdatePosition();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _playback = PlaybackService.Instance;
            _playback.Changed += Update;
            Update();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_playback != null)
            {
                _playback.Changed -= Update;
                _playback = null;
            }

            _timer.Stop();
        }

        private void Update()
        {
            PlayQueue queue = _playback.Queue;
            Song song = queue.CurrentTrack;
            if (song == null)
            {
                // Stop/sign-out: the bar disappears until the next Play.
                Root.Visibility = Visibility.Collapsed;
                _revealed = false;
                _artworkSongId = null;
                _timer.Stop();
                return;
            }

            if (!_revealed)
            {
                _revealed = true;
                Root.Visibility = Visibility.Visible;
                RevealStoryboard.Begin();
            }

            TitleText.Text = song.Title;
            ArtistAlbumText.Text = JoinWithDots(song.Artist, song.Album);
            StreamInfoText.Text = JoinWithDots(
                song.Format?.ToUpperInvariant(),
                song.BitRate.HasValue
                    ? song.BitRate.Value.ToString(CultureInfo.InvariantCulture) + " kbps"
                    : null);
            UpdateArtwork(song);

            PlayPauseIcon.Glyph = _playback.IsPlaying ? PauseGlyph : PlayGlyph;

            _suppressShuffleEvents = true;
            ShuffleToggle.IsChecked = queue.ShuffleEnabled;
            _suppressShuffleEvents = false;

            // Favorite heart follows the current track (checkpoint #4
            // request). A successful toggle writes back to the Song model,
            // so this re-read stays honest across track changes.
            _suppressFavoriteEvents = true;
            FavoriteToggle.IsChecked = song.IsStarred;
            _suppressFavoriteEvents = false;

            RepeatIcon.Glyph = queue.RepeatMode == RepeatMode.One ? RepeatOneGlyph : RepeatAllGlyph;
            RepeatIcon.Opacity = queue.RepeatMode == RepeatMode.Off ? 0.45 : 1.0;

            UpdatePosition();
            if (_playback.IsPlaying)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
        }

        private void UpdateArtwork(Song song)
        {
            if (song.Id == _artworkSongId)
            {
                return;
            }

            _artworkSongId = song.Id;
            var client = AppSession.Client;
            ArtworkImage.Source = song.CoverArtId == null || client == null
                ? null
                : new BitmapImage
                {
                    UriSource = client.GetCoverArtUri(song.CoverArtId, ThumbArtSize),
                    DecodePixelWidth = ThumbArtSize,
                };
        }

        private void UpdatePosition()
        {
            MediaPlaybackSession session = _playback.Session;
            TimeSpan position = session.Position;

            // The session only knows the duration once the stream has opened;
            // until then the tag metadata is the best estimate.
            TimeSpan duration = session.NaturalDuration;
            Song song = _playback.Queue.CurrentTrack;
            if (duration <= TimeSpan.Zero && song != null)
            {
                duration = song.Duration;
            }

            ElapsedText.Text = FormatTime(position);
            TotalText.Text = FormatTime(duration);
            TrackProgress.Maximum = Math.Max(1, duration.TotalSeconds);
            TrackProgress.Value = Math.Min(position.TotalSeconds, TrackProgress.Maximum);
        }

        private static string FormatTime(TimeSpan time)
        {
            if (time < TimeSpan.Zero)
            {
                time = TimeSpan.Zero;
            }

            return time.TotalHours >= 1
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:mm\\:ss}", (int)time.TotalHours, time)
                : time.ToString("m\\:ss", CultureInfo.InvariantCulture);
        }

        private static string JoinWithDots(string first, string second)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(first))
            {
                sb.Append(first);
            }

            if (!string.IsNullOrEmpty(second))
            {
                if (sb.Length > 0)
                {
                    sb.Append("  ·  ");
                }

                sb.Append(second);
            }

            return sb.ToString();
        }

        // --- Control handlers --------------------------------------------------

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            _playback.TogglePlayPause();
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            _playback.Previous();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            _playback.Next();
        }

        private void ShuffleToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (!_suppressShuffleEvents)
            {
                _playback.SetShuffle(ShuffleToggle.IsChecked == true);
            }
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            _playback.CycleRepeat();
        }

        private void QueueButton_Click(object sender, RoutedEventArgs e)
        {
            QueueExpandRequested?.Invoke();
        }

        private async void FavoriteToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressFavoriteEvents)
            {
                return;
            }

            Song song = _playback.Queue.CurrentTrack;
            var client = AppSession.Client;
            if (song == null || client == null)
            {
                return;
            }

            bool wantStarred = FavoriteToggle.IsChecked == true;
            FavoriteToggle.IsEnabled = false;
            try
            {
                if (wantStarred)
                {
                    await client.StarAsync(song.Id);
                }
                else
                {
                    await client.UnstarAsync(song.Id);
                }

                song.IsStarred = wantStarred;
            }
            catch (SubsonicClientException)
            {
                // Persist failed: revert so the heart never lies about what
                // the server holds (§8). The bar has no room for a status
                // line; the visible snap-back is the feedback.
                _suppressFavoriteEvents = true;
                FavoriteToggle.IsChecked = !wantStarred;
                _suppressFavoriteEvents = false;
            }
            finally
            {
                FavoriteToggle.IsEnabled = true;
            }
        }
    }
}
