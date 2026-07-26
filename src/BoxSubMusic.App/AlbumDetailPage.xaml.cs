using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BoxSubMusic.App.Models;
using BoxSubMusic.App.Services;
using BoxSubMusic.Core.Browsing;
using BoxSubMusic.Core.Playback;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Album detail (Slices 07a + 07b, requirements §9): mirrors the artist
    /// detail layout — large artwork, title, artist, year, favorite heart
    /// persisted through star/unstar, song count + total duration, and the
    /// track list in disc/track order. Since Slice 10: Play/Shuffle replace
    /// the queue with this album's tracks, a track row jumps playback to that
    /// track, and the current track is highlighted. Connects the restored
    /// client on first use; load failure is a retryable error state (§3).
    /// </summary>
    public sealed partial class AlbumDetailPage : Page
    {
        // Detail artwork is 320 epx; request double for 200%-scale displays.
        private const int HeaderArtSize = 640;

        private CancellationTokenSource _cts;
        private string _albumId;
        private AlbumDetail _detail;
        private System.Collections.Generic.List<TrackRow> _rows;

        // Guards the favorite handler while we set IsChecked from loaded state,
        // so restoring the toggle never fires a network call.
        private bool _suppressFavoriteEvents;

        public AlbumDetailPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _albumId = e.Parameter as string;
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

            if (string.IsNullOrEmpty(_albumId))
            {
                ShowError("No album was selected.");
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
                AlbumDetail detail = await AppSession.Client.GetAlbumAsync(_albumId, cancellationToken);
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

        private void Populate(AlbumDetail detail)
        {
            Album album = detail.Album;
            AlbumNameText.Text = album.Name;
            AlbumArtistText.Text = album.Artist ?? string.Empty;
            MetaText.Text = FormatMeta(album, detail);

            ArtworkImage.Source = album.CoverArtId == null
                ? null
                : new BitmapImage
                {
                    UriSource = AppSession.Client.GetCoverArtUri(album.CoverArtId, HeaderArtSize),
                    DecodePixelWidth = 640,
                };

            // Restore the favorite toggle without triggering a star/unstar call.
            _suppressFavoriteEvents = true;
            FavoriteToggle.IsChecked = album.IsStarred;
            _suppressFavoriteEvents = false;
            FavoriteStatusText.Visibility = Visibility.Collapsed;

            var rows = detail.Songs.Select(s => ToRow(s, album.Artist)).ToList();
            _detail = detail;
            _rows = rows;
            TracksList.ItemsSource = rows;
            TracksList.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            NoTracksText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // Nothing to queue on a trackless album (§15: visibly disabled
            // beats a Play that silently does nothing).
            PlayButton.IsEnabled = rows.Count > 0;
            ShuffleButton.IsEnabled = rows.Count > 0;
            PlayNextButton.IsEnabled = rows.Count > 0;
            AddToQueueButton.IsEnabled = rows.Count > 0;
            UpdateNowPlayingHighlight();
        }

        // --- Playback (Slice 07b behavior, live since Slice 10) ---------------

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            StartPlayback(0, shuffle: false);
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            StartPlayback(0, shuffle: true);
        }

        private void TracksList_ItemClick(object sender, ItemClickEventArgs e)
        {
            // §9: selecting a track jumps playback directly to it — the queue
            // becomes this album, positioned at the chosen track.
            var row = e.ClickedItem as TrackRow;
            if (row != null && _rows != null)
            {
                StartPlayback(_rows.IndexOf(row), shuffle: false);
            }
        }

        private void PlayTrack_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.Tag as TrackRow;
            if (row != null && _rows != null)
            {
                StartPlayback(_rows.IndexOf(row), shuffle: false);
            }
        }

        private void PlayNextButton_Click(object sender, RoutedEventArgs e)
        {
            // Container queue edits (2026-07-18): the whole album, inserted
            // after the current track / appended — no replace, no refetch
            // (the songs are already loaded).
            AlbumDetail detail = _detail;
            if (detail != null && detail.Songs.Count > 0)
            {
                PlaybackService.Instance.PlayNext(detail.Songs,
                    new QueueContext(detail.Album.Name, detail.Album.CoverArtId));
            }
        }

        private void AddToQueueButton_Click(object sender, RoutedEventArgs e)
        {
            AlbumDetail detail = _detail;
            if (detail != null && detail.Songs.Count > 0)
            {
                PlaybackService.Instance.AddToQueue(detail.Songs,
                    new QueueContext(detail.Album.Name, detail.Album.CoverArtId));
            }
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
            AlbumDetail detail = _detail;
            if (detail == null || detail.Songs.Count == 0 || startIndex < 0)
            {
                return;
            }

            // §9/§10: replace the queue with this album's tracks only; the
            // now-playing bar reveals itself off the service's change event.
            PlaybackService.Instance.Play(
                detail.Songs,
                startIndex,
                shuffle,
                new QueueContext(detail.Album.Name, detail.Album.CoverArtId));
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

        private static TrackRow ToRow(Song song, string albumArtist)
        {
            // Credit the track artist only when it differs from the album's,
            // so compilations stay honest and regular albums stay clean.
            string artist = song.Artist != null && song.Artist != albumArtist
                ? song.Artist
                : string.Empty;
            return new TrackRow(
                song.Id,
                song.Track?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                song.Title,
                artist,
                DurationText.Track(song.Duration),
                song.ArtistId,
                song.AlbumId,
                song.IsStarred);
        }

        // --- Context menu (console feedback 2026-07-14: same secondary-action
        // menu as the Songs page; "Go to artist" instead of the circular
        // "Go to album") --------------------------------------------------------

        private void TracksList_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            // The gamepad Menu button raises ContextRequested on the focused
            // ListViewItem, but the flyout sits on the item's child grid and
            // routed events bubble up, never down — redirect, exactly as on
            // SongsPage (that redirect is console-verified).
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
            // Context-menu favorite toggle (checkpoint #4 third round); same
            // change-only-after-success contract as the Songs page.
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

                // Keep the loaded album model honest too: the queue and the
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

        private void GoToArtist_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.Tag as TrackRow;
            if (row != null && row.HasArtistId)
            {
                Frame.Navigate(typeof(ArtistDetailPage), row.ArtistId);
            }
        }

        private static string FormatMeta(Album album, AlbumDetail detail)
        {
            var parts = new StringBuilder();
            if (album.Year.HasValue)
            {
                parts.Append(album.Year.Value).Append("  ·  ");
            }

            // Genre (2026-07-27): free from the album the page was opened with,
            // and the one piece of §9 metadata the header was still dropping.
            if (!string.IsNullOrWhiteSpace(album.Genre))
            {
                parts.Append(album.Genre).Append("  ·  ");
            }

            // The songs actually returned are authoritative for the §9 counts;
            // fall back to the album attribute when the list is empty.
            int songCount = detail.Songs.Count > 0 ? detail.Songs.Count : album.SongCount;
            parts.Append(songCount == 1 ? "1 song" : songCount + " songs");

            TimeSpan total = detail.Songs.Count > 0
                ? TimeSpan.FromSeconds(detail.Songs.Sum(s => s.Duration.TotalSeconds))
                : album.Duration;
            parts.Append("  ·  ").Append(DurationText.Total(total));
            return parts.ToString();
        }

        // --- Favorite heart (§9) --------------------------------------------

        private async void FavoriteToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressFavoriteEvents)
            {
                return;
            }

            bool wantStarred = FavoriteToggle.IsChecked == true;
            FavoriteStatusText.Visibility = Visibility.Collapsed;

            var client = AppSession.Client;
            if (client == null || string.IsNullOrEmpty(_albumId))
            {
                return;
            }

            FavoriteToggle.IsEnabled = false;
            try
            {
                if (wantStarred)
                {
                    await client.StarAsync(_albumId);
                }
                else
                {
                    await client.UnstarAsync(_albumId);
                }
            }
            catch (SubsonicClientException)
            {
                // Persist failed: revert the toggle and say so, so the shown
                // state never lies about what the server holds (§9).
                _suppressFavoriteEvents = true;
                FavoriteToggle.IsChecked = !wantStarred;
                _suppressFavoriteEvents = false;
                FavoriteStatusText.Text = "Couldn't update favorite. Try again.";
                FavoriteStatusText.Visibility = Visibility.Visible;
            }
            finally
            {
                FavoriteToggle.IsEnabled = true;
            }
        }

        // Starred visuals live in the shared FavoriteHeart control (filled
        // accent heart + black rim since checkpoint #4); this page only
        // drives IsChecked and persistence.

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
            // The old synchronous Programmatic focus here silently failed on
            // console — the content panel was un-collapsed in the same tick,
            // so the toggle was not yet focusable, and Programmatic draws no
            // focus visual anyway.
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
