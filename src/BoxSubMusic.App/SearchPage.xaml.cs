using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoxSubMusic.App.Models;
using BoxSubMusic.App.Services;
using BoxSubMusic.Core.Playback;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Search (Slice 13, requirements §5 + the §17 resolution 2026-07-19).
    /// The query runs on submit; the "All" view shows the top matches of all
    /// three scopes from one search3 request, and each scope filter switches
    /// to a full incrementally paged list (fetched per scope via search3
    /// offsets). The page is navigation-cached so B-back from a result's
    /// detail page returns to the same query and results — the one deliberate
    /// exception to the refetch-on-navigation pattern (documented staleness:
    /// starred markers on kept results can lag until the next search).
    /// </summary>
    public sealed partial class SearchPage : Page
    {
        private const int AllArtistCount = 6;
        private const int AllAlbumCount = 6;
        private const int AllSongCount = 10;
        // Display box the request is sized from: result cards are 200 epx.
        private const int CoverArtSize = 200;

        /// <summary>search3 exists since Subsonic API 1.8 (§3 gate).</summary>
        private static readonly Version SearchMinimumApiVersion = new Version(1, 8);

        private enum SearchScope
        {
            All,
            Artists,
            Albums,
            Songs,
        }

        private CancellationTokenSource _cts;
        private string _query;
        private SearchScope _scope = SearchScope.All;

        // "All" results are plain lists (one request, no paging); scoped
        // results are incremental sources created lazily per scope and reset
        // by every new submit. A null scope field means "not loaded yet for
        // this query" — a failed first page leaves it null so Retry and
        // re-entering the scope start clean.
        private bool _allLoaded;
        private int _allTotal;
        private List<SongRow> _allSongRows;
        private IncrementalSource<ArtistCard> _scopeArtists;
        private IncrementalSource<AlbumCard> _scopeAlbums;
        private IncrementalSource<SongRow> _scopeSongs;

        public SearchPage()
        {
            InitializeComponent();

            // Keep query + results alive across detail-page round trips:
            // losing the search on every B-back would make result browsing
            // start over each time.
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _cts = new CancellationTokenSource();

            // Content owns focus (2026-07-14 convention): the query box is
            // the page's first focusable element, so a fresh page lands
            // there; a cached page with results also re-enters at the box,
            // ready for the next query.
            ContentFocus.MoveInto(this);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _cts?.Cancel();
            _cts = null;
        }

        // --- Query submit ------------------------------------------------------

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            _ = SubmitAsync();
        }

        private void QueryBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (HandleGamepadTextEditing(QueryBox, e))
            {
                return;
            }

            // Enter = the console OSK's accept (or a hardware keyboard);
            // search runs on submit by design (§17 resolution — no
            // live-as-you-type requests).
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                _ = SubmitAsync();
            }
        }

        private async Task SubmitAsync()
        {
            string query = QueryBox.Text?.Trim();
            var cts = _cts;
            if (string.IsNullOrEmpty(query) || cts == null)
            {
                return;
            }

            _query = query;

            // Scoped results belong to the previous query; drop them and
            // land on All for every new search.
            _allLoaded = false;
            _allSongRows = null;
            _scopeArtists = null;
            _scopeAlbums = null;
            _scopeSongs = null;
            ArtistsScopeGrid.ItemsSource = null;
            AlbumsScopeGrid.ItemsSource = null;
            SongsScopeList.ItemsSource = null;

            SetScope(SearchScope.All);
            FilterPanel.Visibility = Visibility.Visible;

            await LoadAllAsync(cts.Token);
        }

        private async Task LoadAllAsync(CancellationToken cancellationToken)
        {
            ShowLoading();

            if (!await EnsureReadyAsync(cancellationToken))
            {
                return;
            }

            string query = _query;
            try
            {
                SearchResult result = await AppSession.Client.SearchAsync(
                    query,
                    artistCount: AllArtistCount,
                    albumCount: AllAlbumCount,
                    songCount: AllSongCount,
                    cancellationToken: cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var artists = result.Artists.Select(ToArtistCard).ToList();
                var albums = result.Albums.Select(ToAlbumCard).ToList();
                _allSongRows = result.Songs.Select(ToRow).ToList();

                AllArtistsList.ItemsSource = artists;
                AllAlbumsList.ItemsSource = albums;
                AllSongsList.ItemsSource = _allSongRows;

                // Sections with no matches collapse instead of showing empty
                // strips; a fully empty result is the page-level empty state.
                AllArtistsSection.Visibility = ToVisibility(artists.Count > 0);
                AllAlbumsSection.Visibility = ToVisibility(albums.Count > 0);
                AllSongsSection.Visibility = ToVisibility(_allSongRows.Count > 0);

                _allLoaded = true;
                _allTotal = artists.Count + albums.Count + _allSongRows.Count;
                if (_allTotal == 0)
                {
                    ShowEmpty("No results for “" + query + "”.");
                }
                else
                {
                    ShowContent(moveFocus: true);
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

        /// <summary>
        /// Connect-on-use plus the §3 capability gate: search3 needs API 1.8.
        /// Returns false after showing the appropriate error state.
        /// </summary>
        private async Task<bool> EnsureReadyAsync(CancellationToken cancellationToken)
        {
            if (AppSession.Client == null)
            {
                ShowConnectError("You are not signed in to a server.");
                return false;
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
                return false;
            }
            catch (SubsonicClientException ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ShowConnectError(ex.Message);
                }
                return false;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            var capabilities = AppSession.Client.Capabilities;
            if (capabilities != null
                && Version.TryParse(capabilities.ApiVersion, out Version apiVersion)
                && apiVersion < SearchMinimumApiVersion)
            {
                ShowUnsupported(
                    "This server reports Subsonic API " + capabilities.ApiVersion
                    + ", older than 1.8 — the version that introduced search. "
                    + "Browse by artist or album instead.");
                return false;
            }

            return true;
        }

        // --- Scope filters (§5: in the content area) ---------------------------

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            SearchScope scope;
            if (ReferenceEquals(sender, FilterArtistsButton))
            {
                scope = SearchScope.Artists;
            }
            else if (ReferenceEquals(sender, FilterAlbumsButton))
            {
                scope = SearchScope.Albums;
            }
            else if (ReferenceEquals(sender, FilterSongsButton))
            {
                scope = SearchScope.Songs;
            }
            else
            {
                scope = SearchScope.All;
            }

            if (scope == _scope)
            {
                // Segmented control: clicking the active filter keeps it on.
                SetScope(scope);
                return;
            }

            SetScope(scope);
            _ = ShowScopeAsync(scope);
        }

        private void SetScope(SearchScope scope)
        {
            _scope = scope;
            FilterAllButton.IsChecked = scope == SearchScope.All;
            FilterArtistsButton.IsChecked = scope == SearchScope.Artists;
            FilterAlbumsButton.IsChecked = scope == SearchScope.Albums;
            FilterSongsButton.IsChecked = scope == SearchScope.Songs;
        }

        private async Task ShowScopeAsync(SearchScope scope)
        {
            var cts = _cts;
            if (cts == null || _query == null)
            {
                return;
            }

            switch (scope)
            {
                case SearchScope.All:
                    if (_allLoaded)
                    {
                        ShowScopeOrEmpty(_allTotal, "No results for “" + _query + "”.");
                    }
                    else
                    {
                        await LoadAllAsync(cts.Token);
                    }
                    break;

                case SearchScope.Artists:
                    if (_scopeArtists != null)
                    {
                        ShowScopeOrEmpty(_scopeArtists.Count, "No artists match “" + _query + "”.");
                    }
                    else
                    {
                        await LoadArtistsScopeAsync(cts.Token);
                    }
                    break;

                case SearchScope.Albums:
                    if (_scopeAlbums != null)
                    {
                        ShowScopeOrEmpty(_scopeAlbums.Count, "No albums match “" + _query + "”.");
                    }
                    else
                    {
                        await LoadAlbumsScopeAsync(cts.Token);
                    }
                    break;

                case SearchScope.Songs:
                    if (_scopeSongs != null)
                    {
                        ShowScopeOrEmpty(_scopeSongs.Count, "No songs match “" + _query + "”.");
                    }
                    else
                    {
                        await LoadSongsScopeAsync(cts.Token);
                    }
                    break;
            }
        }

        private void ShowScopeOrEmpty(int count, string emptyMessage)
        {
            if (count == 0)
            {
                ShowEmpty(emptyMessage);
            }
            else
            {
                ShowContent(moveFocus: false);
            }
        }

        private async Task LoadArtistsScopeAsync(CancellationToken cancellationToken)
        {
            string query = _query;
            var source = new IncrementalSource<ArtistCard>(
                async (offset, size, ct) =>
                {
                    SearchResult page = await RequireClient().SearchAsync(
                        query, artistCount: size, albumCount: 0, songCount: 0,
                        artistOffset: offset, cancellationToken: ct);
                    return page.Artists.Select(ToArtistCard).ToList();
                });
            source.LoadFailed += Scope_LoadFailed;

            await LoadScopeFirstPageAsync(
                source, cancellationToken,
                loaded =>
                {
                    _scopeArtists = source;
                    ArtistsScopeGrid.ItemsSource = source;
                    ShowScopeOrEmpty(loaded, "No artists match “" + query + "”.");
                });
        }

        private async Task LoadAlbumsScopeAsync(CancellationToken cancellationToken)
        {
            string query = _query;
            var source = new IncrementalSource<AlbumCard>(
                async (offset, size, ct) =>
                {
                    SearchResult page = await RequireClient().SearchAsync(
                        query, artistCount: 0, albumCount: size, songCount: 0,
                        albumOffset: offset, cancellationToken: ct);
                    return page.Albums.Select(ToAlbumCard).ToList();
                });
            source.LoadFailed += Scope_LoadFailed;

            await LoadScopeFirstPageAsync(
                source, cancellationToken,
                loaded =>
                {
                    _scopeAlbums = source;
                    AlbumsScopeGrid.ItemsSource = source;
                    ShowScopeOrEmpty(loaded, "No albums match “" + query + "”.");
                });
        }

        private async Task LoadSongsScopeAsync(CancellationToken cancellationToken)
        {
            string query = _query;
            var source = new IncrementalSource<SongRow>(
                async (offset, size, ct) =>
                {
                    SearchResult page = await RequireClient().SearchAsync(
                        query, artistCount: 0, albumCount: 0, songCount: size,
                        songOffset: offset, cancellationToken: ct);
                    return page.Songs.Select(ToRow).ToList();
                });
            source.LoadFailed += Scope_LoadFailed;

            await LoadScopeFirstPageAsync(
                source, cancellationToken,
                loaded =>
                {
                    _scopeSongs = source;
                    SongsScopeList.ItemsSource = source;
                    ShowScopeOrEmpty(loaded, "No songs match “" + query + "”.");
                });
        }

        /// <summary>
        /// Shared first-page flow for the three scoped lists: loading state,
        /// connect check, then hand the loaded count to the scope's own
        /// wire-up. On failure the scope field stays null, so Retry and
        /// re-entering the scope start from a clean source.
        /// </summary>
        private async Task LoadScopeFirstPageAsync<T>(
            IncrementalSource<T> source,
            CancellationToken cancellationToken,
            Action<int> onLoaded)
        {
            ShowLoading();

            if (!await EnsureReadyAsync(cancellationToken))
            {
                return;
            }

            try
            {
                int loaded = await source.LoadFirstPageAsync(cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    onLoaded(loaded);
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

        private void Scope_LoadFailed(object sender, Exception ex)
        {
            // A later page failed mid-scroll (§3: surface it; Retry reloads
            // the scope from the top). Ignore sources abandoned by a newer
            // query or scope switch.
            if (!ReferenceEquals(sender, _scopeArtists)
                && !ReferenceEquals(sender, _scopeAlbums)
                && !ReferenceEquals(sender, _scopeSongs))
            {
                return;
            }

            ShowConnectError("Couldn't load more results. " + ex.Message);
        }

        private void ConnectRetryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_query == null)
            {
                return;
            }

            // Retry reloads the current scope from the top (the browse-view
            // rule). Scoped sources are recreated so a mid-scroll failure
            // cannot leave a gap.
            switch (_scope)
            {
                case SearchScope.Artists:
                    _scopeArtists = null;
                    break;
                case SearchScope.Albums:
                    _scopeAlbums = null;
                    break;
                case SearchScope.Songs:
                    _scopeSongs = null;
                    break;
                default:
                    _allLoaded = false;
                    break;
            }

            _ = ShowScopeAsync(_scope);
        }

        private static SubsonicApiClient RequireClient()
        {
            var client = AppSession.Client;
            if (client == null)
            {
                throw new SubsonicConnectionException("You are not signed in to a server.");
            }

            return client;
        }

        // --- Result interactions ----------------------------------------------

        private void Artist_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ArtistCard card)
            {
                Frame.Navigate(typeof(ArtistDetailPage), card.Id);
            }
        }

        private void Album_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AlbumCard card)
            {
                Frame.Navigate(typeof(AlbumDetailPage), card.Id);
            }
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
            // Queue = the song results loaded in the row's own list (All's
            // top matches, or the scoped list's pages so far), positioned at
            // the chosen song — the Songs-browse semantics (§17 resolution).
            IReadOnlyList<SongRow> pool = null;
            if (_allSongRows != null && _allSongRows.Contains(row))
            {
                pool = _allSongRows;
            }
            else if (_scopeSongs != null && _scopeSongs.Contains(row))
            {
                pool = _scopeSongs;
            }

            if (pool == null)
            {
                return;
            }

            var songs = new List<Song>(pool.Count);
            int startIndex = -1;
            foreach (SongRow loaded in pool)
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
                context: new QueueContext("Search results", null));
        }

        // --- Context menus -----------------------------------------------------

        private void List_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            // The gamepad Menu button raises ContextRequested on the focused
            // item container, but the flyout sits on the template root —
            // redirect (the console-verified pattern from the song lists).
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
            // Change-only-after-success (checkpoint #4 third round): a
            // failure leaves everything untouched — the menu is closed by
            // then, so nothing shown ever lies.
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

        private void GoToArtist_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.Tag as SongRow;
            if (row != null && row.HasArtistId)
            {
                Frame.Navigate(typeof(ArtistDetailPage), row.ArtistId);
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

        // --- Mapping -----------------------------------------------------------

        private static ArtistCard ToArtistCard(Artist artist)
        {
            var artwork = CoverArt.UriFor(artist.CoverArtId, CoverArtSize);
            var label = artist.AlbumCount == 1 ? "1 album" : artist.AlbumCount + " albums";
            return new ArtistCard(artist.Id, artist.Name, label, artwork);
        }

        private static AlbumCard ToAlbumCard(Album album)
        {
            var artwork = CoverArt.UriFor(album.CoverArtId, CoverArtSize);
            return new AlbumCard(album.Id, album.Name, album.Artist ?? string.Empty, artwork);
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

        // --- Inline gamepad text editing (SetupPage mapping) -------------------

        /// <summary>
        /// With the OSK dismissed the controller has no way to fix a typo
        /// (console finding 2026-07-14): mirror the Xbox OSK's accelerators
        /// on the box itself — X deletes backwards, LB/RB move the caret.
        /// Marking these handled deliberately outranks the shell's LB/RB
        /// region cycling while the box has focus. Duplicated from SetupPage
        /// (which is outside the shell and console-verified) rather than
        /// shared, to leave that page untouched.
        /// </summary>
        private static bool HandleGamepadTextEditing(TextBox box, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.GamepadX:
                    if (box.SelectionLength > 0)
                    {
                        int selectionStart = box.SelectionStart;
                        box.Text = box.Text.Remove(selectionStart, box.SelectionLength);
                        box.Select(selectionStart, 0);
                    }
                    else if (box.SelectionStart > 0)
                    {
                        int caret = box.SelectionStart;
                        box.Text = box.Text.Remove(caret - 1, 1);
                        box.Select(caret - 1, 0);
                    }
                    e.Handled = true;
                    return true;

                case VirtualKey.GamepadLeftShoulder:
                {
                    // A selection collapses to its start, like a Left arrow.
                    int target = box.SelectionLength > 0
                        ? box.SelectionStart
                        : Math.Max(0, box.SelectionStart - 1);
                    box.Select(target, 0);
                    e.Handled = true;
                    return true;
                }

                case VirtualKey.GamepadRightShoulder:
                {
                    int end = box.SelectionStart + box.SelectionLength;
                    int target = box.SelectionLength > 0
                        ? end
                        : Math.Min(box.Text.Length, end + 1);
                    box.Select(target, 0);
                    e.Handled = true;
                    return true;
                }

                default:
                    return false;
            }
        }

        // --- View states -------------------------------------------------------

        private void ShowLoading()
        {
            IdlePanel.Visibility = Visibility.Collapsed;
            LoadingRing.IsActive = true;
            EmptyText.Visibility = Visibility.Collapsed;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            HideResultViews();
        }

        private void ShowEmpty(string message)
        {
            IdlePanel.Visibility = Visibility.Collapsed;
            LoadingRing.IsActive = false;
            EmptyText.Text = message;
            EmptyText.Visibility = Visibility.Visible;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            HideResultViews();
        }

        private void ShowContent(bool moveFocus)
        {
            IdlePanel.Visibility = Visibility.Collapsed;
            LoadingRing.IsActive = false;
            EmptyText.Visibility = Visibility.Collapsed;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            HideResultViews();

            UIElement view;
            switch (_scope)
            {
                case SearchScope.Artists:
                    view = ArtistsScopeGrid;
                    break;
                case SearchScope.Albums:
                    view = AlbumsScopeGrid;
                    break;
                case SearchScope.Songs:
                    view = SongsScopePanel;
                    break;
                default:
                    view = AllScroll;
                    break;
            }

            view.Visibility = Visibility.Visible;

            // Focus moves into the results only on a fresh submit; a scope
            // switch keeps the controller on the filter row (stealing focus
            // on every filter press would fight the user).
            if (moveFocus)
            {
                ContentFocus.MoveInto(view);
            }
        }

        private void ShowConnectError(string message)
        {
            ShowErrorCore(message, retryable: true);
        }

        private void ShowUnsupported(string message)
        {
            // Retry is pointless: the capability won't appear by retrying.
            ShowErrorCore(message, retryable: false);
        }

        private void ShowErrorCore(string message, bool retryable)
        {
            IdlePanel.Visibility = Visibility.Collapsed;
            LoadingRing.IsActive = false;
            EmptyText.Visibility = Visibility.Collapsed;
            HideResultViews();
            ConnectErrorPanel.Visibility = Visibility.Visible;
            ConnectErrorText.Text = message;
            ConnectRetryButton.Visibility = retryable ? Visibility.Visible : Visibility.Collapsed;

            if (retryable)
            {
                ConnectRetryButton.Focus(FocusState.Keyboard);
            }
        }

        private void HideResultViews()
        {
            AllScroll.Visibility = Visibility.Collapsed;
            ArtistsScopeGrid.Visibility = Visibility.Collapsed;
            AlbumsScopeGrid.Visibility = Visibility.Collapsed;
            SongsScopePanel.Visibility = Visibility.Collapsed;
        }

        private static Visibility ToVisibility(bool visible)
        {
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
