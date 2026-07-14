using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoxSubMusic.App.Models;
using BoxSubMusic.App.Services;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Artists browse (Slice 06a, requirements §7.1): grid by default, list as
    /// the alternate mode, one always-visible view toggle (§13.5). Connects the
    /// restored client on first use like Home; a connection/auth failure is a
    /// page-level error with Retry (§3). Selecting an artist opens the detail
    /// view.
    /// </summary>
    public sealed partial class ArtistsPage : Page
    {
        // Grid cards are 200 epx; request double for 200%-scale displays.
        private const int CoverArtSize = 400;

        private CancellationTokenSource _cts;

        public ArtistsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
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

            if (AppSession.Client == null)
            {
                ShowConnectError("You are not signed in to a server.");
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
                    ShowConnectError("The server rejected the sign-in: " + ex.Message
                        + " If your password changed, sign out from Account and set up again.");
                }
                return;
            }
            catch (SubsonicClientException ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ShowConnectError(ex.Message);
                }
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                IReadOnlyList<Artist> artists = await AppSession.Client.GetArtistsAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var cards = artists.Select(ToCard).ToList();
                ArtistsGrid.ItemsSource = cards;
                ArtistsList.ItemsSource = cards;
                if (cards.Count == 0)
                {
                    ShowEmpty();
                }
                else
                {
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
                    ShowConnectError(ex.Message);
                }
            }
        }

        private static ArtistCard ToCard(Artist artist)
        {
            var artwork = artist.CoverArtId == null
                ? null
                : AppSession.Client.GetCoverArtUri(artist.CoverArtId, CoverArtSize);
            var label = artist.AlbumCount == 1 ? "1 album" : artist.AlbumCount + " albums";
            return new ArtistCard(artist.Id, artist.Name, label, artwork);
        }

        // --- View states -----------------------------------------------------

        private void ShowLoading()
        {
            LoadingRing.IsActive = true;
            EmptyText.Visibility = Visibility.Collapsed;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            ArtistsGrid.Visibility = Visibility.Collapsed;
            ArtistsList.Visibility = Visibility.Collapsed;
        }

        private void ShowEmpty()
        {
            LoadingRing.IsActive = false;
            EmptyText.Visibility = Visibility.Visible;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            ArtistsGrid.Visibility = Visibility.Collapsed;
            ArtistsList.Visibility = Visibility.Collapsed;
        }

        private void ShowContent()
        {
            LoadingRing.IsActive = false;
            EmptyText.Visibility = Visibility.Collapsed;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            ApplyViewMode();

            // Focus convention: loaded content owns focus (see ContentFocus).
            ContentFocus.MoveInto(this);
        }

        private void ShowConnectError(string message)
        {
            LoadingRing.IsActive = false;
            EmptyText.Visibility = Visibility.Collapsed;
            ArtistsGrid.Visibility = Visibility.Collapsed;
            ArtistsList.Visibility = Visibility.Collapsed;
            ConnectErrorPanel.Visibility = Visibility.Visible;
            ConnectErrorText.Text = message;

            // Controller-first: the error state must own focus, or Right-from-nav
            // lands on nothing.
            ConnectRetryButton.Focus(FocusState.Keyboard);
        }

        // --- View-mode toggle (§13.5) ---------------------------------------

        private void ViewModeToggle_Changed(object sender, RoutedEventArgs e)
        {
            // Only re-show a browse control if content is the visible state.
            if (ConnectErrorPanel.Visibility == Visibility.Visible
                || EmptyText.Visibility == Visibility.Visible
                || LoadingRing.IsActive)
            {
                return;
            }

            ApplyViewMode();
        }

        private void ApplyViewMode()
        {
            bool listMode = ViewModeToggle.IsListMode;
            ArtistsGrid.Visibility = listMode ? Visibility.Collapsed : Visibility.Visible;
            ArtistsList.Visibility = listMode ? Visibility.Visible : Visibility.Collapsed;
            // Gamepad down from the toggle always enters the visible view
            // (see ViewModeToggle.FocusDownTarget).
            ViewModeToggle.FocusDownTarget = listMode ? (DependencyObject)ArtistsList : ArtistsGrid;
        }

        // --- Navigation ------------------------------------------------------

        private void Artist_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ArtistCard card)
            {
                Frame.Navigate(typeof(ArtistDetailPage), card.Id);
            }
        }

        private void ConnectRetryButton_Click(object sender, RoutedEventArgs e)
        {
            var cts = _cts;
            if (cts != null)
            {
                _ = LoadAsync(cts.Token);
            }
        }
    }
}
