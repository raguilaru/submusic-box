using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoxSubMusic.App.Models;
using BoxSubMusic.App.Services;
using BoxSubMusic.Core.Browsing;
using BoxSubMusic.Core.Playback;
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
    /// Songs browse (Slice 08, requirements §7.3): list-only with title,
    /// artist, album, length, format and bitrate. Songs come from search3
    /// with an empty query — the OpenSubsonic full-library listing (decision
    /// 2026-07-13); servers that are not OpenSubsonic get an explicit
    /// unsupported state (§3), never a wrong empty list. A/Enter on a row
    /// starts immediate playback (decision 2026-07-13, live since Slice 10);
    /// the Menu-button context menu carries the secondary actions.
    ///
    /// Sorting and whole-library playback (2026-07-26) changed how the page
    /// loads. The Subsonic API cannot sort songs — search3 has no sort
    /// parameter — so ordering is client-side, which only tells the truth
    /// over the WHOLE library. The page therefore pages the entire library
    /// into memory (500/request, the API maximum) instead of the previous
    /// scroll-driven incremental loading: the first page is shown as soon as
    /// it arrives and the rest streams in behind it, with Sort/Play All/
    /// Shuffle All disabled until every page is in. The list virtualizes, so
    /// the cost is the held rows, not the rendering.
    /// </summary>
    public sealed partial class SongsPage : Page
    {
        // The API caps one search3 request at 500; the whole-library load
        // wants the fewest round trips it can get.
        private const int LoadPageSize = 500;

        private CancellationTokenSource _cts;

        /// <summary>Every row, in the server's own order — the sort input.</summary>
        private List<SongRow> _allRows;

        /// <summary>The rows as currently shown: the queue order for playback.</summary>
        private ObservableCollection<SongRow> _view;

        private SongSort _sort = SongSort.DefaultOrder;
        private bool _fullyLoaded;

        public SongsPage()
        {
            InitializeComponent();

            // The whole-library load is far too expensive to repeat on every
            // B-back from a song's album page. This makes Songs the second
            // navigation-cached page after Search, and inherits Search's
            // documented trade-off: starred markers on kept rows can lag
            // until the page is reloaded.
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _cts = new CancellationTokenSource();

            // Cached instance coming back from a detail page: the library and
            // the chosen sort are still here, so there is nothing to reload.
            if (_fullyLoaded)
            {
                ContentFocus.MoveInto(SongsList);
                return;
            }

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

            // Only OpenSubsonic promises that an empty search3 query lists
            // the whole library; on legacy servers an empty result would be
            // indistinguishable from an empty library, so degrade explicitly
            // instead (§3).
            var capabilities = AppSession.Client.Capabilities;
            if (capabilities == null || !capabilities.IsOpenSubsonic)
            {
                ShowUnsupported(
                    "This server does not report OpenSubsonic support, which the Songs view "
                    + "needs to list the whole library. Browse by artist or album instead.");
                return;
            }

            // Fresh state per (re)load: Retry after any failure starts over
            // from page one, so the library can never hold a gap — and a gap
            // would be invisible once sorted.
            _fullyLoaded = false;
            _sort = SongSort.DefaultOrder;
            _allRows = new List<SongRow>();
            _view = new ObservableCollection<SongRow>();
            SongsList.ItemsSource = _view;
            ApplySortSelection(SongSort.DefaultOrder);
            SetLibraryActionsEnabled(false);

            try
            {
                // Page one decides the view state; the rest streams in behind
                // it so the user is not staring at a ring while a large
                // library downloads.
                int first = await LoadPageAsync(0, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (first == 0)
                {
                    ShowEmpty();
                    return;
                }

                ShowContent();

                // A short page means the server has nothing left — the same
                // end-of-list signal IncrementalSource uses.
                int lastPage = first;
                while (lastPage == LoadPageSize)
                {
                    lastPage = await LoadPageAsync(_allRows.Count, cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                }

                _fullyLoaded = true;
                SetLibraryActionsEnabled(true);
            }
            catch (OperationCanceledException)
            {
                // Page was left mid-load; nothing to show.
            }
            catch (SubsonicClientException ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    // A later page failing is fatal here in a way it was not
                    // under scroll-driven paging: sorting and Play All promise
                    // the whole library, and a silently truncated one would
                    // break that promise invisibly. Surface the page-level
                    // error with Retry, which reloads from the top (§3).
                    ShowConnectError(_allRows.Count == 0
                        ? ex.Message
                        : "Couldn't load the rest of the library. " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Fetches one page into <see cref="_allRows"/> and, while the default
        /// order is showing, straight into the bound view. Returns the number
        /// of rows added.
        /// </summary>
        private async Task<int> LoadPageAsync(int offset, CancellationToken cancellationToken)
        {
            var client = AppSession.Client;
            if (client == null)
            {
                throw new SubsonicConnectionException("You are not signed in to a server.");
            }

            IReadOnlyList<Song> page = await client.GetSongsAsync(
                LoadPageSize, offset, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return 0;
            }

            foreach (Song song in page)
            {
                var row = ToRow(song);
                _allRows.Add(row);

                // Safe to append blind: the sort controls stay disabled until
                // the load finishes, so the view is still in server order.
                _view.Add(row);
            }

            return page.Count;
        }

        private static SongRow ToRow(Song song)
        {
            return new SongRow(
                song,
                song.Id,
                song.AlbumId,
                song.Title,
                song.Artist ?? string.Empty,
                song.Album ?? string.Empty,
                FormatDuration(song.Duration),
                song.Format?.ToUpperInvariant() ?? string.Empty,
                song.BitRate.HasValue
                    ? song.BitRate.Value.ToString(CultureInfo.InvariantCulture) + " kbps"
                    : string.Empty);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            return duration.TotalHours >= 1
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:mm\\:ss}", (int)duration.TotalHours, duration)
                : duration.ToString("m\\:ss", CultureInfo.InvariantCulture);
        }

        // --- Sorting (2026-07-26, §7.3) --------------------------------------

        private void SortItem_Click(object sender, RoutedEventArgs e)
        {
            // Identify the sort by the item itself rather than parsing a Tag:
            // no reflection, and the compiler catches a renamed item.
            SongSort sort;
            if (ReferenceEquals(sender, SortTitleItem))
            {
                sort = SongSort.Title;
            }
            else if (ReferenceEquals(sender, SortArtistItem))
            {
                sort = SongSort.Artist;
            }
            else if (ReferenceEquals(sender, SortAlbumItem))
            {
                sort = SongSort.Album;
            }
            else if (ReferenceEquals(sender, SortDateAddedItem))
            {
                sort = SongSort.DateAdded;
            }
            else
            {
                sort = SongSort.DefaultOrder;
            }

            ApplySort(sort);
        }

        private void ApplySort(SongSort sort)
        {
            _sort = sort;
            ApplySortSelection(sort);

            if (_allRows == null)
            {
                return;
            }

            // Rebind a freshly built collection rather than clearing and
            // refilling the existing one: a library-sized ObservableCollection
            // would raise one CollectionChanged per row.
            _view = new ObservableCollection<SongRow>(
                SongSorter.Sort(_allRows, r => r.Song, sort));
            SongsList.ItemsSource = _view;

            // Focus is deliberately left alone: it was on the flyout item, so
            // closing the menu returns it to the Sort button, which is where a
            // user trying another sort wants it. Rebinding also scrolls the
            // list back to the top, so Down lands on the new first row.
        }

        /// <summary>
        /// Mirrors the chosen sort onto the menu (the items are independent
        /// toggles, so mutual exclusion is applied here) and the button label.
        /// </summary>
        private void ApplySortSelection(SongSort sort)
        {
            SortDefaultItem.IsChecked = sort == SongSort.DefaultOrder;
            SortTitleItem.IsChecked = sort == SongSort.Title;
            SortArtistItem.IsChecked = sort == SongSort.Artist;
            SortAlbumItem.IsChecked = sort == SongSort.Album;
            SortDateAddedItem.IsChecked = sort == SongSort.DateAdded;
            SortLabel.Text = SongSorter.Label(sort);
        }

        /// <summary>
        /// Sort, Play All and Shuffle All all speak for the whole library, so
        /// they stay disabled — with the reason on screen — until every page
        /// is loaded (§15: degrade visibly, never silently).
        /// </summary>
        private void SetLibraryActionsEnabled(bool enabled)
        {
            SortButton.IsEnabled = enabled;
            PlayAllButton.IsEnabled = enabled;
            ShuffleAllButton.IsEnabled = enabled;
            LoadingAllText.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;

            // A server that omits `created` cannot order by it; offering the
            // option would silently sort by nothing (§3).
            if (enabled)
            {
                SortDateAddedItem.IsEnabled =
                    _allRows != null && _allRows.Any(r => r.Song.Created.HasValue);
            }
        }

        // --- Playback (decision 2026-07-13, live since Slice 10) --------------

        private void PlayAllButton_Click(object sender, RoutedEventArgs e)
        {
            PlayLibrary(shuffle: false);
        }

        private void ShuffleAllButton_Click(object sender, RoutedEventArgs e)
        {
            PlayLibrary(shuffle: true);
        }

        /// <summary>
        /// §10 Play semantics for the whole library: replace the queue with
        /// every song in the current sort order (Shuffle randomizes the pool
        /// including the first track, §8).
        /// </summary>
        private void PlayLibrary(bool shuffle)
        {
            var view = _view;
            if (view == null || view.Count == 0)
            {
                return;
            }

            PlaybackService.Instance.Play(
                view.Select(r => r.Song).ToList(),
                startIndex: 0,
                shuffle: shuffle,
                context: new QueueContext("Songs", null));
        }

        private void SongsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var row = e.ClickedItem as SongRow;
            if (row != null)
            {
                StartPlayback(row);
            }
        }

        private void PlaySong_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.Tag as SongRow;
            if (row != null)
            {
                StartPlayback(row);
            }
        }

        private void PlayNext_Click(object sender, RoutedEventArgs e)
        {
            // Queue edits (2026-07-17): explicit insert/append, distinct from
            // the replace-only Play actions (§10). With no queue the song
            // simply plays (never invisible state).
            var row = (sender as FrameworkElement)?.Tag as SongRow;
            if (row != null)
            {
                PlaybackService.Instance.PlayNext(row.Song);
            }
        }

        private void AddToQueue_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.Tag as SongRow;
            if (row != null)
            {
                PlaybackService.Instance.AddToQueue(row.Song);
            }
        }

        private void StartPlayback(SongRow row)
        {
            var source = _view;
            if (source == null)
            {
                return;
            }

            // Queue = the rows as shown, positioned at the chosen song, so it
            // follows the current sort. Once the library is fully loaded that
            // is every song — the "rows loaded so far" limit of the 2026-07-15
            // decision now only applies while the load is still running.
            var songs = new List<Song>(source.Count);
            int startIndex = -1;
            foreach (SongRow loaded in source)
            {
                if (ReferenceEquals(loaded, row))
                {
                    startIndex = songs.Count;
                }

                songs.Add(loaded.Song);
            }

            if (startIndex < 0)
            {
                return;
            }

            PlaybackService.Instance.Play(
                songs,
                startIndex,
                shuffle: false,
                context: new QueueContext("Songs", null));
        }

        // --- Context menu (decision 2026-07-13: secondary actions) -----------

        private void SongsList_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            // Console finding 2026-07-14: the gamepad Menu button raises
            // ContextRequested on the FOCUSED element — the ListViewItem —
            // but the flyout sits on the item's child grid, and routed
            // events bubble up, never down. So the menu only ever opened for
            // pointer input (right-click starts at the grid itself, which
            // handles it before reaching here). Redirect the container-level
            // request to the row's own flyout.
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
            // Context-menu favorite toggle (checkpoint #4 third round). The
            // row state only changes after the server accepted the call, so
            // a failure simply leaves everything as it was — the shown state
            // never lies (§8 spirit; the menu is closed by now, so there is
            // no place for a status line).
            var row = (sender as FrameworkElement)?.Tag as SongRow;
            var client = AppSession.Client;
            if (row == null || client == null)
            {
                return;
            }

            bool wantStarred = !row.Song.IsStarred;
            try
            {
                if (wantStarred)
                {
                    await client.StarAsync(row.Song.Id);
                }
                else
                {
                    await client.UnstarAsync(row.Song.Id);
                }

                row.Song.IsStarred = wantStarred;
                row.NotifyStarredChanged();
            }
            catch (SubsonicClientException)
            {
                // Nothing changed; the action simply didn't take.
            }
        }

        private void GoToAlbum_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.Tag as SongRow;
            if (row != null && !string.IsNullOrEmpty(row.AlbumId))
            {
                Frame.Navigate(typeof(AlbumDetailPage), row.AlbumId);
            }
        }

        // --- View states -----------------------------------------------------

        private void ShowLoading()
        {
            LoadingRing.IsActive = true;
            EmptyText.Visibility = Visibility.Collapsed;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            SongsPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowEmpty()
        {
            LoadingRing.IsActive = false;
            EmptyText.Visibility = Visibility.Visible;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            SongsPanel.Visibility = Visibility.Collapsed;

            // Nothing left to load; the disabled actions speak for themselves.
            LoadingAllText.Visibility = Visibility.Collapsed;
        }

        private void ShowContent()
        {
            LoadingRing.IsActive = false;
            EmptyText.Visibility = Visibility.Collapsed;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            SongsPanel.Visibility = Visibility.Visible;

            // Focus convention: loaded content owns focus (see ContentFocus).
            // Target the list, not the page — otherwise first focus would land
            // on the Sort button, where the browse pages deliberately put the
            // first card instead of the view toggle (§16 2026-07-19).
            ContentFocus.MoveInto(SongsList);
        }

        private void ShowConnectError(string message)
        {
            ShowErrorCore(message, retryable: true);
        }

        private void ShowUnsupported(string message)
        {
            // Same panel, but Retry is pointless: the capability won't
            // appear by retrying.
            ShowErrorCore(message, retryable: false);
        }

        private void ShowErrorCore(string message, bool retryable)
        {
            LoadingRing.IsActive = false;
            EmptyText.Visibility = Visibility.Collapsed;
            SongsPanel.Visibility = Visibility.Collapsed;
            ConnectErrorPanel.Visibility = Visibility.Visible;
            ConnectErrorText.Text = message;
            LoadingAllText.Visibility = Visibility.Collapsed;
            ConnectRetryButton.Visibility = retryable ? Visibility.Visible : Visibility.Collapsed;

            if (retryable)
            {
                // Controller-first: the error state must own focus, or
                // Right-from-nav lands on nothing.
                ConnectRetryButton.Focus(FocusState.Keyboard);
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
