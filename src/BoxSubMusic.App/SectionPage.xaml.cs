using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Placeholder content for shell sections until their slice lands. Takes
    /// the section tag as navigation parameter.
    /// </summary>
    public sealed partial class SectionPage : Page
    {
        public SectionPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var tag = e.Parameter as string ?? string.Empty;
            SectionTitle.Text = TitleFor(tag);
            SectionNote.Text = "This section arrives in a later slice.";
        }

        private static string TitleFor(string tag)
        {
            switch (tag)
            {
                case "search": return "Search";
                case "home": return "Home";
                case "artists": return "Artists";
                case "albums": return "Albums";
                case "songs": return "Songs";
                case "playlists": return "Playlists";
                default: return tag;
            }
        }
    }
}
