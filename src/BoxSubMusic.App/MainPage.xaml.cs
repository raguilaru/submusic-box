using System;
using BoxSubMusic.App.Services;
using Windows.Media.Playback;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

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

            // Spike diagnostics: surface playback failures/state in the UI so
            // VM testing doesn't need a debugger attached.
            MediaPlayer player = PlaybackService.Instance.Player;
            player.MediaFailed += async (s, args) =>
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    ActivationLog.Text = $"MEDIA FAILED: {args.Error} — {args.ErrorMessage}");
            player.PlaybackSession.PlaybackStateChanged += async (s, args) =>
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    SectionTitle.Text = $"Home — {s.PlaybackState}");
        }

        private void NavView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Keyboard fallback (req §13.1): the nav pane's list keeps arrow
            // keys to itself, so Right would never leave it. Mirror the D-pad:
            // hand focus across to the content region. Left back into the pane
            // is covered by XY focus on the content grid.
            if (e.Key == VirtualKey.Right
                && !e.Handled
                && FocusManager.GetFocusedElement() is NavigationViewItem)
            {
                e.Handled = FocusManager.TryMoveFocus(FocusNavigationDirection.Right);
            }
            else if (e.Key == VirtualKey.Left
                && !e.Handled
                && FocusManager.GetFocusedElement() is Control focused
                && focused is not NavigationViewItem)
            {
                // Left at the content's edge: the XY region boundary stops the
                // spatial search, so return focus to the current nav item.
                if (!FocusManager.TryMoveFocus(FocusNavigationDirection.Left))
                {
                    (NavView.SelectedItem as NavigationViewItem)?.Focus(FocusState.Keyboard);
                }
                e.Handled = true;
            }
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

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            PlaybackService.Instance.TogglePlayPause();
            bool playing = PlaybackService.Instance.IsPlaying;
            PlayButton.Content = playing ? "  Pause" : "  Play";
            ActivationLog.Text = playing
                ? "Playing test tone (looping). Minimize the app: audio must continue and the system media flyout must show BoxSubMusic."
                : "Paused.";
        }
    }
}
