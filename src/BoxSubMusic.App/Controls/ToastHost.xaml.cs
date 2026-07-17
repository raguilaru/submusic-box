using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace BoxSubMusic.App.Controls
{
    /// <summary>
    /// The visual half of the toast surface (Slice 14): shows one transient
    /// message at a time, bottom-centered, fading in and auto-dismissing
    /// after a 10-ft-readable dwell. <see cref="Services.Toast"/> is the
    /// app-facing entry point; the shell registers this control with it.
    /// UI thread only (the service marshals).
    /// </summary>
    public sealed partial class ToastHost : UserControl
    {
        // Long enough to read a sentence from the couch; short enough that a
        // burst of messages (consecutive skips) never feels stuck.
        private static readonly TimeSpan DismissDelay = TimeSpan.FromSeconds(3.5);

        private readonly DispatcherTimer _timer;

        public ToastHost()
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = DismissDelay };
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                FadeOutStoryboard.Begin();
            };

            // A Show during the fade-out restarted the timer, so skip the
            // collapse — the fade-in storyboard has already taken the
            // opacity back over.
            FadeOutStoryboard.Completed += (s, e) =>
            {
                if (!_timer.IsEnabled)
                {
                    ToastBorder.Visibility = Visibility.Collapsed;
                }
            };
        }

        /// <summary>Shows <paramref name="message"/>, replacing any current
        /// toast and restarting the dismiss clock.</summary>
        public void Show(string message)
        {
            MessageText.Text = message;
            ToastBorder.Visibility = Visibility.Visible;
            FadeInStoryboard.Begin();
            _timer.Stop();
            _timer.Start();
        }
    }
}
