using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BoxSubMusic.App.Controls;
using BoxSubMusic.App.Models;
using BoxSubMusic.App.Services;
using BoxSubMusic.Core.Browsing;
using BoxSubMusic.Core.Playback;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Artist detail (Slices 06a + 06b, requirements §8): large artwork, name,
    /// favorite heart persisted through star/unstar, album + song counts, and
    /// the lower album grid. Since Slice 10: Play queues every song
    /// oldest-album-first, Shuffle the same pool randomized — both replace the
    /// queue. Connects the restored client on first use; load failure is a
    /// retryable error state (§3).
    /// </summary>
    public sealed partial class ArtistDetailPage : Page
    {
        // Both are display boxes (1:1 at 1080p, layout scaling off). The
        // album grid's 200 shares its request tier with the Albums browse
        // page, so an album seen in both places is one cached image.
        private const int HeaderArtSize = 320;
        private const int AlbumArtSize = 200;

        private CancellationTokenSource _cts;
        private string _artistId;
        private ArtistDetail _detail;

        // Guards the favorite handler while we set IsChecked from loaded state,
        // so restoring the toggle never fires a network call.
        private bool _suppressFavoriteEvents;

        public ArtistDetailPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _artistId = e.Parameter as string;
            _cts = new CancellationTokenSource();
            _ = LoadAsync(_cts.Token);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _cts?.Cancel();
            _cts = null;
        }

        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            ShowLoading();

            if (string.IsNullOrEmpty(_artistId))
            {
                ShowError("No artist was selected.");
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
                ArtistDetail detail = await AppSession.Client.GetArtistAsync(_artistId, cancellationToken);
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

        private void Populate(ArtistDetail detail)
        {
            _detail = detail;
            PlaybackStatusText.Visibility = Visibility.Collapsed;

            // Nothing to queue for an album-less artist (§15: visibly disabled
            // beats a Play that silently does nothing).
            SetPlaybackButtonsEnabled(detail.Albums.Count > 0);

            ArtistNameText.Text = detail.Artist.Name;
            CountsText.Text = FormatCounts(detail);

            CachedCover.SetDecodeWidth(ArtworkImage, HeaderArtSize);
            CachedCover.SetSourceUri(ArtworkImage, CoverArt.UriFor(detail.Artist.CoverArtId, HeaderArtSize));

            // Restore the favorite toggle without triggering a star/unstar call.
            _suppressFavoriteEvents = true;
            FavoriteToggle.IsChecked = detail.Artist.IsStarred;
            _suppressFavoriteEvents = false;
            FavoriteStatusText.Visibility = Visibility.Collapsed;

            var cards = detail.Albums
                .Select(a => new AlbumCard(
                    a.Id,
                    a.Name,
                    string.IsNullOrEmpty(a.Artist) ? detail.Artist.Name : a.Artist,
                    CoverArt.UriFor(a.CoverArtId, AlbumArtSize)))
                .ToList();
            AlbumsGrid.ItemsSource = cards;
            // Deliberately NOT collapsed when empty any more: the grid now
            // hosts the page header, so hiding it would hide the whole page.
            // NoAlbumsText (in the header) is the empty state.
            NoAlbumsText.Visibility = cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// The §8 counts line, extended 2026-07-27 with the artist's total play
        /// time and the span of years their albums cover. Both are summed from
        /// the albums getArtist already returned, so the richer line costs no
        /// extra request; each part is omitted when the server sent nothing to
        /// compute it from, rather than shown as a zero (§3).
        /// </summary>
        private static string FormatCounts(ArtistDetail detail)
        {
            var parts = new StringBuilder();
            int albumCount = detail.Artist.AlbumCount;
            parts.Append(albumCount == 1 ? "1 album" : albumCount + " albums");
            parts.Append("  ·  ").Append(
                detail.SongCount == 1 ? "1 song" : detail.SongCount + " songs");

            TimeSpan total = TimeSpan.FromSeconds(
                detail.Albums.Sum(a => a.Duration.TotalSeconds));
            if (total > TimeSpan.Zero)
            {
                parts.Append("  ·  ").Append(DurationText.Total(total));
            }

            var years = detail.Albums
                .Where(a => a.Year.HasValue)
                .Select(a => a.Year.Value)
                .ToList();
            if (years.Count > 0)
            {
                int first = years.Min();
                int last = years.Max();
                parts.Append("  ·  ").Append(
                    first == last ? first.ToString() : first + "–" + last);
            }

            return parts.ToString();
        }

        // --- Playback (Slice 06b behavior, live since Slice 10) ---------------

        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            await RunPoolActionAsync(songs => PlaybackService.Instance.Play(
                songs, 0, shuffle: false, context: PoolContext()));
        }

        private async void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            await RunPoolActionAsync(songs => PlaybackService.Instance.Play(
                songs, 0, shuffle: true, context: PoolContext()));
        }

        private async void PlayNextButton_Click(object sender, RoutedEventArgs e)
        {
            // Queue edits (2026-07-18): insert/append the whole pool without
            // replacing the queue (distinct from the §8/§10 Play actions).
            await RunPoolActionAsync(songs =>
                PlaybackService.Instance.PlayNext(songs, PoolContext()));
        }

        private async void AddToQueueButton_Click(object sender, RoutedEventArgs e)
        {
            await RunPoolActionAsync(songs =>
                PlaybackService.Instance.AddToQueue(songs, PoolContext()));
        }

        private QueueContext PoolContext()
        {
            return new QueueContext(_detail.Artist.Name, _detail.Artist.CoverArtId);
        }

        /// <summary>
        /// §8: the pool is ALL of the artist's songs oldest-album-first (year
        /// ascending, unknown years last, then name), built by
        /// <see cref="QueueActions.LoadArtistPoolAsync"/> — getArtist carries
        /// albums without songs, so it fans out one getAlbum per album in
        /// parallel. All four playback buttons share this fetch: they disable
        /// while it runs and PlaybackStatusText reports a failure.
        /// </summary>
        private async Task RunPoolActionAsync(Action<List<Song>> action)
        {
            ArtistDetail detail = _detail;
            var client = AppSession.Client;
            var cancellationToken = _cts?.Token ?? CancellationToken.None;
            if (detail == null || client == null || detail.Albums.Count == 0)
            {
                return;
            }

            SetPlaybackButtonsEnabled(false);
            PlaybackStatusText.Visibility = Visibility.Collapsed;
            try
            {
                List<Song> songs = await QueueActions.LoadArtistPoolAsync(
                    client, detail, cancellationToken);
                if (songs.Count == 0)
                {
                    PlaybackStatusText.Text = "This artist has no playable songs.";
                    PlaybackStatusText.Visibility = Visibility.Visible;
                    return;
                }

                action(songs);
            }
            catch (OperationCanceledException)
            {
                // Page was left mid-fetch; nothing to start.
            }
            catch (SubsonicClientException ex)
            {
                PlaybackStatusText.Text = "Couldn't start playback. " + ex.Message;
                PlaybackStatusText.Visibility = Visibility.Visible;
            }
            finally
            {
                SetPlaybackButtonsEnabled(_detail != null && _detail.Albums.Count > 0);
            }
        }

        private void SetPlaybackButtonsEnabled(bool enabled)
        {
            PlayButton.IsEnabled = enabled;
            ShuffleButton.IsEnabled = enabled;
            PlayNextButton.IsEnabled = enabled;
            AddToQueueButton.IsEnabled = enabled;
        }

        // --- Favorite heart (§8) --------------------------------------------

        private async void FavoriteToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressFavoriteEvents)
            {
                return;
            }

            bool wantStarred = FavoriteToggle.IsChecked == true;
            FavoriteStatusText.Visibility = Visibility.Collapsed;

            var client = AppSession.Client;
            if (client == null || string.IsNullOrEmpty(_artistId))
            {
                return;
            }

            FavoriteToggle.IsEnabled = false;
            try
            {
                if (wantStarred)
                {
                    await client.StarAsync(_artistId);
                }
                else
                {
                    await client.UnstarAsync(_artistId);
                }
            }
            catch (SubsonicClientException)
            {
                // Persist failed: revert the toggle and say so, so the shown
                // state never lies about what the server holds (§8).
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
            AlbumsGrid.Visibility = Visibility.Collapsed;
        }

        private void ShowContent()
        {
            LoadingRing.IsActive = false;
            ErrorPanel.Visibility = Visibility.Collapsed;
            AlbumsGrid.Visibility = Visibility.Visible;

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
            AlbumsGrid.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            ErrorText.Text = message;
            RetryButton.Focus(FocusState.Keyboard);
        }

        private void Album_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AlbumCard card)
            {
                Frame.Navigate(typeof(AlbumDetailPage), card.Id);
            }
        }

        // --- Album-card context menu (container queue edits, 2026-07-18) ------

        private void Albums_ContextRequested(UIElement sender, Windows.UI.Xaml.Input.ContextRequestedEventArgs args)
        {
            // The gamepad Menu button raises ContextRequested on the focused
            // GridViewItem, but the flyout sits on the item's template root —
            // redirect (the console-verified pattern from the song lists).
            var container = args.OriginalSource as Windows.UI.Xaml.Controls.Primitives.SelectorItem;
            var root = container?.ContentTemplateRoot as FrameworkElement;
            if (root?.ContextFlyout != null)
            {
                root.ContextFlyout.ShowAt(root);
                args.Handled = true;
            }
        }

        private void AlbumPlay_Click(object sender, RoutedEventArgs e)
        {
            var card = (sender as FrameworkElement)?.Tag as AlbumCard;
            if (card != null)
            {
                _ = QueueActions.PlayAlbumAsync(card.Id);
            }
        }

        private void AlbumPlayNext_Click(object sender, RoutedEventArgs e)
        {
            var card = (sender as FrameworkElement)?.Tag as AlbumCard;
            if (card != null)
            {
                _ = QueueActions.AddAlbumAsync(card.Id, insertNext: true);
            }
        }

        private void AlbumAddToQueue_Click(object sender, RoutedEventArgs e)
        {
            var card = (sender as FrameworkElement)?.Tag as AlbumCard;
            if (card != null)
            {
                _ = QueueActions.AddAlbumAsync(card.Id, insertNext: false);
            }
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
