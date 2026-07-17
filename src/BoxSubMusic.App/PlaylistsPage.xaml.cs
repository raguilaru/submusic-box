using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoxSubMusic.App.Models;
using BoxSubMusic.App.Services;
using BoxSubMusic.Core.Playlists;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Playlists browse (Slice 09, requirements §7.4): grid by default, list as
    /// the alternate mode, the shared view toggle (§13.5). getPlaylists returns
    /// only summaries, so each tile's composite artwork (up to four distinct
    /// album covers, single cover when one album) is composed client-side and
    /// fetched lazily per card after the grid is shown — the grid stays
    /// responsive even with many playlists. Cards open the playlist detail
    /// view; editing stays out of v1 (decision 2026-07-13). A connection/auth
    /// failure is a page-level error with Retry (§3).
    /// </summary>
    public sealed partial class PlaylistsPage : Page
    {
        // Cover art hint; XAML DecodePixelWidth downsizes per layout.
        private const int CoverArtSize = 400;

        private CancellationTokenSource _cts;

        public PlaylistsPage()
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
                IReadOnlyList<Playlist> playlists = await AppSession.Client.GetPlaylistsAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var cards = playlists.Select(ToCard).ToList();
                PlaylistsGrid.ItemsSource = cards;
                PlaylistsList.ItemsSource = cards;
                if (cards.Count == 0)
                {
                    ShowEmpty();
                    return;
                }

                ShowContent();

                // Compose each tile's artwork lazily and in parallel; the grid
                // is already interactive, and each card fills in when ready.
                foreach (var card in cards)
                {
                    _ = LoadCompositeAsync(card, cancellationToken);
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

        private static PlaylistCard ToCard(Playlist playlist)
        {
            var subtitle = playlist.SongCount == 1 ? "1 song" : playlist.SongCount + " songs";
            return new PlaylistCard(playlist.Id, playlist.Name, subtitle);
        }

        /// <summary>
        /// Fetches one playlist's tracks and composes its tile artwork (§7.4). A
        /// per-tile failure degrades to the placeholder rather than failing the
        /// whole page — the artwork is decorative, and the grid already loaded.
        /// </summary>
        private async Task LoadCompositeAsync(PlaylistCard card, CancellationToken cancellationToken)
        {
            var client = AppSession.Client;
            if (client == null)
            {
                return;
            }

            try
            {
                PlaylistDetail detail = await client.GetPlaylistAsync(card.Id, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var covers = PlaylistArtwork.SelectCoverArtIds(detail.Songs)
                    .Select(id => client.GetCoverArtUri(id, CoverArtSize))
                    .ToList();
                card.SetCovers(covers);
            }
            catch (OperationCanceledException)
            {
                // Navigated away; leave the placeholder.
            }
            catch (SubsonicClientException)
            {
                // Artwork is best-effort; the placeholder stands in.
            }
        }

        // --- View states -----------------------------------------------------

        private void ShowLoading()
        {
            LoadingRing.IsActive = true;
            EmptyText.Visibility = Visibility.Collapsed;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            PlaylistsGrid.Visibility = Visibility.Collapsed;
            PlaylistsList.Visibility = Visibility.Collapsed;
        }

        private void ShowEmpty()
        {
            LoadingRing.IsActive = false;
            EmptyText.Visibility = Visibility.Visible;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            PlaylistsGrid.Visibility = Visibility.Collapsed;
            PlaylistsList.Visibility = Visibility.Collapsed;
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
            PlaylistsGrid.Visibility = Visibility.Collapsed;
            PlaylistsList.Visibility = Visibility.Collapsed;
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
            PlaylistsGrid.Visibility = listMode ? Visibility.Collapsed : Visibility.Visible;
            PlaylistsList.Visibility = listMode ? Visibility.Visible : Visibility.Collapsed;
            // Gamepad down from the toggle always enters the visible view
            // (see ViewModeToggle.FocusDownTarget).
            ViewModeToggle.FocusDownTarget = listMode ? (DependencyObject)PlaylistsList : PlaylistsGrid;
        }

        // --- Navigation ------------------------------------------------------

        private void Playlists_ItemClick(object sender, ItemClickEventArgs e)
        {
            var card = e.ClickedItem as PlaylistCard;
            if (card != null)
            {
                Frame.Navigate(typeof(PlaylistDetailPage), card.Id);
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
