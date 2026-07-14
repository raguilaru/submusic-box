using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace BoxSubMusic.App.Services
{
    /// <summary>
    /// Controller-first focus convention (console feedback 2026-07-14).
    /// Frame navigation destroys the old page's focused element, and XAML
    /// then falls back to the first focusable element in the whole tree —
    /// the nav pane — stranding the controller in the left menu after every
    /// navigation. The rule: every page moves focus into itself whenever it
    /// reveals an interactive state (content loaded; error states already
    /// focus their Retry button directly).
    /// </summary>
    public static class ContentFocus
    {
        public static void MoveInto(UIElement root)
        {
            // One low-priority dispatcher hop so freshly bound list/grid
            // item containers exist before the first-focusable search runs
            // (item realization happens after ItemsSource is set).
            _ = root.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                var target = FocusManager.FindFirstFocusableElement(root) as Control;
                target?.Focus(FocusState.Keyboard);
            });
        }
    }
}
