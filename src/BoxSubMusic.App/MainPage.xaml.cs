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
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (!e.Handled && ContentFrame.CanGoBack)
            {
                e.Handled = true;
                ContentFrame.GoBack();
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

        private void NavigateToSection(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tag == _currentSectionTag)
            {
                return;
            }

            _currentSectionTag = tag;
            switch (tag)
            {
                case "home":
                    ContentFrame.Navigate(typeof(HomePage));
                    break;
                case "artists":
                    ContentFrame.Navigate(typeof(ArtistsPage));
                    break;
                case "albums":
                    ContentFrame.Navigate(typeof(AlbumsPage));
                    break;
                case "songs":
                    ContentFrame.Navigate(typeof(SongsPage));
                    break;
                case "playlists":
                    ContentFrame.Navigate(typeof(PlaylistsPage));
                    break;
                case "settings":
                    ContentFrame.Navigate(typeof(SettingsPage));
                    break;
                case "account":
                    ContentFrame.Navigate(typeof(AccountPage));
                    break;
                default:
                    ContentFrame.Navigate(typeof(SectionPage), tag);
                    break;
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
