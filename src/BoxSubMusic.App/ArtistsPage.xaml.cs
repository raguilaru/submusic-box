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
            // getArtists carries the album count and the starred flag only; the
            // song count and total play time would cost one getArtist per row,
            // so they live on the detail page instead (decision 2026-07-27).
            return new ArtistCard(
                artist.Id, artist.Name, label, artwork, artist.AlbumCount, artist.IsStarred);
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
            // Into the active view, not the page: the page's first focusable
            // is the view-mode toggle, and landing there put the first card
            // an extra press away (UI feedback 2026-07-19 — focus the first
            // element BELOW the toggle; Up still reaches it via XYFocusUp).
            ContentFocus.MoveInto(ViewModeToggle.IsListMode ? (UIElement)ArtistsList : ArtistsGrid);
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

        // --- Card context menu (container queue edits, 2026-07-18) ------------

        private void Artists_ContextRequested(UIElement sender, Windows.UI.Xaml.Input.ContextRequestedEventArgs args)
        {
            // The gamepad Menu button raises ContextRequested on the focused
            // item container, but the flyout sits on the template root —
            // redirect (the console-verified pattern from the song lists).
            var container = args.OriginalSource as Windows.UI.Xaml.Controls.Primitives.SelectorItem;
            var root = container?.ContentTemplateRoot as FrameworkElement;
            if (root?.ContextFlyout != null)
            {
                root.ContextFlyout.ShowAt(root);
                args.Handled = true;
            }
        }

        private void ArtistPlay_Click(object sender, RoutedEventArgs e)
        {
            var card = (sender as FrameworkElement)?.Tag as ArtistCard;
            if (card != null)
            {
                _ = QueueActions.PlayArtistAsync(card.Id);
            }
        }

        private void ArtistPlayNext_Click(object sender, RoutedEventArgs e)
        {
            var card = (sender as FrameworkElement)?.Tag as ArtistCard;
            if (card != null)
            {
                _ = QueueActions.AddArtistAsync(card.Id, insertNext: true);
            }
        }

        private void ArtistAddToQueue_Click(object sender, RoutedEventArgs e)
        {
            var card = (sender as FrameworkElement)?.Tag as ArtistCard;
            if (card != null)
            {
                _ = QueueActions.AddArtistAsync(card.Id, insertNext: false);
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
