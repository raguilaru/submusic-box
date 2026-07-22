using BoxSubMusic.App.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace BoxSubMusic.App.Controls
{
    /// <summary>
    /// Now-playing-bar artwork drawn as a vinyl record whose centre label is
    /// the album art (retro theme refinement, 2026-07-22). The disc spins while
    /// playback is running and freezes in place when paused/stopped — the same
    /// gating as <see cref="SpinningVinyl"/>: it subscribes to
    /// <see cref="PlaybackService.Changed"/> only while loaded and runs the
    /// storyboard only while actually visible (so a collapsed bar spins
    /// nothing).
    /// </summary>
    public sealed partial class SpinningAlbumArt : UserControl
    {
        // Begin() has been called at least once; Pause/Resume are only valid
        // on a started storyboard.
        private bool _started;

        /// <summary>
        /// The record label's album art. Null (or a not-yet-decoded image)
        /// shows the blank teal label instead.
        /// </summary>
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(
                nameof(Source),
                typeof(ImageSource),
                typeof(SpinningAlbumArt),
                new PropertyMetadata(null, OnSourceChanged));

        public ImageSource Source
        {
            get => (ImageSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public SpinningAlbumArt()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            RegisterPropertyChangedCallback(VisibilityProperty, OnVisibilityChanged);
        }

        private static void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var control = (SpinningAlbumArt)sender;
            control.ArtBrush.ImageSource = (ImageSource)e.NewValue;
            control.LabelBackground.Visibility =
                e.NewValue == null ? Visibility.Visible : Visibility.Collapsed;
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
