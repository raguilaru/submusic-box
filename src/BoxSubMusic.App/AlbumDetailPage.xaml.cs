using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BoxSubMusic.App.Models;
using BoxSubMusic.App.Services;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Album detail (Slice 07a, requirements §9): mirrors the artist detail
    /// layout — large artwork, title, artist, year, favorite heart persisted
    /// through star/unstar, song count + total duration, and the track list in
    /// disc/track order. Play/Shuffle and track-row playback are deferred to
    /// Slice 07b (queue + now-playing bar) and rendered disabled/inert.
    /// Connects the restored client on first use; load failure is a retryable
    /// error state (§3).
    /// </summary>
    public sealed partial class AlbumDetailPage : Page
    {
        // Detail artwork is 320 epx; request double for 200%-scale displays.
        private const int HeaderArtSize = 640;

        private CancellationTokenSource _cts;
        private string _albumId;

        // Guards the favorite handler while we set IsChecked from loaded state,
        // so restoring the toggle never fires a network call.
        private bool _suppressFavoriteEvents;

        private readonly Brush _favoriteActiveBrush;

        public AlbumDetailPage()
        {
            InitializeComponent();
            _favoriteActiveBrush = ResolveAccentBrush();
        }

        // The starred heart is tinted with the system accent (the Regular
        // Fluent font has no filled heart). Resolve defensively so a missing
        // resource can never break page construction.
        private static Brush ResolveAccentBrush()
        {
            try
            {
                if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var value)
                    && value is Color color)
                {
                    return new SolidColorBrush(color);
                }
            }
            catch (Exception)
            {
                // fall through to the accent-brush fallback below
            }

            return Application.Current.Resources["SystemControlForegroundAccentBrush"] as Brush
                ?? new SolidColorBrush(Colors.DodgerBlue);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _albumId = e.Parameter as string;
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

            if (string.IsNullOrEmpty(_albumId))
            {
                ShowError("No album was selected.");
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
                AlbumDetail detail = await AppSession.Client.GetAlbumAsync(_albumId, cancellationToken);
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

        private void Populate(AlbumDetail detail)
        {
            Album album = detail.Album;
            AlbumNameText.Text = album.Name;
            AlbumArtistText.Text = album.Artist ?? string.Empty;
            MetaText.Text = FormatMeta(album, detail);

            ArtworkImage.Source = album.CoverArtId == null
                ? null
                : new BitmapImage
                {
                    UriSource = AppSession.Client.GetCoverArtUri(album.CoverArtId, HeaderArtSize),
                    DecodePixelWidth = 640,
                };

            // Restore the favorite toggle without triggering a star/unstar call.
            _suppressFavoriteEvents = true;
            FavoriteToggle.IsChecked = album.IsStarred;
            _suppressFavoriteEvents = false;
            UpdateFavoriteVisual(album.IsStarred);
            FavoriteStatusText.Visibility = Visibility.Collapsed;

            var rows = detail.Songs.Select(s => ToRow(s, album.Artist)).ToList();
            TracksList.ItemsSource = rows;
            TracksList.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            NoTracksText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static TrackRow ToRow(Song song, string albumArtist)
        {
            // Credit the track artist only when it differs from the album's,
            // so compilations stay honest and regular albums stay clean.
            string artist = song.Artist != null && song.Artist != albumArtist
                ? song.Artist
                : string.Empty;
            return new TrackRow(
                song.Id,
                song.Track?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                song.Title,
                artist,
                FormatTrackDuration(song.Duration),
                song.ArtistId);
        }

        // --- Context menu (console feedback 2026-07-14: same secondary-action
        // menu as the Songs page; "Go to artist" instead of the circular
        // "Go to album") --------------------------------------------------------

        private void TracksList_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            // The gamepad Menu button raises ContextRequested on the focused
            // ListViewItem, but the flyout sits on the item's child grid and
            // routed events bubble up, never down — redirect, exactly as on
            // SongsPage (that redirect is console-verified).
            var container = args.OriginalSource as SelectorItem;
            var root = container?.ContentTemplateRoot as FrameworkElement;
            if (root?.ContextFlyout != null)
            {
                root.ContextFlyout.ShowAt(root);
                args.Handled = true;
            }
        }

        private void GoToArtist_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.Tag as TrackRow;
            if (row != null && row.HasArtistId)
            {
                Frame.Navigate(typeof(ArtistDetailPage), row.ArtistId);
            }
        }

        private static string FormatTrackDuration(TimeSpan duration)
        {
            return duration.TotalHours >= 1
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:mm\\:ss}", (int)duration.TotalHours, duration)
                : duration.ToString("m\\:ss", CultureInfo.InvariantCulture);
        }

        private static string FormatMeta(Album album, AlbumDetail detail)
        {
            var parts = new StringBuilder();
            if (album.Year.HasValue)
            {
                parts.Append(album.Year.Value).Append("  ·  ");
            }

            // The songs actually returned are authoritative for the §9 counts;
            // fall back to the album attribute when the list is empty.
            int songCount = detail.Songs.Count > 0 ? detail.Songs.Count : album.SongCount;
            parts.Append(songCount == 1 ? "1 song" : songCount + " songs");

            TimeSpan total = detail.Songs.Count > 0
                ? TimeSpan.FromSeconds(detail.Songs.Sum(s => s.Duration.TotalSeconds))
                : album.Duration;
            parts.Append("  ·  ").Append(FormatTotalDuration(total));
            return parts.ToString();
        }

        private static string FormatTotalDuration(TimeSpan duration)
        {
            int hours = (int)duration.TotalHours;
            if (hours >= 1)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} hr {1} min", hours, duration.Minutes);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0} min", Math.Max(1, duration.Minutes));
        }

        // --- Favorite heart (§9) --------------------------------------------

        private async void FavoriteToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressFavoriteEvents)
            {
                return;
            }

            bool wantStarred = FavoriteToggle.IsChecked == true;
            UpdateFavoriteVisual(wantStarred);
            FavoriteStatusText.Visibility = Visibility.Collapsed;

            var client = AppSession.Client;
            if (client == null || string.IsNullOrEmpty(_albumId))
            {
                return;
            }

            FavoriteToggle.IsEnabled = false;
            try
            {
                if (wantStarred)
                {
                    await client.StarAsync(_albumId);
                }
                else
                {
                    await client.UnstarAsync(_albumId);
                }
            }
            catch (SubsonicClientException)
            {
                // Persist failed: revert the toggle and say so, so the shown
                // state never lies about what the server holds (§9).
                _suppressFavoriteEvents = true;
                FavoriteToggle.IsChecked = !wantStarred;
                _suppressFavoriteEvents = false;
                UpdateFavoriteVisual(!wantStarred);
                FavoriteStatusText.Text = "Couldn't update favorite. Try again.";
                FavoriteStatusText.Visibility = Visibility.Visible;
            }
            finally
            {
                FavoriteToggle.IsEnabled = true;
            }
        }

        private void UpdateFavoriteVisual(bool starred)
        {
            FavoriteIcon.Foreground = starred ? _favoriteActiveBrush : null;
        }

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
