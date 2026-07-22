using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BoxSubMusic.App.Models;
using BoxSubMusic.App.Services;
using BoxSubMusic.Core.Playback;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using Windows.Foundation;
using Windows.Media.Playback;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
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

        // Seconds of no manual lyric navigation before auto-follow resumes.
        private const double LyricsIdleResumeSeconds = 4.0;

        private enum PaneMode
        {
            Queue,
            Lyrics,
        }

        private readonly DispatcherTimer _timer;
        private readonly List<QueueRow> _rows = new List<QueueRow>();
        private readonly List<LyricLineRow> _lyricRows = new List<LyricLineRow>();
        private readonly List<int?> _lyricStartsMs = new List<int?>();
        private PlaybackService _playback;
        private IReadOnlyList<Song> _rowOrderSnapshot = new Song[0];
        private bool _suppressShuffleEvents;
        private bool _suppressFavoriteEvents;
        private string _artworkSongId;

        // Lyrics tab state.
        private PaneMode _paneMode = PaneMode.Queue;
        private bool _lyricsSupported;
        private string _lyricsSongId;
        private int _lyricsRequestToken;
        private bool _lyricsSynced;
        private int _lyricsOffsetMs;
        private int _currentLyricIndex = -1;
        private bool _autoFollow = true;
        private DateTimeOffset _lastLyricInteraction;
        private ScrollViewer _lyricsScrollViewer;

        public ExpandedQueueView()
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (s, e) => OnTimerTick();
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

        /// <summary>Raised by the row menu's "Go to album" (Slice 14): the
        /// host closes the overlay and navigates the content frame — the
        /// control has no frame of its own.</summary>
        public event Action<string> AlbumRequested;

        /// <summary>Raised by the row menu's "Go to artist" (Slice 14).</summary>
        public event Action<string> ArtistRequested;

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

        /// <summary>Slides the view up over the shell on the Queue tab.</summary>
        public void Open()
        {
            Open(showLyrics: false);
        }

        /// <summary>Slides the view up over the shell. No-op when the queue is
        /// empty (there is nothing to show). <paramref name="showLyrics"/> opens
        /// straight to the Lyrics tab (the bar's lyrics button); it is ignored
        /// when the server has no songLyrics extension.</summary>
        public void Open(bool showLyrics)
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

            ConfigureLyricsAvailability();
            bool lyrics = showLyrics && _lyricsSupported;
            SetPaneMode(lyrics ? PaneMode.Lyrics : PaneMode.Queue, moveFocus: false);

            // Refresh loads the current song's lyrics (guarded on song id), so
            // the Lyrics tab is ready whichever tab we open on.
            Refresh(forceRowRebuild: true);

            if (_paneMode == PaneMode.Lyrics)
            {
                FocusLyrics(FocusAttempts);
            }
            else
            {
                FocusCurrentRow(FocusAttempts);
            }
        }

        private void ConfigureLyricsAvailability()
        {
            // Capability gate (§3): with no songLyrics extension the whole
            // Lyrics affordance is hidden — the tab strip collapses back to a
            // plain "Queue" title and the pane stays queue-only.
            _lyricsSupported = LyricsService.IsSupported;
            TabStrip.Visibility = _lyricsSupported ? Visibility.Visible : Visibility.Collapsed;
            HeaderTitleText.Visibility = _lyricsSupported ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SetPaneMode(PaneMode mode, bool moveFocus)
        {
            if (!_lyricsSupported)
            {
                mode = PaneMode.Queue;
            }

            _paneMode = mode;
            bool queue = mode == PaneMode.Queue;

            QueueList.Visibility = queue ? Visibility.Visible : Visibility.Collapsed;
            LyricsPanel.Visibility = queue ? Visibility.Collapsed : Visibility.Visible;
            QueueHints.Visibility = queue ? Visibility.Visible : Visibility.Collapsed;
            LyricsHints.Visibility = queue ? Visibility.Collapsed : Visibility.Visible;

            // Keep the tab toggles' checked state in sync (a click on the
            // already-active tab toggled it off — put it back).
            QueueTabButton.IsChecked = queue;
            LyricsTabButton.IsChecked = !queue;

            UpdateJumpButton();

            if (!queue)
            {
                // Entering the Lyrics tab: recompute the highlight for the
                // current position and (if playing) let the timer follow.
                _autoFollow = true;
                UpdateLyricHighlight();
            }

            if (moveFocus)
            {
                if (queue)
                {
                    FocusCurrentRow(FocusAttempts);
                }
                else
                {
                    FocusLyrics(FocusAttempts);
                }
            }
        }

        private void QueueTabButton_Click(object sender, RoutedEventArgs e)
        {
            SetPaneMode(PaneMode.Queue, moveFocus: true);
        }

        private void LyricsTabButton_Click(object sender, RoutedEventArgs e)
        {
            SetPaneMode(PaneMode.Lyrics, moveFocus: true);
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
            // Delivered-stream info over tag values, same rule as the bar
            // (§10 display fix 2026-07-19).
            string liveFormat;
            int? liveKbps;
            StreamInfoText.Text = _playback.TryGetDeliveredStreamInfo(out liveFormat, out liveKbps)
                ? JoinWithDots(
                    liveFormat,
                    liveKbps.HasValue
                        ? liveKbps.Value.ToString(CultureInfo.InvariantCulture) + " kbps"
                        : null)
                : JoinWithDots(
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

            // Lyrics follow the current track (guarded on song id so an
            // unrelated Changed does not refetch). Handles the mid-view song
            // change case — new song → new lyrics, or the no-lyrics message.
            LoadLyricsForCurrentSong(force: false);

            UpdatePosition();
            if (_playback.IsPlaying)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
                // Still refresh the highlight once so a paused seek (via SMTC)
                // lands on the right line without waiting for a tick.
                UpdateLyricHighlight();
            }
        }

        private void OnTimerTick()
        {
            UpdatePosition();
            UpdateLyricHighlight();
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

        // --- Lyrics tab (songLyrics extension) ---------------------------------

        private async void LoadLyricsForCurrentSong(bool force)
        {
            if (!_lyricsSupported || _playback == null)
            {
                return;
            }

            Song song = _playback.Queue.CurrentTrack;
            if (song == null)
            {
                return;
            }

            if (!force && song.Id == _lyricsSongId)
            {
                return;
            }

            _lyricsSongId = song.Id;
            int myToken = ++_lyricsRequestToken;
            ResetLyricsState();
            ShowLyricsMessage("Loading lyrics…");

            StructuredLyrics result = await LyricsService.GetForSongAsync(song.Id);

            // A newer request (song changed, or a forced reload) supersedes
            // this one — drop the stale result silently.
            if (myToken != _lyricsRequestToken)
            {
                return;
            }

            ApplyLyrics(result);
        }

        private void ResetLyricsState()
        {
            foreach (LyricLineRow row in _lyricRows)
            {
                row.IsCurrent = false;
            }

            _lyricRows.Clear();
            _lyricStartsMs.Clear();
            LyricsList.ItemsSource = null;
            _lyricsSynced = false;
            _lyricsOffsetMs = 0;
            _currentLyricIndex = -1;
            _autoFollow = true;
            UpdateLyricsHints();
            UpdateJumpButton();
        }

        private void ApplyLyrics(StructuredLyrics lyrics)
        {
            if (lyrics == null || lyrics.Lines.Count == 0)
            {
                // No lyrics for this track (or the fetch failed — lyrics are a
                // display extra, so a failure reads the same, never an error).
                _lyricRows.Clear();
                _lyricStartsMs.Clear();
                LyricsList.ItemsSource = null;
                _lyricsSynced = false;
                _currentLyricIndex = -1;
                ShowLyricsMessage("No lyrics available for this track.");
                UpdateLyricsHints();
                UpdateJumpButton();
                return;
            }

            // Treat an entry as synced only when it both claims to be and has at
            // least one usable timestamp — a "synced" entry with no starts is
            // rendered as plain text, never a highlight that can never advance.
            _lyricsSynced = lyrics.Synced && lyrics.Lines.Any(l => l.StartMs.HasValue);
            _lyricsOffsetMs = lyrics.OffsetMs;

            _lyricRows.Clear();
            _lyricStartsMs.Clear();
            foreach (LyricLine line in lyrics.Lines)
            {
                _lyricRows.Add(new LyricLineRow(line.Text, _lyricsSynced));
                _lyricStartsMs.Add(line.StartMs);
            }

            LyricsList.ItemsSource = _lyricRows;
            HideLyricsMessage();
            _currentLyricIndex = -1;
            _autoFollow = true;
            UpdateLyricsHints();
            UpdateJumpButton();

            if (_paneMode == PaneMode.Lyrics)
            {
                UpdateLyricHighlight();
                FocusLyrics(FocusAttempts);
            }
        }

        private void ShowLyricsMessage(string text)
        {
            LyricsMessage.Text = text;
            LyricsMessage.Visibility = Visibility.Visible;
            LyricsList.Visibility = Visibility.Collapsed;
        }

        private void HideLyricsMessage()
        {
            LyricsMessage.Visibility = Visibility.Collapsed;
            LyricsList.Visibility = Visibility.Visible;
        }

        private void UpdateLyricsHints()
        {
            // "A · Jump to current line" only makes sense for synced lyrics
            // with lines — unsynced/no-lyrics leave just the close hints.
            LyricsJumpHint.Visibility = _lyricsSynced && _lyricRows.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateJumpButton()
        {
            JumpToCurrentButton.Visibility =
                _paneMode == PaneMode.Lyrics && _lyricsSynced && !_autoFollow && _lyricRows.Count > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void UpdateLyricHighlight()
        {
            if (_paneMode != PaneMode.Lyrics || !_lyricsSynced || _lyricRows.Count == 0 || _playback == null)
            {
                return;
            }

            long positionMs = (long)_playback.Session.Position.TotalMilliseconds + _lyricsOffsetMs;
            int index = FindCurrentLine(positionMs);
            if (index != _currentLyricIndex)
            {
                if (_currentLyricIndex >= 0 && _currentLyricIndex < _lyricRows.Count)
                {
                    _lyricRows[_currentLyricIndex].IsCurrent = false;
                }

                _currentLyricIndex = index;
                if (index >= 0)
                {
                    _lyricRows[index].IsCurrent = true;
                    if (_autoFollow)
                    {
                        CenterLyric(index);
                    }
                }
            }

            // Resume auto-follow after a spell of no manual navigation.
            if (!_autoFollow &&
                (DateTimeOffset.UtcNow - _lastLyricInteraction).TotalSeconds >= LyricsIdleResumeSeconds)
            {
                _autoFollow = true;
                UpdateJumpButton();
                if (_currentLyricIndex >= 0)
                {
                    CenterLyric(_currentLyricIndex);
                }
            }
        }

        private int FindCurrentLine(long positionMs)
        {
            // The active line is the last one whose start is at or before the
            // position; before the first timestamp there is no active line.
            // Starts are normally ascending, but a stray out-of-order/nulled
            // line should not stop the scan, so we don't early-break.
            int found = -1;
            for (int i = 0; i < _lyricStartsMs.Count; i++)
            {
                int? start = _lyricStartsMs[i];
                if (start.HasValue && start.Value <= positionMs)
                {
                    found = i;
                }
            }

            return found;
        }

        private void CenterLyric(int index)
        {
            if (index < 0 || index >= _lyricRows.Count)
            {
                return;
            }

            LyricLineRow item = _lyricRows[index];
            ScrollViewer scroll = GetLyricsScrollViewer();
            if (scroll == null || !(LyricsList.ContainerFromItem(item) is ListViewItem container))
            {
                // Container not realized yet (virtualization) — bring it into
                // view; the next tick centers it once the container exists.
                LyricsList.ScrollIntoView(item);
                return;
            }

            GeneralTransform transform = container.TransformToVisual(scroll);
            Point position = transform.TransformPoint(new Point(0, 0));
            double target = scroll.VerticalOffset + position.Y
                - (scroll.ViewportHeight - container.ActualHeight) / 2.0;
            scroll.ChangeView(null, Math.Max(0, target), null, false);
        }

        private ScrollViewer GetLyricsScrollViewer()
        {
            return _lyricsScrollViewer ?? (_lyricsScrollViewer = FindDescendant<ScrollViewer>(LyricsList));
        }

        private static T FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is T typed)
                {
                    return typed;
                }

                T nested = FindDescendant<T>(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void FocusLyrics(int attemptsLeft)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                if (!IsOpen || _paneMode != PaneMode.Lyrics)
                {
                    return;
                }

                UpdateLayout();

                if (_lyricRows.Count == 0)
                {
                    // Message state (loading / no lyrics): keep focus somewhere
                    // reachable so B/close and the tabs still work.
                    if (_lyricsSupported && LyricsTabButton.Focus(FocusState.Keyboard))
                    {
                        return;
                    }

                    PlayPauseButton.Focus(FocusState.Keyboard);
                    return;
                }

                int index = _lyricsSynced && _currentLyricIndex >= 0 ? _currentLyricIndex : 0;
                LyricLineRow item = _lyricRows[index];
                LyricsList.ScrollIntoView(item);
                if (LyricsList.ContainerFromItem(item) is ListViewItem container
                    && container.Focus(FocusState.Keyboard))
                {
                    if (_lyricsSynced && _autoFollow && _currentLyricIndex >= 0)
                    {
                        CenterLyric(_currentLyricIndex);
                    }

                    return;
                }

                if (attemptsLeft > 1)
                {
                    FocusLyrics(attemptsLeft - 1);
                }
                else
                {
                    PlayPauseButton.Focus(FocusState.Keyboard);
                }
            });
        }

        private void LyricsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Read-only navigation (confirmed decision): A does NOT seek — it
            // re-centers on the current line and resumes auto-follow. On
            // unsynced lyrics there is no current line, so A is inert.
            RecenterOnCurrentLine();
        }

        private void LyricsList_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!_lyricsSynced)
            {
                return;
            }

            switch (e.Key)
            {
                case VirtualKey.Up:
                case VirtualKey.Down:
                case VirtualKey.PageUp:
                case VirtualKey.PageDown:
                case VirtualKey.GamepadDPadUp:
                case VirtualKey.GamepadDPadDown:
                case VirtualKey.GamepadLeftThumbstickUp:
                case VirtualKey.GamepadLeftThumbstickDown:
                    // Manual navigation pauses the auto-scroll follow (the
                    // highlight keeps tracking playback, but the view stays
                    // where the user put it). Don't mark handled — the list
                    // still moves focus.
                    _autoFollow = false;
                    _lastLyricInteraction = DateTimeOffset.UtcNow;
                    UpdateJumpButton();
                    break;
            }
        }

        private void JumpToCurrentButton_Click(object sender, RoutedEventArgs e)
        {
            RecenterOnCurrentLine();
        }

        private void RecenterOnCurrentLine()
        {
            if (!_lyricsSynced || _currentLyricIndex < 0)
            {
                return;
            }

            _autoFollow = true;
            UpdateJumpButton();
            CenterLyric(_currentLyricIndex);

            if (LyricsList.ContainerFromItem(_lyricRows[_currentLyricIndex]) is ListViewItem container)
            {
                container.Focus(FocusState.Keyboard);
            }
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

        // --- Row context menu (Slice 14 — the refinement deferred out of
        // Slice 11) -------------------------------------------------------------

        private void QueueList_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            // The gamepad Menu button raises ContextRequested on the focused
            // ListViewItem, but the flyout sits on the item's child grid and
            // routed events bubble up, never down — redirect to the row's own
            // flyout (the console-verified pattern from the Songs rows).
            var container = args.OriginalSource as Windows.UI.Xaml.Controls.Primitives.SelectorItem;
            var root = container?.ContentTemplateRoot as FrameworkElement;
            if (root?.ContextFlyout != null)
            {
                root.ContextFlyout.ShowAt(root);
                args.Handled = true;
            }
        }

        private void PlaySong_Click(object sender, RoutedEventArgs e)
        {
            // Mouse-facing twin of the row's A/Enter jump (the menu opens by
            // right-click too, and every other song menu leads with Play).
            var row = (sender as FrameworkElement)?.Tag as QueueRow;
            if (row != null)
            {
                _playback.JumpTo(row.Song);
            }
        }

        private async void ToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            // Same change-only-after-success contract as the other song-row
            // menus: a failure changes nothing (the menu is closed by then).
            var row = (sender as FrameworkElement)?.Tag as QueueRow;
            var client = AppSession.Client;
            if (row == null || client == null)
            {
                return;
            }

            Song song = row.Song;
            bool wantStarred = !song.IsStarred;
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

                // Repaint every row holding this instance, and keep the left
                // pane's heart honest when the edited row is the current
                // track (it reads the same Song).
                foreach (QueueRow other in _rows)
                {
                    if (ReferenceEquals(other.Song, song))
                    {
                        other.IsStarred = wantStarred;
                    }
                }

                if (ReferenceEquals(song, _playback.Queue.CurrentTrack))
                {
                    _suppressFavoriteEvents = true;
                    FavoriteToggle.IsChecked = wantStarred;
                    _suppressFavoriteEvents = false;
                }
            }
            catch (SubsonicClientException)
            {
                // Nothing changed; the action simply didn't take.
            }
        }

        private void GoToAlbum_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.Tag as QueueRow;
            if (row != null && row.HasAlbumId)
            {
                AlbumRequested?.Invoke(row.Song.AlbumId);
            }
        }

        private void GoToArtist_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.Tag as QueueRow;
            if (row != null && row.HasArtistId)
            {
                ArtistRequested?.Invoke(row.Song.ArtistId);
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
