using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace BoxSubMusic.App.Controls
{
    /// <summary>
    /// A monochrome controller-button badge for hint lines (2026-07-18):
    /// set <see cref="Label"/> to "A", "B", "X", "Y", "LB", "RB", "View", or
    /// "Menu". Single letters render as an outlined circle, LB/RB as a pill,
    /// "Menu" swaps the text for the hamburger glyph, and "View" for the
    /// two-panes symbol (the physical button carries neither a letter nor a
    /// pill label). Purely decorative — never focusable.
    /// </summary>
    public sealed partial class ControllerGlyph : UserControl
    {
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(
                nameof(Label),
                typeof(string),
                typeof(ControllerGlyph),
                new PropertyMetadata(string.Empty, OnLabelChanged));

        public ControllerGlyph()
        {
            InitializeComponent();
            IsTabStop = false;
        }

        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var glyph = (ControllerGlyph)d;
            string label = e.NewValue as string ?? string.Empty;
            bool isMenu = label == "Menu";
            bool isView = label == "View";
            bool isDrawn = isMenu || isView;
            glyph.MenuIcon.Visibility = isMenu ? Visibility.Visible : Visibility.Collapsed;
            glyph.ViewIcon.Visibility = isView ? Visibility.Visible : Visibility.Collapsed;
            glyph.LabelText.Visibility = isDrawn ? Visibility.Collapsed : Visibility.Visible;
            glyph.LabelText.Text = isDrawn ? string.Empty : label;
        }
    }
}
