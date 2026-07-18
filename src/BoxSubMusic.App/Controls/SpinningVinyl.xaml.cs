using BoxSubMusic.App.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace BoxSubMusic.App.Controls
{
    /// <summary>
    /// Current-track marker: a small vinyl disc that spins while playback is
    /// running and freezes when paused/stopped (retro theme, 2026-07-19).
    /// Subscribes to <see cref="PlaybackService.Changed"/> only while loaded
    /// (rows recycle; the service is app-lifetime, so a lingering handler
    /// would leak the row). The storyboard only runs while this instance is
    /// actually visible — song lists hold one collapsed marker per row.
    /// </summary>
    public sealed partial class SpinningVinyl : UserControl
    {
        // Begin() has been called at least once; Pause/Resume are only valid
        // on a started storyboard.
        private bool _started;

        public SpinningVinyl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            RegisterPropertyChangedCallback(VisibilityProperty, OnVisibilityChanged);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PlaybackService.Instance.Changed += OnPlaybackChanged;
            Sync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            PlaybackService.Instance.Changed -= OnPlaybackChanged;
            if (_started)
            {
                SpinStoryboard.Stop();
                _started = false;
            }
        }

        private void OnVisibilityChanged(DependencyObject sender, DependencyProperty dp)
        {
            Sync();
        }

        private void OnPlaybackChanged()
        {
            Sync();
        }

        private void Sync()
        {
            bool spin = Visibility == Visibility.Visible && PlaybackService.Instance.IsPlaying;
            if (spin)
            {
                if (_started)
                {
                    SpinStoryboard.Resume();
                }
                else
                {
                    SpinStoryboard.Begin();
                    _started = true;
                }
            }
            else if (_started)
            {
                // Freeze in place — a stopped record, not a reset one.
                SpinStoryboard.Pause();
            }
        }
    }
}
