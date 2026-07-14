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
        // A page whose whole content sits in a collapsed-until-loaded panel
        // (the detail pages, Songs) can reach the focus pass before the
        // newly revealed tree has had a layout pass — the search then finds
        // nothing focusable and a single-shot attempt would silently give
        // up, stranding focus in the nav (console finding 2026-07-14).
        // UpdateLayout forces measure (which also realizes list containers),
        // and a bounded retry covers anything still not ready.
        private const int MaxAttempts = 5;

        public static void MoveInto(UIElement root)
        {
            Attempt(root, MaxAttempts);
        }

        private static void Attempt(UIElement root, int attemptsLeft)
        {
            _ = root.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                (root as FrameworkElement)?.UpdateLayout();
                var target = FocusManager.FindFirstFocusableElement(root) as Control;
                bool focused = target != null && target.Focus(FocusState.Keyboard);
                if (!focused && attemptsLeft > 1)
                {
                    Attempt(root, attemptsLeft - 1);
                }
            });
        }
    }
}
