using System.ComponentModel;
using Windows.UI.Text;
using Windows.UI.Xaml;

namespace BoxSubMusic.App.Models
{
    /// <summary>
    /// Display model for one lyric line in the now-playing overlay's Lyrics
    /// tab. Observable like <see cref="QueueRow"/>: for synced lyrics the
    /// active line raises <see cref="IsCurrent"/>, which brightens and bolds
    /// the text and shows the accent marker bar; unsynced lines render at a
    /// steady readable weight with no highlight. Colour is left to the
    /// template's ThemeResource brushes (theme-correct) — this model only
    /// drives weight, opacity, and the marker's visibility.
    /// </summary>
    public sealed class LyricLineRow : INotifyPropertyChanged
    {
        private readonly bool _synced;
        private bool _isCurrent;

        public LyricLineRow(string text, bool synced)
        {
            Text = text ?? string.Empty;
            _synced = synced;
        }

        public string Text { get; }

        /// <summary>True while this is the line the playback position sits on
        /// (synced lyrics only).</summary>
        public bool IsCurrent
        {
            get { return _isCurrent; }
            set
            {
                if (_isCurrent == value)
                {
                    return;
                }

                _isCurrent = value;
                var handler = PropertyChanged;
                if (handler != null)
                {
                    handler(this, new PropertyChangedEventArgs(nameof(IsCurrent)));
                    handler(this, new PropertyChangedEventArgs(nameof(LineOpacity)));
                    handler(this, new PropertyChangedEventArgs(nameof(LineWeight)));
                    handler(this, new PropertyChangedEventArgs(nameof(MarkerVisibility)));
                }
            }
        }

        /// <summary>Dimmed for inactive synced lines; steady for unsynced text.</summary>
        public double LineOpacity
        {
            get
            {
                if (!_synced)
                {
                    return 0.9;
                }

                if (_isCurrent)
                {
                    return 1.0;
                }

                return string.IsNullOrEmpty(Text) ? 0.2 : 0.45;
            }
        }

        public FontWeight LineWeight
        {
            get { return _synced && _isCurrent ? FontWeights.SemiBold : FontWeights.Normal; }
        }

        /// <summary>The accent marker bar shows only on the active synced line.</summary>
        public Visibility MarkerVisibility
        {
            get { return _synced && _isCurrent ? Visibility.Visible : Visibility.Collapsed; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
