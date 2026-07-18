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
    /// Albums browse (Slice 07a, requirements §7.2): grid by default, list as
    /// the alternate mode, the shared view toggle (§13.5). Albums come from
    /// getAlbumList2 alphabeticalByName in incrementally loaded pages
    /// (decision 2026-07-13). Connects the restored client on first use like
    /// the other browse pages; failures are a page-level error with Retry
    /// (§3). Selecting an album opens the detail view.
    /// </summary>
    public sealed partial class AlbumsPage : Page
    {
        // Grid cards are 200 epx; request double for 200%-scale displays.
        private const int CoverArtSize = 400;

        private CancellationTokenSource _cts;
        private IncrementalSource<AlbumCard> _albums;

        public AlbumsPage()
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

            // A fresh source per (re)load: Retry after any failure starts
            // over from page one, so the collection can never hold a gap.
            var source = new IncrementalSource<AlbumCard>(FetchPageAsync);
            source.LoadFailed += Albums_LoadFailed;
            _albums = source;

            try
            {
                int loaded = await source.LoadFirstPageAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                AlbumsGrid.ItemsSource = source;
                AlbumsList.ItemsSource = source;
                if (loaded == 0)
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

        private static async Task<IReadOnlyList<AlbumCard>> FetchPageAsync(
            int offset, int size, CancellationToken cancellationToken)
        {
            var client = AppSession.Client;
            if (client == null)
            {
                throw new SubsonicConnectionException("You are not signed in to a server.");
            }

            IReadOnlyList<Album> page = await client.GetAlbumList2Async(
                AlbumListType.AlphabeticalByName, size, offset, cancellationToken);
            return page.Select(ToCard).ToList();
        }

        private static AlbumCard ToCard(Album album)
        {
            var artwork = album.CoverArtId == null
                ? null
                : AppSession.Client.GetCoverArtUri(album.CoverArtId, CoverArtSize);
            return new AlbumCard(album.Id, album.Name, album.Artist ?? string.Empty, artwork);
        }

        private void Albums_LoadFailed(object sender, Exception ex)
        {
            // A later page failed mid-scroll. Surface the same page-level
            // error with Retry (which reloads from the top) rather than
            // failing silently (§3). Losing the scroll position is a known
            // v1 trade-off.
            if (!ReferenceEquals(sender, _albums))
            {
                return;
            }

            ShowConnectError("Couldn't load more albums. " + ex.Message);
        }

        // --- View states -----------------------------------------------------

        private void ShowLoading()
        {
            LoadingRing.IsActive = true;
            EmptyText.Visibility = Visibility.Collapsed;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            AlbumsGrid.Visibility = Visibility.Collapsed;
            AlbumsList.Visibility = Visibility.Collapsed;
        }

        private void ShowEmpty()
        {
            LoadingRing.IsActive = false;
            EmptyText.Visibility = Visibility.Visible;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            AlbumsGrid.Visibility = Visibility.Collapsed;
            AlbumsList.Visibility = Visibility.Collapsed;
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
            ContentFocus.MoveInto(ViewModeToggle.IsListMode ? (UIElement)AlbumsList : AlbumsGrid);
        }

        private void ShowConnectError(string message)
        {
            LoadingRing.IsActive = false;
            EmptyText.Visibility = Visibility.Collapsed;
            AlbumsGrid.Visibility = Visibility.Collapsed;
            AlbumsList.Visibility = Visibility.Collapsed;
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
            AlbumsGrid.Visibility = listMode ? Visibility.Collapsed : Visibility.Visible;
            AlbumsList.Visibility = listMode ? Visibility.Visible : Visibility.Collapsed;
            // Gamepad down from the toggle always enters the visible view
            // (see ViewModeToggle.FocusDownTarget).
            ViewModeToggle.FocusDownTarget = listMode ? (DependencyObject)AlbumsList : AlbumsGrid;
        }

        // --- Navigation ------------------------------------------------------

        private void Album_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AlbumCard card)
            {
                Frame.Navigate(typeof(AlbumDetailPage), card.Id);
            }
        }

        // --- Card context menu (container queue edits, 2026-07-18) ------------

        private void Albums_ContextRequested(UIElement sender, Windows.UI.Xaml.Input.ContextRequestedEventArgs args)
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
