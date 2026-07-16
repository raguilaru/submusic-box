using System;
using BoxSubMusic.App.Services;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Application shell (Slice 04, requirements §5): persistent collapsible
    /// left navigation hosting the content frame. Search and Home are always
    /// visible; Artists/Albums/Playlists visibility follows
    /// NavigationPreferences (toggles live on the Settings page).
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private string _currentSectionTag;

        public MainPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyNavVisibility();
            NavigationPreferences.Changed += ApplyNavVisibility;

            // B/back walks the content frame's history (Slice 07a: detail
            // pages made the hierarchy deep enough to need it). The outer
            // frame's stack was cleared after setup, so B can never fall
            // through to the setup page.
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            ContentFrame.Navigated += ContentFrame_Navigated;

            // Slice 11 (§10): the bar's queue button and the global Y both
            // open the expanded queue overlay.
            NowPlaying.QueueExpandRequested += OpenQueueOverlay;
            QueueOverlay.Closed += OnQueueOverlayClosed;

            // Controller-first rule: something must own focus from the first
            // frame, or directional input has no starting point.
            HomeNavItem.Focus(FocusState.Keyboard);
            if (ContentFrame.Content == null)
            {
                NavView.SelectedItem = HomeNavItem;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            NavigationPreferences.Changed -= ApplyNavVisibility;
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            ContentFrame.Navigated -= ContentFrame_Navigated;
            NowPlaying.QueueExpandRequested -= OpenQueueOverlay;
            QueueOverlay.Closed -= OnQueueOverlayClosed;
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            // B closes the expanded queue before anything else (§10): while
            // the overlay is open it owns the screen.
            if (!e.Handled && QueueOverlay.IsOpen)
            {
                e.Handled = true;
                QueueOverlay.Close();
                return;
            }

            if (!e.Handled && ContentFrame.CanGoBack)
            {
                e.Handled = true;
                ContentFrame.GoBack();
            }
        }

        private void OpenQueueOverlay()
        {
            if (QueueOverlay.IsOpen || PlaybackService.Instance.Queue.IsEmpty)
            {
                return;
            }

            QueueOverlay.Open();

            // Focus trap: with the whole shell disabled underneath, XY focus
            // cannot leave the overlay (the PaneFooter lesson: never rely on
            // the engine skipping what merely *looks* unreachable).
            NavView.IsEnabled = false;
        }

        private void OnQueueOverlayClosed()
        {
            NavView.IsEnabled = true;

            // Return focus where the user came from: the bar's queue button
            // (also right for a Y-press — the bar is always visible while a
            // queue exists). If the queue died while the overlay was open
            // (playback failure stop), the bar is hidden — content instead.
            if (PlaybackService.Instance.Queue.IsEmpty)
            {
                ContentFocus.MoveInto(ContentFrame);
            }
            else
            {
                NowPlaying.FocusQueueButton();
            }
        }

        private void ShellRoot_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            // §10: Y is the queue expand/collapse toggle, app-wide while a
            // queue exists (decision 2026-07-16). Escape is the keyboard
            // fallback for closing (B has no keyboard equivalent wired).
            // Key-repeat is ignored — a held Y must not flap the overlay.
            if (e.Key == VirtualKey.GamepadY && !e.KeyStatus.WasKeyDown)
            {
                if (QueueOverlay.IsOpen)
                {
                    QueueOverlay.Close();
                }
                else
                {
                    OpenQueueOverlay();
                }

                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Escape && QueueOverlay.IsOpen)
            {
                QueueOverlay.Close();
                e.Handled = true;
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            // Back navigation can change the section underneath the pane, so
            // resync the highlighted nav item. Detail pages return null and
            // keep their originating section highlighted.
            string tag = SectionTagFor(e);
            if (tag == null || tag == _currentSectionTag)
            {
                return;
            }

            _currentSectionTag = tag;
            SyncNavSelection(tag);
        }

        private static string SectionTagFor(NavigationEventArgs e)
        {
            if (e.Content is HomePage) return "home";
            if (e.Content is ArtistsPage) return "artists";
            if (e.Content is AlbumsPage) return "albums";
            if (e.Content is SongsPage) return "songs";
            if (e.Content is SettingsPage) return "settings";
            if (e.Content is AccountPage) return "account";
            if (e.Content is SectionPage) return e.Parameter as string;
            return null;
        }

        private void SyncNavSelection(string tag)
        {
            foreach (var candidate in NavView.MenuItems)
            {
                if (candidate is NavigationViewItem item && (item.Tag as string) == tag)
                {
                    // _currentSectionTag is already up to date, so the
                    // SelectionChanged this raises is a guarded no-op.
                    NavView.SelectedItem = item;
                    return;
                }
            }
        }

        private void ApplyNavVisibility()
        {
            ArtistsNavItem.Visibility = ToVisibility(NavigationPreferences.ShowArtists);
            AlbumsNavItem.Visibility = ToVisibility(NavigationPreferences.ShowAlbums);
            PlaylistsNavItem.Visibility = ToVisibility(NavigationPreferences.ShowPlaylists);
        }

        private static Visibility ToVisibility(bool visible)
        {
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var item = args.SelectedItem as NavigationViewItem;
            if (item == null)
            {
                return;
            }

            NavigateToSection(item.Tag as string);
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            // Invoking the already selected section while a detail page is
            // shown must return to the section's list (console checkpoint #4
            // feedback 2026-07-15). SelectionChanged never fires in that
            // case — the item is already selected — so it lands here.
            string tag = (args.InvokedItemContainer as NavigationViewItem)?.Tag as string;
            if (tag == null || tag != _currentSectionTag)
            {
                return;
            }

            if (ContentFrame.Content?.GetType() != PageTypeFor(tag))
            {
                NavigateToSectionCore(tag);
            }
        }

        private void NavigateToSection(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tag == _currentSectionTag)
            {
                return;
            }

            _currentSectionTag = tag;
            NavigateToSectionCore(tag);
        }

        private void NavigateToSectionCore(string tag)
        {
            Type pageType = PageTypeFor(tag);
            ContentFrame.Navigate(pageType, pageType == typeof(SectionPage) ? tag : null);
        }

        private static Type PageTypeFor(string tag)
        {
            switch (tag)
            {
                case "home": return typeof(HomePage);
                case "artists": return typeof(ArtistsPage);
                case "albums": return typeof(AlbumsPage);
                case "songs": return typeof(SongsPage);
                case "playlists": return typeof(PlaylistsPage);
                case "settings": return typeof(SettingsPage);
                case "account": return typeof(AccountPage);
                default: return typeof(SectionPage);
            }
        }

        private void NowPlaying_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Up from the now-playing bar must land in the page content
            // above it, not in the nav pane: checkpoint #4 showed the XY
            // engine preferring the pane's bottom items (they sit closer to
            // the bar's vertical band than most page content). Constrain the
            // search to the content frame; when the page has nothing
            // focusable, fall through to the engine's default.
            if (e.Handled)
            {
                return;
            }

            if (e.Key == VirtualKey.GamepadDPadUp
                || e.Key == VirtualKey.GamepadLeftThumbstickUp
                || e.Key == VirtualKey.Up)
            {
                var options = new FindNextElementOptions { SearchRoot = ContentFrame };
                if (FocusManager.FindNextElement(FocusNavigationDirection.Up, options) is Control target)
                {
                    target.Focus(FocusState.Keyboard);
                    e.Handled = true;
                }
            }
        }

        private void NavView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Keyboard fallback (req §13.1), carried over from Slice 01: the
            // nav pane's list keeps arrow keys to itself, so Right would never
            // leave it. Mirror the D-pad by handing focus to the content.
            if (e.Key == VirtualKey.Right
                && !e.Handled
                && FocusManager.GetFocusedElement() is NavigationViewItem)
            {
                e.Handled = FocusManager.TryMoveFocus(FocusNavigationDirection.Right);
            }
            else if (e.Key == VirtualKey.Left
                && !e.Handled
                && FocusManager.GetFocusedElement() is Control focused
                && !(focused is NavigationViewItem))
            {
                // Left at the content's edge: return focus to the nav pane.
                if (!FocusManager.TryMoveFocus(FocusNavigationDirection.Left))
                {
                    var target = (NavView.SelectedItem as NavigationViewItem) ?? HomeNavItem;
                    target.Focus(FocusState.Keyboard);
                }
                e.Handled = true;
            }
        }
    }
}
