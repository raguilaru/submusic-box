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
