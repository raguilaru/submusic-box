using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoxSubMusic.App.Models;
using BoxSubMusic.App.Services;
using BoxSubMusic.Core.Concurrency;
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
    /// Favorites (§5 Favorites addition, decision 2026-07-26): the starred
    /// artists, albums and songs, split into three tabs whose selection and
    /// per-tab sort are remembered (<see cref="FavoritesPreferences"/>).
    ///
    /// One getStarred2 request fills all three tabs — the endpoint takes no
    /// counts or offsets and returns the complete favorites set — so this is
    /// the one browse-ish view with no incremental paging, and sorting is a
    /// client-side reorder of everything rather than of "pages loaded so far".
    /// Servers older than API 1.8 get an explicit unsupported state (§3).
    /// </summary>
    public sealed partial class FavoritesPage : Page
    {
        // Grid cards are 200 epx; request double for 200%-scale displays.
        private const int CoverArtSize = 400;

        /// <summary>getStarred2 exists since Subsonic API 1.8 (§3 gate).</summary>
        private static readonly Version StarredMinimumApiVersion = new Version(1, 8);

        // Server order, kept so "Default Order" can be restored without
        // refetching; the *View collections are what the lists bind to.
        private List<ArtistCard> _artists = new List<ArtistCard>();
        private List<AlbumCard> _albums = new List<AlbumCard>();
        private List<SongRow> _songs = new List<SongRow>();

        private readonly ObservableCollection<ArtistCard> _artistView = new ObservableCollection<ArtistCard>();
        private readonly ObservableCollection<AlbumCard> _albumView = new ObservableCollection<AlbumCard>();
        private readonly ObservableCollection<SongRow> _songView = new ObservableCollection<SongRow>();

        private readonly MenuFlyout _sortFlyout = new MenuFlyout { Placement = FlyoutPlacementMode.Bottom };

        private CancellationTokenSource _cts;
        private FavoritesTab _tab;
        private bool _loaded;

        // A Play All / Shuffle All on the Artists or Albums tab fans out real
        // requests; the flag keeps a second press from starting a rival build.
        private bool _buildingPool;

        public FavoritesPage()
        {
            InitializeComponent();
            ArtistsGrid.ItemsSource = _artistView;
            AlbumsGrid.ItemsSource = _albumView;
            SongsList.ItemsSource = _songView;
            SortButton.Flyout = _sortFlyout;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _cts = new CancellationTokenSource();

            // Restore the remembered tab before loading, so the first content
            // reveal already shows the right scope (no visible flip).
            _tab = FavoritesPreferences.Tab;
            ApplyTabButtons();
            RebuildSortMenu();

            _ = LoadAsync(_cts.Token);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _cts?.Cancel();
            _cts = null;
        }

        // --- Loading -----------------------------------------------------------

        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            _loaded = false;
            ShowLoading();

            if (!await EnsureReadyAsync(cancellationToken))
            {
                return;
            }

            try
            {
                StarredResult starred = await AppSession.Client.GetStarred2Async(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _artists = starred.Artists.Select(ToArtistCard).ToList();
                _albums = starred.Albums.Select(ToAlbumCard).ToList();
                _songs = starred.Songs.Select(ToRow).ToList();

                _loaded = true;
                ApplySort();
                ShowActiveTab(moveFocus: true);
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
        /// Connect-on-use plus the §3 capability gate: getStarred2 needs API
        /// 1.8. Returns false after showing the appropriate error state.
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
                && apiVersion < StarredMinimumApiVersion)
            {
                ShowUnsupported(
                    "This server reports Subsonic API " + capabilities.ApiVersion
                    + ", older than 1.8 — the version that introduced favorites listing. "
                    + "Your stars still work; they just can't be listed here.");
                return false;
            }

            return true;
        }

        private void ConnectRetryButton_Click(object sender, RoutedEventArgs e)
        {
            var cts = _cts;
            if (cts != null)
            {
                _ = LoadAsync(cts.Token);
            }
        }

        // --- Tabs ---------------------------------------------------------------

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            FavoritesTab tab;
            if (ReferenceEquals(sender, AlbumsTabButton))
            {
                tab = FavoritesTab.Albums;
            }
            else if (ReferenceEquals(sender, SongsTabButton))
            {
                tab = FavoritesTab.Songs;
            }
            else
            {
                tab = FavoritesTab.Artists;
            }

            // Segmented control: clicking the active tab keeps it on (the
            // ToggleButton has just unchecked itself).
            if (tab == _tab)
            {
                ApplyTabButtons();
                return;
            }

            _tab = tab;
            FavoritesPreferences.Tab = tab;
            ApplyTabButtons();
            RebuildSortMenu();

            // Tabs stay usable while loading or in an error/unsupported state
            // (they are the only focusable thing an unsupported state leaves),
            // but they must not replace that state with a misleading empty
            // list. The pending load reveals the new tab when it finishes.
            if (!_loaded)
            {
                return;
            }

            // The sort for the tab being entered may never have been applied
            // to its collection yet (a tab the user hasn't visited this load).
            ApplySort();

            // Focus stays on the tab row: stealing it into the results on
            // every tab press would fight the user (the Search filter rule).
            ShowActiveTab(moveFocus: false);
        }

        private void ApplyTabButtons()
        {
            ArtistsTabButton.IsChecked = _tab == FavoritesTab.Artists;
            AlbumsTabButton.IsChecked = _tab == FavoritesTab.Albums;
            SongsTabButton.IsChecked = _tab == FavoritesTab.Songs;
        }

        // --- Sorting ------------------------------------------------------------

        /// <summary>One entry in the sort flyout: its label and its enum value.</summary>
        private struct SortOption
        {
            public SortOption(string label, int value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }
            public int Value { get; }
        }

        private static readonly SortOption[] ArtistSortOptions =
        {
            new SortOption("Alphabetical", (int)ArtistSort.Alphabetical),
            new SortOption("Most Albums", (int)ArtistSort.MostAlbums),
        };

        private static readonly SortOption[] AlbumSortOptions =
        {
            new SortOption("Year", (int)AlbumSort.Year),
            new SortOption("Alphabetical", (int)AlbumSort.Alphabetical),
        };

        private static readonly SortOption[] SongSortOptions =
        {
            new SortOption("Default Order", (int)SongSort.Default),
            new SortOption("Title", (int)SongSort.Title),
            new SortOption("Artist", (int)SongSort.Artist),
            new SortOption("Album", (int)SongSort.Album),
            new SortOption("Date Added", (int)SongSort.DateAdded),
        };

        private SortOption[] OptionsForActiveTab()
        {
            switch (_tab)
            {
                case FavoritesTab.Albums: return AlbumSortOptions;
                case FavoritesTab.Songs: return SongSortOptions;
                default: return ArtistSortOptions;
            }
        }

        private int ActiveSortValue()
        {
            switch (_tab)
            {
                case FavoritesTab.Albums: return (int)FavoritesPreferences.AlbumSort;
                case FavoritesTab.Songs: return (int)FavoritesPreferences.SongSort;
                default: return (int)FavoritesPreferences.ArtistSort;
            }
        }

        /// <summary>
        /// Rebuilds the sort flyout for the active tab: the option sets differ
        /// per tab, so the menu is generated rather than declared. The button's
        /// label always names the active sort, so the current order is readable
        /// without opening the menu.
        /// </summary>
        private void RebuildSortMenu()
        {
            SortOption[] options = OptionsForActiveTab();
            int active = ActiveSortValue();

            _sortFlyout.Items.Clear();
            foreach (SortOption option in options)
            {
                var item = new ToggleMenuFlyoutItem
                {
                    Text = option.Label,
                    IsChecked = option.Value == active,
                    Tag = option.Value,
                };
                item.Click += SortOption_Click;
                _sortFlyout.Items.Add(item);
            }

            SortLabel.Text = options.First(o => o.Value == active).Label;
        }

        private void SortOption_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as ToggleMenuFlyoutItem;
            if (item == null || !(item.Tag is int))
            {
                return;
            }

            int value = (int)item.Tag;

            switch (_tab)
            {
                case FavoritesTab.Albums:
                    FavoritesPreferences.AlbumSort = (AlbumSort)value;
                    break;
                case FavoritesTab.Songs:
                    FavoritesPreferences.SongSort = (SongSort)value;
                    break;
                default:
                    FavoritesPreferences.ArtistSort = (ArtistSort)value;
                    break;
            }

            // Re-checking the already-active item unchecked it; the rebuild
            // restores the checkmarks from the stored preference.
            RebuildSortMenu();
            ApplySort();
        }

        /// <summary>
        /// Reorders the active tab's bound collection in place. Only the
        /// active tab is sorted: an unvisited tab is sorted when it is entered
        /// (Tab_Click), so switching never pays for the other two scopes.
        /// LINQ ordering is stable, so equal keys keep server order.
        /// </summary>
        private void ApplySort()
        {
            if (!_loaded)
            {
                return;
            }

            switch (_tab)
            {
                case FavoritesTab.Albums:
                    Replace(_albumView, SortAlbums(_albums, FavoritesPreferences.AlbumSort));
                    break;
                case FavoritesTab.Songs:
                    Replace(_songView, SortSongs(_songs, FavoritesPreferences.SongSort));
                    break;
                default:
                    Replace(_artistView, SortArtists(_artists, FavoritesPreferences.ArtistSort));
                    break;
            }
        }

        private static IEnumerable<ArtistCard> SortArtists(List<ArtistCard> cards, ArtistSort sort)
        {
            switch (sort)
            {
                case ArtistSort.MostAlbums:
                    // Biggest discography first; ties fall back to the name so
                    // the order is stable and readable rather than arbitrary.
                    return cards
                        .OrderByDescending(c => c.AlbumCount)
                        .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase);
                default:
                    return cards.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase);
            }
        }

        private static IEnumerable<AlbumCard> SortAlbums(List<AlbumCard> cards, AlbumSort sort)
        {
            switch (sort)
            {
                case AlbumSort.Alphabetical:
                    return cards.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase);
                default:
                    // Newest first — the useful end of a favorites list — with
                    // undated albums last rather than masquerading as year 0.
                    return cards
                        .OrderByDescending(c => c.Year.HasValue)
                        .ThenByDescending(c => c.Year ?? 0)
                        .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase);
            }
        }

        private static IEnumerable<SongRow> SortSongs(List<SongRow> rows, SongSort sort)
        {
            switch (sort)
            {
                case SongSort.Title:
                    return rows.OrderBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase);
                case SongSort.Artist:
                    return rows
                        .OrderBy(r => r.Artist, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase);
                case SongSort.Album:
                    // Within an album, the album's own order beats the title.
                    return rows
                        .OrderBy(r => r.Album, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(r => r.Song.DiscNumber ?? 1)
                        .ThenBy(r => r.Song.Track ?? int.MaxValue);
                case SongSort.DateAdded:
                    // Most recently added to the library first; songs whose
                    // server sent no date sort last.
                    return rows
                        .OrderByDescending(r => r.Song.Created.HasValue)
                        .ThenByDescending(r => r.Song.Created ?? DateTimeOffset.MinValue);
                default:
                    return rows; // server order
            }
        }

        private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> ordered)
        {
            // Materialize before clearing: the ordered sequence is a lazy query
            // over the source list, and Clear() must not run before it is read.
            var items = ordered.ToList();
            target.Clear();
            foreach (T item in items)
            {
                target.Add(item);
            }
        }

        // --- Interactions -------------------------------------------------------

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
            // Queue = the favorite songs as currently sorted, positioned at the
            // chosen song — the Songs-browse semantics (§10 replace-not-append).
            // Unlike Songs browse there is no "pages loaded so far" caveat: the
            // whole favorites set is already here.
            int startIndex = _songView.IndexOf(row);
            if (startIndex < 0)
            {
                return;
            }

            var songs = _songView.Select(r => r.Song).ToList();
            PlaybackService.Instance.Play(songs, startIndex, shuffle: false, context: FavoritesContext(songs));
        }

        // --- Play All / Shuffle All (§10 over the whole active tab) --------------

        private void PlayAllButton_Click(object sender, RoutedEventArgs e)
        {
            _ = PlayAllAsync(shuffle: false);
        }

        private void ShuffleAllButton_Click(object sender, RoutedEventArgs e)
        {
            _ = PlayAllAsync(shuffle: true);
        }

        /// <summary>
        /// Replaces the queue with everything on the active tab, in its current
        /// sort order (§10 replace-not-append; with shuffle the whole pool is
        /// randomized including the first track, per the §8 shuffle rule).
        ///
        /// Songs is instant — the tracks are already loaded. Artists and Albums
        /// are not: cards carry ids, so the songs must be fetched, and the
        /// buttons stay disabled behind a ring until they are. The build reads
        /// the sorted *View collections, so what plays is exactly the order on
        /// screen.
        /// </summary>
        private async Task PlayAllAsync(bool shuffle)
        {
            var cts = _cts;
            if (_buildingPool || !_loaded || cts == null)
            {
                return;
            }

            _buildingPool = true;
            SetActionsBusy(true);
            try
            {
                List<Song> pool = await BuildActivePoolAsync(cts.Token);
                if (cts.Token.IsCancellationRequested)
                {
                    // The page was left mid-build; starting playback now would
                    // be a surprise from a screen the user already dismissed.
                    return;
                }

                if (pool == null)
                {
                    // The fetch failed and already said so.
                    return;
                }

                if (pool.Count == 0)
                {
                    // Favorited containers that hold no songs: say so, since
                    // silence would be indistinguishable from a broken button.
                    Toast.Show("Nothing to play — these favorites have no songs.");
                    return;
                }

                // Audible playback plus the bar reveal are the feedback (the
                // §16 rule: Play actions do not toast).
                PlaybackService.Instance.Play(pool, 0, shuffle, FavoritesContext(pool));
            }
            finally
            {
                _buildingPool = false;
                if (_cts != null)
                {
                    SetActionsBusy(false);
                }
            }
        }

        /// <summary>
        /// The active tab's songs in on-screen order. Returns null when a fetch
        /// failed (the caller stays silent — a toast has already been shown).
        /// </summary>
        private async Task<List<Song>> BuildActivePoolAsync(CancellationToken cancellationToken)
        {
            var client = AppSession.Client;
            if (client == null)
            {
                Toast.Show("You are not signed in to a server.");
                return null;
            }

            try
            {
                switch (_tab)
                {
                    case FavoritesTab.Albums:
                        return await BuildAlbumPoolAsync(
                            client, _albumView.Select(c => c.Id).ToList(), cancellationToken);

                    case FavoritesTab.Songs:
                        // Already in hand — no request at all.
                        return _songView.Select(r => r.Song).ToList();

                    default:
                        return await BuildArtistPoolAsync(client, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (SubsonicClientException)
            {
                Toast.Show("Couldn't load your favorites — playback didn't start.");
                return null;
            }
        }

        /// <summary>
        /// One getAlbum per favorite album, bounded (§16 2026-07-19 fan-out
        /// rule) and order-preserving, so albums land in the tab's sort order
        /// and each album's tracks in disc/track order.
        /// </summary>
        private static async Task<List<Song>> BuildAlbumPoolAsync(
            SubsonicApiClient client, IReadOnlyList<string> albumIds, CancellationToken cancellationToken)
        {
            AlbumDetail[] loaded = await BoundedParallel.MapAsync(
                albumIds,
                id => client.GetAlbumAsync(id, cancellationToken),
                QueueActions.MaxParallelFetches);

            var songs = new List<Song>();
            foreach (AlbumDetail album in loaded)
            {
                songs.AddRange(album.Songs);
            }

            return songs;
        }

        /// <summary>
        /// Every song of every favorite artist. This is the expensive one: each
        /// artist needs a getArtist, and every album of every artist needs a
        /// getAlbum. Rather than nesting two bounded fan-outs (whose limits
        /// would multiply), the albums of all artists are flattened into one
        /// list and fetched at a single bound of
        /// <see cref="QueueActions.MaxParallelFetches"/>. Order is preserved
        /// end to end: artists in the tab's sort order, each artist's albums
        /// oldest-first (§8), each album's songs in disc/track order.
        /// </summary>
        private async Task<List<Song>> BuildArtistPoolAsync(
            SubsonicApiClient client, CancellationToken cancellationToken)
        {
            var artistIds = _artistView.Select(c => c.Id).ToList();
            ArtistDetail[] details = await BoundedParallel.MapAsync(
                artistIds,
                id => client.GetArtistAsync(id, cancellationToken),
                QueueActions.MaxParallelFetches);

            var albumIds = new List<string>();
            foreach (ArtistDetail detail in details)
            {
                albumIds.AddRange(detail.Albums
                    .OrderBy(a => a.Year ?? int.MaxValue)
                    .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(a => a.Id));
            }

            return await BuildAlbumPoolAsync(client, albumIds, cancellationToken);
        }

        /// <summary>
        /// Queue context for anything started here. The artwork is the first
        /// cover the pool offers, so the §6.1 resume card has something to show
        /// instead of a blank tile.
        /// </summary>
        private static QueueContext FavoritesContext(IReadOnlyList<Song> pool)
        {
            string coverArtId = pool
                .Select(s => s.CoverArtId)
                .FirstOrDefault(id => !string.IsNullOrEmpty(id));
            return new QueueContext("Favorites", coverArtId);
        }

        private void SetActionsBusy(bool busy)
        {
            ActionRing.IsActive = busy;

            // Sort is disabled too: reordering the list mid-build would make
            // the queue that lands disagree with what is on screen.
            SortButton.IsEnabled = !busy;
            PlayAllButton.IsEnabled = !busy && HasPlayableScope();
            ShuffleAllButton.IsEnabled = PlayAllButton.IsEnabled;
        }

        private bool HasPlayableScope()
        {
            if (!_loaded)
            {
                return false;
            }

            switch (_tab)
            {
                case FavoritesTab.Albums: return _albumView.Count > 0;
                case FavoritesTab.Songs: return _songView.Count > 0;
                default: return _artistView.Count > 0;
            }
        }

        private void GoToAlbum_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.Tag as SongRow;
            if (row != null && row.HasAlbumId)
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

        private async void RemoveFavorite_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.Tag as SongRow;
            if (row == null || !await UnstarAsync(row.Song.Id))
            {
                return;
            }

            row.Song.IsStarred = false;
            _songs.Remove(row);
            _songView.Remove(row);
            AfterRemoval();
        }

        private async void RemoveArtistFavorite_Click(object sender, RoutedEventArgs e)
        {
            var card = (sender as FrameworkElement)?.Tag as ArtistCard;
            if (card == null || !await UnstarAsync(card.Id))
            {
                return;
            }

            _artists.Remove(card);
            _artistView.Remove(card);
            AfterRemoval();
        }

        private async void RemoveAlbumFavorite_Click(object sender, RoutedEventArgs e)
        {
            var card = (sender as FrameworkElement)?.Tag as AlbumCard;
            if (card == null || !await UnstarAsync(card.Id))
            {
                return;
            }

            _albums.Remove(card);
            _albumView.Remove(card);
            AfterRemoval();
        }

        /// <summary>
        /// Change-only-after-success, like every other favorite toggle: a
        /// failure leaves the item exactly where it was, and the menu is closed
        /// by then so nothing shown ever lied. Returns whether the server
        /// accepted, which is the callers' cue to drop the item.
        /// </summary>
        private static async Task<bool> UnstarAsync(string id)
        {
            var client = AppSession.Client;
            if (client == null)
            {
                return false;
            }

            try
            {
                await client.UnstarAsync(id);
                return true;
            }
            catch (SubsonicClientException)
            {
                Toast.Show("Couldn't remove that from your favorites.");
                return false;
            }
        }

        /// <summary>
        /// Removing the last item of a scope must fall back to the empty state,
        /// or the page shows a blank pane with focus nowhere; and the whole-tab
        /// actions must go dead when there is nothing left to play.
        /// </summary>
        private void AfterRemoval()
        {
            if (!HasPlayableScope())
            {
                ShowActiveTab(moveFocus: false);
            }
            else
            {
                // Not (false) — a Play All may be building right now, and this
                // must not re-enable its buttons under it.
                SetActionsBusy(_buildingPool);
            }
        }

        private void AlbumGoToArtist_Click(object sender, RoutedEventArgs e)
        {
            var card = (sender as FrameworkElement)?.Tag as AlbumCard;
            if (card != null && card.HasArtistId)
            {
                Frame.Navigate(typeof(ArtistDetailPage), card.ArtistId);
            }
        }

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

        // --- Mapping -------------------------------------------------------------

        private static ArtistCard ToArtistCard(Artist artist)
        {
            var artwork = artist.CoverArtId == null
                ? null
                : AppSession.Client.GetCoverArtUri(artist.CoverArtId, CoverArtSize);
            var label = artist.AlbumCount == 1 ? "1 album" : artist.AlbumCount + " albums";
            return new ArtistCard(artist.Id, artist.Name, label, artwork, artist.AlbumCount);
        }

        private static AlbumCard ToAlbumCard(Album album)
        {
            var artwork = album.CoverArtId == null
                ? null
                : AppSession.Client.GetCoverArtUri(album.CoverArtId, CoverArtSize);
            return new AlbumCard(
                album.Id, album.Name, album.Artist ?? string.Empty, artwork, album.Year, album.ArtistId);
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

        // --- View states ---------------------------------------------------------

        private void ShowLoading()
        {
            LoadingRing.IsActive = true;
            EmptyText.Visibility = Visibility.Collapsed;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            HideScopeViews();

            // Nothing is loaded yet, so there is nothing to play or sort.
            SetActionsBusy(false);
        }

        /// <summary>
        /// Reveals the active tab's list, or its empty state when that scope
        /// has no favorites. The empty message names the scope, so a user with
        /// favorite songs but no favorite artists is told which is which.
        /// </summary>
        private void ShowActiveTab(bool moveFocus)
        {
            LoadingRing.IsActive = false;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            HideScopeViews();

            UIElement view;
            Control tabButton;
            int count;
            string emptyMessage;
            switch (_tab)
            {
                case FavoritesTab.Albums:
                    view = AlbumsGrid;
                    tabButton = AlbumsTabButton;
                    count = _albumView.Count;
                    emptyMessage = "No favorite albums yet. Star an album to see it here.";
                    break;
                case FavoritesTab.Songs:
                    view = SongsPanel;
                    tabButton = SongsTabButton;
                    count = _songView.Count;
                    emptyMessage = "No favorite songs yet. Star a song to see it here.";
                    break;
                default:
                    view = ArtistsGrid;
                    tabButton = ArtistsTabButton;
                    count = _artistView.Count;
                    emptyMessage = "No favorite artists yet. Star an artist to see them here.";
                    break;
            }

            // Down from the right-hand action cluster always enters the visible
            // list: those buttons sit far right, where the XY engine may find
            // nothing below them.
            DependencyObject below = count > 0 ? (DependencyObject)view : null;
            SortButton.XYFocusDown = below;
            PlayAllButton.XYFocusDown = below;
            ShuffleAllButton.XYFocusDown = below;

            // Re-evaluate the whole-tab actions for the scope now showing.
            SetActionsBusy(_buildingPool);

            if (count == 0)
            {
                EmptyText.Text = emptyMessage;
                EmptyText.Visibility = Visibility.Visible;

                // Unlike the browse pages, an empty scope here does NOT leave
                // focus in the nav: the tab row is still interactive, and it is
                // the way out. This also catches the case where the list was
                // emptied by the last "Remove from favorites" — focus was on
                // the row that just vanished with its now-collapsed list.
                tabButton.Focus(FocusState.Keyboard);
                return;
            }

            EmptyText.Visibility = Visibility.Collapsed;
            view.Visibility = Visibility.Visible;

            if (moveFocus)
            {
                // Content owns focus (2026-07-14), and into the list rather
                // than the page: the page's first focusable is the tab row, and
                // landing there puts the first card an extra press away. Up
                // still reaches the tabs via XYFocusUp.
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
            LoadingRing.IsActive = false;
            EmptyText.Visibility = Visibility.Collapsed;
            HideScopeViews();
            ConnectErrorPanel.Visibility = Visibility.Visible;
            ConnectErrorText.Text = message;
            ConnectRetryButton.Visibility = retryable ? Visibility.Visible : Visibility.Collapsed;

            // Nothing loaded — the whole-tab actions have no pool to work from.
            SetActionsBusy(false);

            // Controller-first: the error state must own focus, or Right-from-nav
            // lands on nothing. An unsupported state has no Retry, so the tab
            // row is the nearest thing that can hold focus.
            if (retryable)
            {
                ConnectRetryButton.Focus(FocusState.Keyboard);
            }
            else
            {
                ArtistsTabButton.Focus(FocusState.Keyboard);
            }
        }

        private void HideScopeViews()
        {
            ArtistsGrid.Visibility = Visibility.Collapsed;
            AlbumsGrid.Visibility = Visibility.Collapsed;
            SongsPanel.Visibility = Visibility.Collapsed;
        }
    }
}
