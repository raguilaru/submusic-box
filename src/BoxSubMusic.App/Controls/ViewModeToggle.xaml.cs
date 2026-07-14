using System;
using Windows.UI.Xaml;
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

        private void Toggle_Changed(object sender, RoutedEventArgs e)
        {
            bool listMode = IsListMode;
            // The toggle advertises the mode it is currently showing.
            ModeLabel.Text = listMode ? "List" : "Grid";
            // Verified Fluent codepoints: list U+F4EE, grid U+F463 (§13.3).
            ModeIcon.Glyph = listMode ? "\uF4EE" : "\uF463";
            ModeChanged?.Invoke(this, e);
        }
    }
}
