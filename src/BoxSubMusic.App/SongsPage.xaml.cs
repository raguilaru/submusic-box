using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Songs browse (Slice 08, requirements §7.3): list-only with title,
    /// artist, album, length, format and bitrate. Songs come from search3
    /// with an empty query — the OpenSubsonic full-library listing — in
    /// incrementally loaded pages (decisions 2026-07-13); servers that are
    /// not OpenSubsonic get an explicit unsupported state (§3), never a
    /// wrong empty list. The A/Enter primary action (immediate playback,
    /// queue replace) is deferred to Slice 10 and rows are inert with a
    /// visible note (§15); the Menu-button context menu's "Go to album" is
    /// live.
    /// </summary>
    public sealed partial class SongsPage : Page
    {
        private CancellationTokenSource _cts;
        private IncrementalSource<SongRow> _songs;

        public SongsPage()
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

            // A fresh source per (re)load: Retry after any failure starts
            // over from page one, so the collection can never hold a gap.
            var source = new IncrementalSource<SongRow>(FetchPageAsync);
            source.LoadFailed += Songs_LoadFailed;
            _songs = source;

            try
            {
                int loaded = await source.LoadFirstPageAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                SongsList.ItemsSource = source;
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

        private static async Task<IReadOnlyList<SongRow>> FetchPageAsync(
            int offset, int size, CancellationToken cancellationToken)
        {
            var client = AppSession.Client;
            if (client == null)
            {
                throw new SubsonicConnectionException("You are not signed in to a server.");
            }

            IReadOnlyList<Song> page = await client.GetSongsAsync(size, offset, cancellationToken);
            return page.Select(ToRow).ToList();
        }

        private static SongRow ToRow(Song song)
        {
            return new SongRow(
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

        private void Songs_LoadFailed(object sender, Exception ex)
        {
            // A later page failed mid-scroll. Surface the same page-level
            // error with Retry (which reloads from the top) rather than
            // failing silently (§3). Losing the scroll position is a known
            // v1 trade-off.
            if (!ReferenceEquals(sender, _songs))
            {
                return;
            }

            ShowConnectError("Couldn't load more songs. " + ex.Message);
        }

        // --- Context menu (decision 2026-07-13: secondary actions) -----------

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
        }

        private void ShowContent()
        {
            LoadingRing.IsActive = false;
            EmptyText.Visibility = Visibility.Collapsed;
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            SongsPanel.Visibility = Visibility.Visible;
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
