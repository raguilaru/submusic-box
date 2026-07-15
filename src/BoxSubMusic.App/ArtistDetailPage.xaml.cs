using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoxSubMusic.App.Models;
using BoxSubMusic.App.Services;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Artist detail (Slice 06a, requirements §8): large artwork, name,
    /// favorite heart persisted through star/unstar, album + song counts, and
    /// the lower album grid. Play/Shuffle are deferred to Slice 06b (queue +
    /// now-playing bar) and rendered disabled. Connects the restored client on
    /// first use; load failure is a retryable error state (§3).
    /// </summary>
    public sealed partial class ArtistDetailPage : Page
    {
        // Detail artwork is 320 epx and album cards 200; request generously for
        // 200%-scale displays.
        private const int HeaderArtSize = 640;
        private const int AlbumArtSize = 400;

        private CancellationTokenSource _cts;
        private string _artistId;

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
            ArtistNameText.Text = detail.Artist.Name;
            CountsText.Text = FormatCounts(detail.Artist.AlbumCount, detail.SongCount);

            ArtworkImage.Source = detail.Artist.CoverArtId == null
                ? null
                : new BitmapImage
                {
                    UriSource = AppSession.Client.GetCoverArtUri(detail.Artist.CoverArtId, HeaderArtSize),
                    DecodePixelWidth = 640,
                };

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
                    a.CoverArtId == null ? null : AppSession.Client.GetCoverArtUri(a.CoverArtId, AlbumArtSize)))
                .ToList();
            AlbumsGrid.ItemsSource = cards;
            AlbumsGrid.Visibility = cards.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            NoAlbumsText.Visibility = cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string FormatCounts(int albumCount, int songCount)
        {
            string albums = albumCount == 1 ? "1 album" : albumCount + " albums";
            string songs = songCount == 1 ? "1 song" : songCount + " songs";
            return albums + "  ·  " + songs;
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

        // The heart glyph is NEVER painted directly: the ToggleButton's own
        // checked visuals carry the starred state (accent background +
        // checked foreground). Two console findings 2026-07-14 killed the
        // hand-painting: Foreground=null made the unstarred heart invisible,
        // and an accent glyph on the accent checked background made the
        // starred heart invisible.

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

        private void Album_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AlbumCard card)
            {
                Frame.Navigate(typeof(AlbumDetailPage), card.Id);
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
