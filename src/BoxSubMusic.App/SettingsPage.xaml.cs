using BoxSubMusic.App.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Settings: the three §5 nav-visibility toggles (Slice 04) and the §12
    /// theme selector (Slice 12). Search and Home intentionally have no
    /// toggle.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        private bool _loadingState;

        public SettingsPage()
        {
            InitializeComponent();
            _loadingState = true;
            ArtistsToggle.IsOn = NavigationPreferences.ShowArtists;
            AlbumsToggle.IsOn = NavigationPreferences.ShowAlbums;
            PlaylistsToggle.IsOn = NavigationPreferences.ShowPlaylists;
            switch (ThemeService.Mode)
            {
                case ThemeMode.Dark:
                    ThemeDark.IsChecked = true;
                    break;
                case ThemeMode.Light:
                    ThemeLight.IsChecked = true;
                    break;
                default:
                    ThemeAutomatic.IsChecked = true;
                    break;
            }
            _loadingState = false;

            // Focus convention: content owns focus on arrival (see ContentFocus).
            Loaded += (s, e) => ContentFocus.MoveInto(this);
        }

        private void Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loadingState)
            {
                return;
            }

            if (ReferenceEquals(sender, ArtistsToggle))
            {
                NavigationPreferences.ShowArtists = ArtistsToggle.IsOn;
            }
            else if (ReferenceEquals(sender, AlbumsToggle))
            {
                NavigationPreferences.ShowAlbums = AlbumsToggle.IsOn;
            }
            else if (ReferenceEquals(sender, PlaylistsToggle))
            {
                NavigationPreferences.ShowPlaylists = PlaylistsToggle.IsOn;
            }
        }

        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            if (_loadingState)
            {
                return;
            }

            ThemeService.Mode =
                ReferenceEquals(sender, ThemeDark) ? ThemeMode.Dark
                : ReferenceEquals(sender, ThemeLight) ? ThemeMode.Light
                : ThemeMode.Automatic;
        }
    }
}
