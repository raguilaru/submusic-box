using System;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace BoxSubMusic.App.Services
{
    /// <summary>Theme preference (requirements §12): Automatic follows the system theme.</summary>
    internal enum ThemeMode
    {
        Automatic,
        Dark,
        Light,
    }

    /// <summary>
    /// Theme selection (requirements §12/§13.2). The preference lives in
    /// LocalSettings (decision 2026-07-09, §16); the theme is applied as
    /// RequestedTheme on the root Frame, which cascades to every page and to
    /// flyouts/tooltips via their placement targets — Application.RequestedTheme
    /// cannot change at runtime. Automatic resolves the system theme from
    /// UISettings (Background color: black = dark) and re-resolves live on
    /// ColorValuesChanged, so a system theme change re-themes the app without
    /// a restart.
    /// </summary>
    internal static class ThemeService
    {
        private const string ModeKey = "theme.mode";

        // The event source must stay referenced: a local UISettings gets
        // collected and ColorValuesChanged silently stops firing.
        private static UISettings _uiSettings;
        private static Frame _root;
        private static CoreDispatcher _dispatcher;

        public static ThemeMode Mode
        {
            get
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                object value;
                if (settings.TryGetValue(ModeKey, out value) && value is string stored)
                {
                    if (stored == "dark")
                    {
                        return ThemeMode.Dark;
                    }
                    if (stored == "light")
                    {
                        return ThemeMode.Light;
                    }
                }

                return ThemeMode.Automatic;
            }
            set
            {
                var stored = value == ThemeMode.Dark ? "dark"
                    : value == ThemeMode.Light ? "light"
                    : "automatic";
                ApplicationData.Current.LocalSettings.Values[ModeKey] = stored;
                Apply();
            }
        }

        /// <summary>
        /// Applies the stored mode to the root frame and starts following
        /// system theme changes. Call once at launch, on the UI thread,
        /// after the root frame exists and before the first page shows.
        /// </summary>
        public static void Initialize(Frame root)
        {
            _root = root;
            _dispatcher = root.Dispatcher;
            _uiSettings = new UISettings();
            _uiSettings.ColorValuesChanged += OnColorValuesChanged;
            Apply();
        }

        private static void Apply()
        {
            if (_root == null)
            {
                return;
            }

            switch (Mode)
            {
                case ThemeMode.Dark:
                    _root.RequestedTheme = ElementTheme.Dark;
                    break;
                case ThemeMode.Light:
                    _root.RequestedTheme = ElementTheme.Light;
                    break;
                default:
                    // Resolved explicitly (not ElementTheme.Default):
                    // Application.RequestedTheme is a launch-time snapshot,
                    // so Default would ignore later system theme changes.
                    _root.RequestedTheme = ResolveSystemTheme();
                    break;
            }
        }

        private static ElementTheme ResolveSystemTheme()
        {
            // The system reports its theme through the Background color
            // (black in dark mode, white in light mode).
            Color background = _uiSettings.GetColorValue(UIColorType.Background);
            bool isDark = background.R + background.G + background.B < 128 * 3;
            return isDark ? ElementTheme.Dark : ElementTheme.Light;
        }

        private static async void OnColorValuesChanged(UISettings sender, object args)
        {
            // Fires on a non-UI thread, and also for accent-color changes;
            // Apply is cheap and re-setting an unchanged theme is a no-op.
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (Mode == ThemeMode.Automatic)
                {
                    Apply();
                }
            });
        }
    }
}
