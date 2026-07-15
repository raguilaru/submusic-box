using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace BoxSubMusic.App.Controls
{
    /// <summary>
    /// The one favorite heart (§8/§9/§10; visual redesign from console
    /// checkpoint #4 feedback 2026-07-15): the starred state lives in the
    /// glyph itself — an accent-filled heart over a slightly larger black
    /// filled heart, which reads as a thick black border around the color.
    /// Unstarred shows the regular outline heart in the theme foreground.
    /// The ToggleButton's own checked chrome is silenced via lightweight
    /// styling, replacing the old accent-flooded-button treatment (which
    /// made the state readable but the heart itself invisible). Being a
    /// ToggleButton subclass keeps focus, XYFocus hints, IsChecked and the
    /// Checked/Unchecked events native — no wrapper-control focus quirks.
    /// </summary>
    public sealed class FavoriteHeart : ToggleButton
    {
        // Verified codepoints (§13.3): outline heart F47A in the Regular
        // font, filled heart F47E in the Filled font (both from the shipped
        // TTFs' cmaps, never invented).
        private const string OutlineGlyph = "\uF47A";
        private const string FilledGlyph = "\uF47E";

        // The black rim is a larger filled heart underneath the accent one;
        // this is its extra font size in epx (~3 epx of border all around).
        private const double RimExtra = 7;

        public static readonly DependencyProperty GlyphSizeProperty =
            DependencyProperty.Register(
                nameof(GlyphSize),
                typeof(double),
                typeof(FavoriteHeart),
                new PropertyMetadata(24.0, OnGlyphSizeChanged));

        private readonly FontIcon _rim;
        private readonly FontIcon _fill;
        private readonly FontIcon _outline;

        public FavoriteHeart()
        {
            var regularFont = (FontFamily)Application.Current.Resources["FluentIconFont"];
            var filledFont = (FontFamily)Application.Current.Resources["FluentIconFilledFont"];
            var accent = (Color)Application.Current.Resources["SystemAccentColor"];

            _rim = new FontIcon
            {
                FontFamily = filledFont,
                Glyph = FilledGlyph,
                Foreground = new SolidColorBrush(Colors.Black),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _fill = new FontIcon
            {
                FontFamily = filledFont,
                Glyph = FilledGlyph,
                Foreground = new SolidColorBrush(accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            // No explicit Foreground: inherits the theme foreground (the
            // Foreground=null console trap only applies to CLEARING it).
            _outline = new FontIcon
            {
                FontFamily = regularFont,
                Glyph = OutlineGlyph,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var layers = new Grid();
            layers.Children.Add(_rim);
            layers.Children.Add(_fill);
            layers.Children.Add(_outline);
            Content = layers;

            MinWidth = 0;
            Background = new SolidColorBrush(Colors.Transparent);

            // Lightweight styling: the checked state must not flood the
            // button with accent (the heart carries the state now).
            Resources["ToggleButtonBackgroundChecked"] = new SolidColorBrush(Colors.Transparent);
            Resources["ToggleButtonBackgroundCheckedPointerOver"] = new SolidColorBrush(Colors.Transparent);
            Resources["ToggleButtonBackgroundCheckedPressed"] = new SolidColorBrush(Colors.Transparent);
            Resources["ToggleButtonBorderBrushChecked"] = new SolidColorBrush(Colors.Transparent);
            Resources["ToggleButtonBorderBrushCheckedPointerOver"] = new SolidColorBrush(Colors.Transparent);
            Resources["ToggleButtonBorderBrushCheckedPressed"] = new SolidColorBrush(Colors.Transparent);

            Checked += (s, e) => UpdateLayers();
            Unchecked += (s, e) => UpdateLayers();

            ApplyGlyphSize();
            UpdateLayers();
        }

        /// <summary>Overall heart size in epx; the black rim extends slightly beyond it.</summary>
        public double GlyphSize
        {
            get { return (double)GetValue(GlyphSizeProperty); }
            set { SetValue(GlyphSizeProperty, value); }
        }

        private static void OnGlyphSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FavoriteHeart)d).ApplyGlyphSize();
        }

        private void ApplyGlyphSize()
        {
            _outline.FontSize = GlyphSize;
            _fill.FontSize = GlyphSize;
            _rim.FontSize = GlyphSize + RimExtra;
        }

        private void UpdateLayers()
        {
            bool starred = IsChecked == true;
            _rim.Visibility = starred ? Visibility.Visible : Visibility.Collapsed;
            _fill.Visibility = starred ? Visibility.Visible : Visibility.Collapsed;
            _outline.Visibility = starred ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
