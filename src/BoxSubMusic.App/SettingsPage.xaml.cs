using BoxSubMusic.App.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Settings (Slice 04 subset): the three §5 nav-visibility toggles.
    /// Search and Home intentionally have no toggle. Theme selection is
    /// Slice 12.
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
            _loadingState = false;
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
    }
}
