using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Slice 01 spike shell: exercises controller focus navigation across the
    /// nav pane and content area. Real navigation/content hosting is Slice 04.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Controller-first rule: something must own focus from the first
            // frame, or directional input has no starting point (no mouse on
            // Xbox to bail us out). FocusState.Keyboard forces the focus
            // visual to render immediately.
            HomeNavItem.Focus(FocusState.Keyboard);
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                SectionTitle.Text = "Settings";
            }
            else if (args.SelectedItem is NavigationViewItem item)
            {
                SectionTitle.Text = item.Content?.ToString() ?? string.Empty;
            }
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                ActivationLog.Text = $"Activated: {button.Content} at {DateTime.Now:HH:mm:ss}";
            }
        }
    }
}
