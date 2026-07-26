using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;

namespace BoxSubMusic.App.Controls
{
    /// <summary>
    /// The shared grid/list toggle (requirements §13.5). Hosts read
    /// <see cref="IsListMode"/> and re-apply their view on
    /// <see cref="ModeChanged"/>; grid is the unchecked default (§7).
    /// </summary>
    public sealed partial class ViewModeToggle : UserControl
    {
        /// <summary>Raised after the mode flips and the face is updated.</summary>
        public event RoutedEventHandler ModeChanged;

        public ViewModeToggle()
        {
            InitializeComponent();
            ApplyMode();
        }

        /// <summary>True when the alternate list mode is active.</summary>
        public bool IsListMode => Toggle.IsChecked == true;

        /// <summary>
        /// Where gamepad down goes from the toggle (console finding
        /// 2026-07-14): the toggle sits at the header's far right, and in
        /// grid mode the last card column can end well short of the right
        /// edge — leaving no XY candidate straight below, so the focus
        /// engine's fallback jumped to the nav pane. Hosts point this at
        /// whichever view control is currently visible.
        /// </summary>
        public DependencyObject FocusDownTarget
        {
            get { return Toggle.XYFocusDown; }
            set { Toggle.XYFocusDown = value; }
        }

        /// <summary>
        /// The focusable element XYFocus hints should target — the inner
        /// ToggleButton, since a hint pointing at the (unfocusable)
        /// UserControl wrapper would be ignored. Console checkpoint #4
        /// finding 2026-07-15: Up from the browse grids jumped to the nav
        /// pane instead of this toggle; hosts pin their views' XYFocusUp
        /// here.
        /// </summary>
        public Control FocusTarget
        {
            get { return Toggle; }
        }

        private void Toggle_Changed(object sender, RoutedEventArgs e)
        {
            ApplyMode();
            ModeChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Repaints the segmented face for the current mode: the thumb sits
        /// under the active glyph, the other glyph dims, and the label names
        /// the mode. Only the thumb's column and two opacities change — the
        /// brushes stay declarative in XAML, so both themes (and a live theme
        /// switch) follow without code.
        /// </summary>
        private void ApplyMode()
        {
            bool listMode = IsListMode;
            Grid.SetColumn(ActiveThumb, listMode ? 1 : 0);
            GridGlyph.Opacity = listMode ? InactiveGlyphOpacity : 1.0;
            ListGlyph.Opacity = listMode ? 1.0 : InactiveGlyphOpacity;

            // The toggle advertises the mode it is currently showing.
            string mode = listMode ? "List" : "Grid";
            ModeLabel.Text = mode;
            // The face is graphical, so spell the state out for narration.
            AutomationProperties.SetName(Toggle, mode + " view. Select to switch.");
        }

        /// <summary>
        /// Dimming for the mode that is not current: low enough to read as
        /// inactive at 10 ft, high enough that its glyph stays legible so the
        /// control still advertises what pressing it switches to.
        /// </summary>
        private const double InactiveGlyphOpacity = 0.45;
    }
}
