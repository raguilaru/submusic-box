using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoxSubMusic.App.Controls;
using BoxSubMusic.App.Models;
using BoxSubMusic.App.Services;
using BoxSubMusic.Core.Playback;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Home (Slice 05a, requirements §6.2–§6.4): Most Played / Recently
    /// Added / Random Picks album carousels. Connects the restored client on
    /// first use; a connection/auth failure is a page-level error with
    /// retry, per-carousel failures degrade independently. The §6.1 resume
    /// card is Slice 05b — it renders only when persisted playback context
    /// exists, which is impossible before the playback slices.
    /// </summary>
    public sealed partial class HomePage : Page
    {
        // §6.2/§6.3 cap; §6.4 follows for consistency.
        private const int MaxCarouselItems = 20;

        // Card is 220 epx; request double for 200%-scale displays.
        private const int CoverArtSize = 440;

        private CancellationTokenSource _cts;

        // §6.1: the loaded-but-not-yet-resumed state backing the card.
        private ResumeState _resumeState;

        public HomePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _cts = new CancellationTokenSource();
            // If playback starts while Home is on screen (e.g. resumed from
            // this very card), the card is stale — the bar shows the truth.
            PlaybackService.Instance.Changed += OnPlaybackChanged;
            _ = LoadAsync(_cts.Token);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            PlaybackService.Instance.Changed -= OnPlaybackChanged;
            _cts?.Cancel();
            _cts = null;
        }

        private void OnPlaybackChanged()
        {
            if (!PlaybackService.Instance.Queue.IsEmpty
                && ResumeSection.Visibility == Visibility.Visible)
            {
                HideResumeCard();
            }
        }

        private void HideResumeCard()
        {
            _resumeState = null;
            ResumeSection.Visibility = Visibility.Collapsed;
        }

        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            ConnectErrorPanel.Visibility = Visibility.Collapsed;
            CarouselsScroller.Visibility = Visibility.Visible;
            MostPlayedCarousel.ShowLoading();
            RecentlyAddedCarousel.ShowLoading();
            RandomPicksCarousel.ShowLoading();

            if (AppSession.Client == null)
            {
                // Defensive: the shell is only reachable with a stored login.
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

            await Task.WhenAll(
                LoadResumeCardAsync(cancellationToken),
                LoadCarouselAsync(MostPlayedCarousel, AlbumListType.Frequent, cancellationToken),
                LoadCarouselAsync(RecentlyAddedCarousel, AlbumListType.Newest, cancellationToken),
                LoadCarouselAsync(RandomPicksCarousel, AlbumListType.Random, cancellationToken));

            if (!cancellationToken.IsCancellationRequested)
            {
                // Focus convention: loaded content owns focus (see ContentFocus).
                ContentFocus.MoveInto(this);
            }
        }

        private async Task LoadCarouselAsync(
            AlbumCarousel carousel,
            AlbumListType type,
            CancellationToken cancellationToken)
        {
            carousel.ShowLoading();
            try
            {
                IReadOnlyList<Album> albums = await AppSession.Client.GetAlbumList2Async(
                    type, MaxCarouselItems, 0, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    carousel.ShowAlbums(albums.Select(ToCard).ToList());
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
                    carousel.ShowError(ex.Message);
                }
            }
        }

        private async Task LoadResumeCardAsync(CancellationToken cancellationToken)
        {
            HideResumeCard();

            // An active queue means the bar already shows this session's
            // truth; the card would just duplicate (or contradict) it.
            if (!PlaybackService.Instance.Queue.IsEmpty)
            {
                return;
            }

            ResumeState state = await ResumeStore.LoadForActiveServerAsync();
            if (state == null
                || cancellationToken.IsCancellationRequested
                || !PlaybackService.Instance.Queue.IsEmpty)
            {
                return;
            }

            _resumeState = state;

            Song current = state.PlayOrderIndices == null
                ? state.Tracks[state.CurrentIndex]
                : state.Tracks[state.PlayOrderIndices[state.CurrentIndex]];
            ResumeName.Text = state.Context?.DisplayName ?? current.Title;
            ResumeTrack.Text = string.IsNullOrEmpty(current.Artist)
                ? current.Title
                : current.Title + " · " + current.Artist;

            string coverArtId = state.Context?.CoverArtId ?? current.CoverArtId;
            ResumeArtImage.Source = coverArtId == null
                ? null
                : new BitmapImage
                {
                    UriSource = AppSession.Client.GetCoverArtUri(coverArtId, 320),
                    DecodePixelWidth = 160, // matches the 160-epx card box (1:1 at 1080p)
                };

            ResumeSection.Visibility = Visibility.Visible;
        }

        private void ResumeCard_Click(object sender, RoutedEventArgs e)
        {
            ResumeState state = _resumeState;
            if (state == null)
            {
                return;
            }

            // Resume raises Changed → OnPlaybackChanged hides the card; the
            // collapse would strand focus, so move it into the carousels.
            PlaybackService.Instance.Resume(state);
            ContentFocus.MoveInto(this);
        }

        private static AlbumCard ToCard(Album album)
        {
            var artwork = album.CoverArtId == null
                ? null
                : AppSession.Client.GetCoverArtUri(album.CoverArtId, CoverArtSize);
            return new AlbumCard(
                album.Id,
                album.Name,
                string.IsNullOrEmpty(album.Artist) ? "Unknown artist" : album.Artist,
                artwork);
        }

        private void ShowConnectError(string message)
        {
            CarouselsScroller.Visibility = Visibility.Collapsed;
            ConnectErrorPanel.Visibility = Visibility.Visible;
            ConnectErrorText.Text = message;

            // Controller-first: the error state must own focus, or the page
            // has nothing focusable and Right-from-nav goes nowhere.
            ConnectRetryButton.Focus(FocusState.Keyboard);
        }

        private void ConnectRetryButton_Click(object sender, RoutedEventArgs e)
        {
            var cts = _cts;
            if (cts != null)
            {
                _ = LoadAsync(cts.Token);
            }
        }

        private void MostPlayedCarousel_RetryRequested(object sender, RoutedEventArgs e)
        {
            RetryCarousel(MostPlayedCarousel, AlbumListType.Frequent);
        }

        private void RecentlyAddedCarousel_RetryRequested(object sender, RoutedEventArgs e)
        {
            RetryCarousel(RecentlyAddedCarousel, AlbumListType.Newest);
        }

        private void RandomPicksCarousel_RetryRequested(object sender, RoutedEventArgs e)
        {
            RetryCarousel(RandomPicksCarousel, AlbumListType.Random);
        }

        private void RetryCarousel(AlbumCarousel carousel, AlbumListType type)
        {
            var cts = _cts;
            if (cts != null)
            {
                _ = LoadCarouselAsync(carousel, type, cts.Token);
            }
        }

        private void Carousel_AlbumClicked(object sender, AlbumCard card)
        {
            // Slice 07a: carousel cards open the album detail view.
            Frame.Navigate(typeof(AlbumDetailPage), card.Id);
        }
    }
}
