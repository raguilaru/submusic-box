using System;
using Windows.UI.Core;

namespace BoxSubMusic.App.Services
{
    /// <summary>
    /// App-wide transient feedback (Slice 14, requirements §15 spirit: no
    /// silent failures — and no silent successes for actions with no other
    /// visible effect). Callable from any thread; messages are marshalled to
    /// the UI thread and rendered by the shell's <c>ToastHost</c>. Before the
    /// shell registers (setup flow) or after it unloads, Show is a no-op —
    /// those surfaces carry their own inline error states.
    /// </summary>
    public static class Toast
    {
        private static CoreDispatcher _dispatcher;
        private static Action<string> _show;

        public static void Register(CoreDispatcher dispatcher, Action<string> show)
        {
            _dispatcher = dispatcher;
            _show = show;
        }

        public static void Unregister()
        {
            _dispatcher = null;
            _show = null;
        }

        public static void Show(string message)
        {
            CoreDispatcher dispatcher = _dispatcher;
            Action<string> show = _show;
            if (dispatcher == null || show == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            if (dispatcher.HasThreadAccess)
            {
                show(message);
            }
            else
            {
                _ = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => show(message));
            }
        }
    }
}
