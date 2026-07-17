using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BoxSubMusic.App.Models;
using BoxSubMusic.App.Services;
using BoxSubMusic.Core.Playback;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using Windows.Media.Playback;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace BoxSubMusic.App.Controls
{
    /// <summary>
    /// The expanded two-pane queue (Slice 11, requirements §10). Like the
    /// compact bar it is purely a view over <see cref="PlaybackService"/>:
    /// every <see cref="PlaybackService.Changed"/> re-reads what it shows.
    /// The queue rows are rebuilt only when the play order itself changed
    /// (replace, shuffle toggle) — mere advancement just repaints the
    /// now-playing markers, so scroll position and focus survive track
    /// changes. The host owns opening/closing policy (Y/B mapping, focus
    /// trapping); this control owns the slide animation and its content.
    /// </summary>
    public sealed partial class ExpandedQueueView : UserControl
    {
        private const string PlayGlyph = "\uF606";
        private const string PauseGlyph = "\uF5A2";
        private const string RepeatAllGlyph = "\uF172";
        private const string RepeatOneGlyph = "\uEF34";

        // Artwork pane is 400 epx; request double for the TV's pixel density.
        private const int ArtSize = 800;

        private const int FocusAttempts = 5;

        private readonly DispatcherTimer _timer;
        private readonly List<QueueRow> _rows = new List<QueueRow>();
        private PlaybackService _playback;
        private IReadOnlyList<Song> _rowOrderSnapshot = new Song[0];
        private bool _suppressShuffleEvents;
        private bool _suppressFavoriteEvents;
        private string _artworkSongId;

        public ExpandedQueueView()
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (s, e) => UpdatePosition();
            CloseStoryboard.Completed += CloseStoryboard_Completed;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>True from Open() until Close() — during the closing
        /// animation the view already counts as closed.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Raised when the closing slide has finished and the view
        /// is hidden; the host re-enables the shell and restores focus.</summary>
        public event Action Closed;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _playback = PlaybackService.Instance;
            _playback.Changed += OnPlaybackChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_playback != null)
            {
                _playback.Changed -= OnPlaybackChanged;
                _playback = null;
            }

            _timer.Stop();
        }

        /// <summary>Slides the view up over the shell. No-op when the queue
        /// is empty (there is nothing to show).</summary>
        public void Open()
        {
            if (IsOpen || _playback == null || _playback.Queue.IsEmpty)
            {
                return;
            }

            IsOpen = true;
            CloseStoryboard.Stop();
            Root.Visibility = Visibility.Visible;
            OpenTranslate.From = ViewHeight();
            OpenStoryboard.Begin();

            Refresh(forceRowRebuild: true);
            FocusCurrentRow(FocusAttempts);
        }

        /// <summary>Slides the view back down; <see cref="Closed"/> fires
        /// when the animation completes.</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            _timer.Stop();
            OpenStoryboard.Stop();
            CloseTranslate.To = ViewHeight();
            CloseStoryboard.Begin();
        }

        private double ViewHeight()
        {
            // Before the first layout pass ActualHeight is 0; the design
            // canvas height is the honest fallback (1080, scaling disabled).
            return ActualHeight > 0 ? ActualHeight : 1080;
        }

        private void CloseStoryboard_Completed(object sender, object e)
        {
            if (!IsOpen)
            {
                Root.Visibility = Visibility.Collapsed;
                Closed?.Invoke();
            }
        }

        // --- View over the playback service ------------------------------------

        private void OnPlaybackChanged()
        {
            if (IsOpen)
            {
                Refresh(forceRowRebuild: false);
            }
        }

        private void Refresh(bool forceRowRebuild)
        {
            PlayQueue queue = _playback.Queue;
            Song song = queue.CurrentTrack;
            if (song == null)
            {
                // The queue died under the open view (e.g. the consecutive-
                // failure stop). Nothing left to show.
                Close();
                return;
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

            _suppressFavoriteEvents = true;
            FavoriteToggle.IsChecked = song.IsStarred;
            _suppressFavoriteEvents = false;

            RepeatIcon.Glyph = queue.RepeatMode == RepeatMode.One ? RepeatOneGlyph : RepeatAllGlyph;
            RepeatIcon.Opacity = queue.RepeatMode == RepeatMode.Off ? 0.45 : 1.0;

            QueueContextText.Text = JoinWithDots(
                queue.Context?.DisplayName,
                queue.PlayOrder.Count == 1 ? "1 song" : queue.PlayOrder.Count + " songs");

            if (forceRowRebuild || PlayOrderChanged(queue))
            {
                RebuildRows(queue);
            }
            else
            {
                foreach (QueueRow row in _rows)
                {
                    row.IsCurrent = ReferenceEquals(row.Song, song);
                    row.IsStarred = row.Song.IsStarred;
                }
            }

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

        private bool PlayOrderChanged(PlayQueue queue)
        {
            IReadOnlyList<Song> order = queue.PlayOrder;
            if (order.Count != _rowOrderSnapshot.Count)
            {
                return true;
            }

            for (int i = 0; i < order.Count; i++)
            {
                if (!ReferenceEquals(order[i], _rowOrderSnapshot[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildRows(PlayQueue queue)
        {
            Song current = queue.CurrentTrack;
            var snapshot = new List<Song>(queue.PlayOrder);
            _rows.Clear();
            for (int i = 0; i < snapshot.Count; i++)
            {
                Song song = snapshot[i];
                var row = new QueueRow(song, i + 1, song.Title, song.Artist, FormatTime(song.Duration))
                {
                    IsCurrent = ReferenceEquals(song, current),
                };
                _rows.Add(row);
            }

            _rowOrderSnapshot = snapshot;
            QueueList.ItemsSource = null;
            QueueList.ItemsSource = _rows;
            ScrollToCurrent();
        }

        private QueueRow CurrentRow()
        {
            foreach (QueueRow row in _rows)
            {
                if (row.IsCurrent)
                {
                    return row;
                }
            }

            return null;
        }

        private void ScrollToCurrent()
        {
            QueueRow row = CurrentRow();
            if (row != null)
            {
                QueueList.ScrollIntoView(row, ScrollIntoViewAlignment.Leading);
            }
        }

        private void FocusCurrentRow(int attemptsLeft)
        {
            // Same deferred-pass idea as ContentFocus: the list containers
            // only exist after a layout pass, so retry on a low-priority
            // dispatcher tick until the current row's container is focusable.
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                if (!IsOpen)
                {
                    return;
                }

                UpdateLayout();
                QueueRow row = CurrentRow();
                if (row != null)
                {
                    QueueList.ScrollIntoView(row, ScrollIntoViewAlignment.Leading);
                    if (QueueList.ContainerFromItem(row) is ListViewItem container
                        && container.Focus(FocusState.Keyboard))
                    {
                        return;
                    }
                }

                if (attemptsLeft > 1)
                {
                    FocusCurrentRow(attemptsLeft - 1);
                }
                else
                {
                    // Give up on the row (empty queue race): the transport
                    // cluster is always there.
                    PlayPauseButton.Focus(FocusState.Keyboard);
                }
            });
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
                    UriSource = client.GetCoverArtUri(song.CoverArtId, ArtSize),
                    DecodePixelWidth = ArtSize,
                };
        }

        private void UpdatePosition()
        {
            MediaPlaybackSession session = _playback.Session;
            TimeSpan position = session.Position;

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

        private void QueueList_ContainerContentChanging(
            ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            // Console checkpoint #6 finding: Left from a queue row landed on
            // whatever transport control happened to be vertically nearest —
            // shuffle from one row, previous from another. Pin every row's
            // Left to the primary play/pause button so the exit is one
            // consistent, predictable target (in-tree XYFocus hints are
            // console-verified tech; a hint must live on the focusable
            // container itself, not the template content).
            args.ItemContainer.XYFocusLeft = PlayPauseButton;
        }

        private void QueueList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is QueueRow row)
            {
                // §10: jump directly to any queued track. The view stays
                // open; the marker repaint comes back through Changed.
                _playback.JumpTo(row.Song);
            }
        }

        private void CollapseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

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

                // The row's heart marker follows immediately — same surface,
                // it must not wait for the next queue event.
                foreach (QueueRow row in _rows)
                {
                    if (ReferenceEquals(row.Song, song))
                    {
                        row.IsStarred = wantStarred;
                    }
                }
            }
            catch (SubsonicClientException)
            {
                // Same failure feedback as the bar: the visible snap-back.
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
