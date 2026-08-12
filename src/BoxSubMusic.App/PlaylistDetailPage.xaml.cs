using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoxSubMusic.App.Models;
using BoxSubMusic.App.Services;
using BoxSubMusic.Core.Browsing;
using BoxSubMusic.Core.Playback;
using BoxSubMusic.Core.Playlists;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Playlist detail (deferred out of Slice 09, requirements §7.4/§10):
    /// mirrors the album detail layout (§9 template) — large client-side
    /// composite artwork, name, owner, song count + total duration. Play and
    /// Shuffle replace the queue with the playlist's songs in stored order;
    /// a song row jumps playback to that song, and the current song is
    /// highlighted. No favorite heart: star/unstar has no playlist form.
    /// Read-only for v1. Connects the restored client on first use; load
    /// failure is a retryable error state (§3).
    /// </summary>
    public sealed partial class PlaylistDetailPage : Page
    {
        // Largest box a constituent cover is drawn into: the single-cover
        // header at 320 epx (the composite cells decode down to 160 from the
        // same shared image). Shares its request tier with the detail headers.
        private const int HeaderArtSize = 320;

        private CancellationTokenSource _cts;
        private string _playlistId;
        private PlaylistDetail _detail;
        private System.Collections.Generic.List<TrackRow> _rows;

        public PlaylistDetailPage()
        {
            InitializeComponent();
        }

        /// <summary>Header artwork slots; filled from the loaded songs (§7.4 composition).</summary>
        public PlaylistCard HeaderArt { get; } = new PlaylistCard(string.Empty, string.Empty, string.Empty);

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _playlistId = e.Parameter as string;
            _cts = new CancellationTokenSource();
            PlaybackService.Instance.Changed += UpdateNowPlayingHighlight;
            _ = LoadAsync(_cts.Token);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            PlaybackService.Instance.Changed -= UpdateNowPlayingHighlight;
            _cts?.Cancel();
            _cts = null;
        }

        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            ShowLoading();

            if (string.IsNullOrEmpty(_playlistId))
            {
                ShowError("No playlist was selected.");
                return;
            }

            if (AppSession.Client == null)
            {
                ShowError("You are not signed in to a server.");
                return;
            }

            try
            {
                await AppSession.EnsureConnectedAsync();
            }
            catch (SubsonicApiException ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ShowError("The server rejected the sign-in: " + ex.Message
                        + " If your password changed, sign out from Account and set up again.");
                }
                return;
            }
            catch (SubsonicClientException ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ShowError(ex.Message);
                }
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                PlaylistDetail detail = await AppSession.Client.GetPlaylistAsync(_playlistId, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    Populate(detail);
                    ShowContent();
                }
            }
            catch (OperationCanceledException)
            {
                // Page was left mid-load; nothing to show.
            }
            catch (SubsonicClientException ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ShowError(ex.Message);
                }
            }
        }

        private void Populate(PlaylistDetail detail)
        {
            Playlist playlist = detail.Playlist;
            PlaylistNameText.Text = playlist.Name;
            OwnerText.Text = string.IsNullOrEmpty(playlist.Owner) ? string.Empty : "By " + playlist.Owner;
            OwnerText.Visibility = string.IsNullOrEmpty(playlist.Owner) ? Visibility.Collapsed : Visibility.Visible;
            MetaText.Text = FormatMeta(playlist, detail);

            // §7.4: header artwork is the same client-side composition as the
            // browse tile — the loaded songs carry the album references.
            HeaderArt.SetCovers(PlaylistArtwork.SelectCoverArtIds(detail.Songs)
                .Select(id => CoverArt.UriFor(id, HeaderArtSize))
                .ToList());

            var rows = detail.Songs.Select(ToRow).ToList();
            _detail = detail;
            _rows = rows;
            SongsList.ItemsSource = rows;
            SongsList.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            NoSongsText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // Nothing to queue on an empty playlist (§15: visibly disabled
            // beats a Play that silently does nothing).
            PlayButton.IsEnabled = rows.Count > 0;
            ShuffleButton.IsEnabled = rows.Count > 0;
            PlayNextButton.IsEnabled = rows.Count > 0;
            AddToQueueButton.IsEnabled = rows.Count > 0;
            UpdateNowPlayingHighlight();
        }

        // --- Playback ---------------------------------------------------------

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            StartPlayback(0, shuffle: false);
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            StartPlayback(0, shuffle: true);
        }

        private void SongsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Selecting a row jumps playback directly to it — the queue
            // becomes this playlist, positioned at the chosen song.
            var row = e.ClickedItem as TrackRow;
            if (row != null && _rows != null)
            {
                StartPlayback(_rows.IndexOf(row), shuffle: false);
            }
        }

        private void PlaySong_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.Tag as TrackRow;
            if (row != null && _rows != null)
            {
                StartPlayback(_rows.IndexOf(row), shuffle: false);
            }
        }

        private void PlayNextButton_Click(object sender, RoutedEventArgs e)
        {
            // Container queue edits (2026-07-18): the whole playlist, inserted
            // after the current track / appended — no replace, no refetch
            // (the songs are already loaded).
            PlaylistDetail detail = _detail;
            if (detail != null && detail.Songs.Count > 0)
            {
                PlaybackService.Instance.PlayNext(detail.Songs, PlaylistContext(detail));
            }
        }

        private void AddToQueueButton_Click(object sender, RoutedEventArgs e)
        {
            PlaylistDetail detail = _detail;
            if (detail != null && detail.Songs.Count > 0)
            {
                PlaybackService.Instance.AddToQueue(detail.Songs, PlaylistContext(detail));
            }
        }

        private static QueueContext PlaylistContext(PlaylistDetail detail)
        {
            // Same artwork rule as Play: the first constituent album cover
            // (single-image cut of the §7.4 composition).
            return new QueueContext(
                detail.Playlist.Name,
                PlaylistArtwork.SelectCoverArtIds(detail.Songs).FirstOrDefault());
        }

        private void PlayNext_Click(object sender, RoutedEventArgs e)
        {
            // Queue edits (2026-07-17): explicit insert/append, distinct from
            // the replace-only Play actions (§10).
            Song song = SongForRow(sender);
            if (song != null)
            {
                PlaybackService.Instance.PlayNext(song);
            }
        }

        private void AddToQueue_Click(object sender, RoutedEventArgs e)
        {
            Song song = SongForRow(sender);
            if (song != null)
            {
                PlaybackService.Instance.AddToQueue(song);
            }
        }

        private Song SongForRow(object sender)
        {
            var row = (sender as FrameworkElement)?.Tag as TrackRow;
            var detail = _detail;
            if (row == null || _rows == null || detail == null)
            {
                return null;
            }

            int index = _rows.IndexOf(row);
            return index >= 0 && index < detail.Songs.Count ? detail.Songs[index] : null;
        }

        private void StartPlayback(int startIndex, bool shuffle)
        {
            PlaylistDetail detail = _detail;
            if (detail == null || detail.Songs.Count == 0 || startIndex < 0)
            {
                return;
            }

            // §10: replace the queue with this playlist's songs only; the
            // now-playing bar reveals itself off the service's change event.
            // The resume-card artwork is the first constituent album cover —
            // the single-image cut of the §7.4 client-side composition.
            PlaybackService.Instance.Play(
                detail.Songs,
                startIndex,
                shuffle,
                new QueueContext(
                    detail.Playlist.Name,
                    PlaylistArtwork.SelectCoverArtIds(detail.Songs).FirstOrDefault()));
        }

        private void UpdateNowPlayingHighlight()
        {
            var rows = _rows;
            if (rows == null)
            {
                return;
            }

            string currentId = PlaybackService.Instance.Queue.CurrentTrack?.Id;
            foreach (TrackRow row in rows)
            {
                row.IsCurrent = currentId != null && row.Id == currentId;
            }
        }

        private static TrackRow ToRow(Song song, int index)
        {
            // The number column shows the stored playlist position; the artist
            // is always shown — a playlist has no album artist to elide against.
            return new TrackRow(
                song.Id,
                (index + 1).ToString(CultureInfo.InvariantCulture),
                song.Title,
                song.Artist ?? string.Empty,
                DurationText.Track(song.Duration),
                song.ArtistId,
                song.AlbumId,
                song.IsStarred);
        }

        // --- Context menu (same console-verified redirect pattern as the
        // Songs/album-track rows; both "Go to album" and "Go to artist" are
        // non-circular here, so both appear) -----------------------------------

        private void SongsList_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            // The gamepad Menu button raises ContextRequested on the focused
            // ListViewItem, but the flyout sits on the item's child grid and
            // routed events bubble up, never down — redirect to the row's own
            // flyout.
            var container = args.OriginalSource as SelectorItem;
            var root = container?.ContentTemplateRoot as FrameworkElement;
            if (root?.ContextFlyout != null)
            {
                root.ContextFlyout.ShowAt(root);
                args.Handled = true;
            }
        }

        private async void ToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            // Same change-only-after-success contract as the other song lists.
            var row = (sender as FrameworkElement)?.Tag as TrackRow;
            var client = AppSession.Client;
            if (row == null || client == null)
            {
                return;
            }

            bool wantStarred = !row.IsStarred;
            try
            {
                if (wantStarred)
                {
                    await client.StarAsync(row.Id);
                }
                else
                {
                    await client.UnstarAsync(row.Id);
                }

                row.IsStarred = wantStarred;

                // Keep the loaded playlist model honest too: the queue and the
                // now-playing bar read starred state from these instances.
                var detail = _detail;
                if (detail != null)
                {
                    foreach (Song song in detail.Songs)
                    {
                        if (song.Id == row.Id)
                        {
                            song.IsStarred = wantStarred;
                        }
                    }
                }
            }
            catch (SubsonicClientException)
            {
                // Nothing changed; the action simply didn't take.
            }
        }

        private void GoToAlbum_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.Tag as TrackRow;
            if (row != null && row.HasAlbumId)
            {
                Frame.Navigate(typeof(AlbumDetailPage), row.AlbumId);
            }
        }

        private void GoToArtist_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.Tag as TrackRow;
            if (row != null && row.HasArtistId)
            {
                Frame.Navigate(typeof(ArtistDetailPage), row.ArtistId);
            }
        }

        private static string FormatMeta(Playlist playlist, PlaylistDetail detail)
        {
            // The songs actually returned are authoritative for the counts;
            // fall back to the summary attributes when the list is empty.
            int songCount = detail.Songs.Count > 0 ? detail.Songs.Count : playlist.SongCount;
            TimeSpan total = detail.Songs.Count > 0
                ? TimeSpan.FromSeconds(detail.Songs.Sum(s => s.Duration.TotalSeconds))
                : playlist.Duration;
            string songs = songCount == 1 ? "1 song" : songCount + " songs";
            return songs + "  ·  " + DurationText.Total(total);
        }

        // --- View states -----------------------------------------------------

        private void ShowLoading()
        {
            LoadingRing.IsActive = true;
            ErrorPanel.Visibility = Visibility.Collapsed;
            ContentScroller.Visibility = Visibility.Collapsed;
        }

        private void ShowContent()
        {
            LoadingRing.IsActive = false;
            ErrorPanel.Visibility = Visibility.Collapsed;
            ContentScroller.Visibility = Visibility.Visible;

            // Focus convention: loaded content owns focus (see ContentFocus).
            ContentFocus.MoveInto(this);
        }

        private void ShowError(string message)
        {
            LoadingRing.IsActive = false;
            ContentScroller.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            ErrorText.Text = message;
            RetryButton.Focus(FocusState.Keyboard);
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            var cts = _cts;
            if (cts != null)
            {
                _ = LoadAsync(cts.Token);
            }
        }
    }
}
