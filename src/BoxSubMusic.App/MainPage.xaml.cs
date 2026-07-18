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
        // Pane toggle glyphs (Fluent panel_left_contract / _expand,
        // cmap-verified 2026-07-19 — §13.3).
        private const string PaneContractGlyph = "\uE8B6";
        private const string PaneExpandGlyph = "\uE8BA";

        // The template's icon Viewbox is enlarged from code (see
        // EnlargeNavIcons); retries cover containers not yet templated.
        private const int NavIconAttempts = 5;

        private string _currentSectionTag;

        // Queue-row "Go to album/artist" (Slice 14): the navigation waits for
        // the overlay's closing slide, so the page never loads under a
        // disabled shell (ContentFocus would fail on a disabled tree).
        private Type _pendingOverlayNavPage;
        private object _pendingOverlayNavParameter;

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
            QueueOverlay.AlbumRequested += OnOverlayAlbumRequested;
            QueueOverlay.ArtistRequested += OnOverlayArtistRequested;

            // Slice 14: the shell hosts the one app-wide toast surface.
            Toast.Register(Dispatcher, Toasts.Show);

            // Controller-first rule: something must own focus from the first
            // frame, or directional input has no starting point.
            HomeNavItem.Focus(FocusState.Keyboard);
            if (ContentFrame.Content == null)
            {
                NavView.SelectedItem = HomeNavItem;
            }

            EnlargeNavIcons(NavIconAttempts);
        }

        /// <summary>
        /// 10-ft nav icons (root cause found 2026-07-19 after two failed
        /// attempts): the OS left-pane item template renders the icon inside
        /// a Viewbox named "IconBox" — so FontIcon.FontSize is a no-op (the
        /// Viewbox rescales) — whose box is 40 epx minus the margin resource
        /// NavigationViewItemIconBoxMargin (10,12,16,12 → a ~16-epx icon).
        /// That Thickness sits at generic.xaml's GLOBAL scope, and resource
        /// references inside a system template resolve at the template's own
        /// parse scope first, so an App.xaml override is never consulted (on
        /// any device). The one reliable fix short of retemplating: size the
        /// named Viewbox directly once the template has loaded — 28 epx,
        /// pairing with the 20-epx text. Alignment (UI feedback 2026-07-19:
        /// icons sat high next to the labels and off-center in the compact
        /// strip): the box CENTERS in its template zone both ways instead of
        /// carrying the old top-left-biased fixed margin, so it tracks the
        /// row's 56-epx height and the zone's actual position in either pane
        /// state.
        /// </summary>
        private void EnlargeNavIcons(int attemptsLeft)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                bool anyMissing = false;
                foreach (object entry in NavView.MenuItems)
                {
                    // Collapsed items (nav-visibility toggles) have no
                    // template yet; ApplyNavVisibility re-runs this when
                    // they appear.
                    var item = entry as NavigationViewItem;
                    if (item == null || item.Visibility == Visibility.Collapsed)
                    {
                        continue;
                    }

                    var iconBox = FindDescendant<Viewbox>(item, "IconBox");
                    if (iconBox == null)
                    {
                        anyMissing = true;
                        continue;
                    }

                    iconBox.Width = 28;
                    iconBox.Height = 28;
                    iconBox.Margin = new Thickness(0);
                    iconBox.HorizontalAlignment = HorizontalAlignment.Center;
                    iconBox.VerticalAlignment = VerticalAlignment.Center;
                }

                if (anyMissing && attemptsLeft > 1)
                {
                    EnlargeNavIcons(attemptsLeft - 1);
                }
            });
        }

        private static T FindDescendant<T>(DependencyObject root, string name)
            where T : FrameworkElement
        {
            int count = Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
                if (child is T match && match.Name == name)
                {
                    return match;
                }

                T nested = FindDescendant<T>(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            NavigationPreferences.Changed -= ApplyNavVisibility;
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            ContentFrame.Navigated -= ContentFrame_Navigated;
            NowPlaying.QueueExpandRequested -= OpenQueueOverlay;
            QueueOverlay.Closed -= OnQueueOverlayClosed;
            QueueOverlay.AlbumRequested -= OnOverlayAlbumRequested;
            QueueOverlay.ArtistRequested -= OnOverlayArtistRequested;
            Toast.Unregister();
        }

        private void OnOverlayAlbumRequested(string albumId)
        {
            NavigateFromOverlay(typeof(AlbumDetailPage), albumId);
        }

        private void OnOverlayArtistRequested(string artistId)
        {
            NavigateFromOverlay(typeof(ArtistDetailPage), artistId);
        }

        private void NavigateFromOverlay(Type pageType, object parameter)
        {
            _pendingOverlayNavPage = pageType;
            _pendingOverlayNavParameter = parameter;
            QueueOverlay.Close();
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

            // Queue-row "Go to album/artist": navigate now that the shell is
            // enabled again; the detail page's ContentFocus owns focus (the
            // usual content-owns-focus convention), so the queue-button
            // restore below must not fight it.
            if (_pendingOverlayNavPage != null)
            {
                Type pageType = _pendingOverlayNavPage;
                object parameter = _pendingOverlayNavParameter;
                _pendingOverlayNavPage = null;
                _pendingOverlayNavParameter = null;
                ContentFrame.Navigate(pageType, parameter);
                return;
            }

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
            else if (e.Key == VirtualKey.GamepadLeftShoulder
                && !e.KeyStatus.WasKeyDown
                && !QueueOverlay.IsOpen)
            {
                // Region cycling (checkpoint #7 request, decision 2026-07-18,
                // evolving the verified checkpoint-#6 quick nav): LB/RB move
                // between the shell's three regions — nav pane, page content,
                // now-playing bar (skipped while hidden). From content, LB is
                // still "go to nav" and RB reaches the bar. Inert while the
                // overlay traps focus.
                CycleRegion(forward: false);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.GamepadRightShoulder
                && !e.KeyStatus.WasKeyDown
                && !QueueOverlay.IsOpen)
            {
                CycleRegion(forward: true);
                e.Handled = true;
            }
        }

        // --- LB/RB region cycling (2026-07-18) --------------------------------

        private enum ShellRegion
        {
            Nav = 0,
            Content = 1,
            Bar = 2,
        }

        private void CycleRegion(bool forward)
        {
            int current = (int)CurrentRegion();
            for (int step = 1; step <= 2; step++)
            {
                int next = forward ? (current + step) % 3 : (current + 3 - step) % 3;
                if (next == (int)ShellRegion.Bar && !NowPlaying.IsBarVisible)
                {
                    continue;
                }

                if (FocusRegion((ShellRegion)next))
                {
                    return;
                }
            }
        }

        private ShellRegion CurrentRegion()
        {
            // Walk the visual ancestry of the focused element: inside the
            // bar / the content frame / anything else (the nav pane). A null
            // chain (popup) reads as Nav — harmless, the cycle still lands
            // somewhere sensible.
            var node = FocusManager.GetFocusedElement() as DependencyObject;
            while (node != null)
            {
                if (ReferenceEquals(node, NowPlaying))
                {
                    return ShellRegion.Bar;
                }

                if (ReferenceEquals(node, ContentFrame))
                {
                    return ShellRegion.Content;
                }

                node = Windows.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
            }

            return ShellRegion.Nav;
        }

        private bool FocusRegion(ShellRegion region)
        {
            switch (region)
            {
                case ShellRegion.Nav:
                    // A hidden selected section (nav-visibility toggle) falls
                    // back to Home.
                    var target = NavView.SelectedItem as NavigationViewItem;
                    if (target == null || target.Visibility == Visibility.Collapsed)
                    {
                        target = HomeNavItem;
                    }

                    return target.Focus(FocusState.Keyboard);

                case ShellRegion.Content:
                    // Deferred-focus helper; a page with nothing focusable
                    // leaves focus where it was.
                    ContentFocus.MoveInto(ContentFrame);
                    return true;

                case ShellRegion.Bar:
                    return NowPlaying.FocusPrimary();

                default:
                    return false;
            }
        }

        private void NavView_PaneOpening(NavigationView sender, object args)
        {
            NavHintPanel.Visibility = Visibility.Visible;
            PaneBackdrop.Width = NavView.OpenPaneLength;
            PaneToggleIcon.Glyph = PaneContractGlyph;
        }

        private void NavView_PaneClosing(NavigationView sender, NavigationViewPaneClosingEventArgs args)
        {
            // The compact pane is 48 epx wide; the hint text would clip into
            // an unreadable sliver, so it hides with the pane. The rounded
            // backdrop and the toggle glyph follow the pane state too.
            NavHintPanel.Visibility = Visibility.Collapsed;
            PaneBackdrop.Width = NavView.CompactPaneLength;
            PaneToggleIcon.Glyph = PaneExpandGlyph;
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
            if (e.Content is SearchPage) return "search";
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

            // A just-revealed item realizes its template now — give its icon
            // the same treatment (idempotent for the others).
            EnlargeNavIcons(NavIconAttempts);
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
            // The pane toggle lives in the menu list (SelectsOnInvoked=False,
            // 2026-07-19): invoking it flips the pane instead of navigating.
            if (ReferenceEquals(args.InvokedItemContainer, PaneToggleItem))
            {
                NavView.IsPaneOpen = !NavView.IsPaneOpen;
                return;
            }

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
                case "search": return typeof(SearchPage);
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
