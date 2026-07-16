using System;
using System.Diagnostics;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.System;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        private bool _isInBackground;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            // Requirement §2/§13.1: Xbox runs UWP apps in "mouse mode" by
            // default — the left stick drives a pointer and the D-pad does
            // nothing. Opt out so the gamepad performs XY focus navigation.
            // Desktop is unaffected, so this difference never shows in the VM.
            this.RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;

            // Requirement §2: precise 1920×1080 layout control. Xbox applies
            // an automatic 200% XAML scale by default (960×540 effective
            // layout), which doubled every content dimension on the TV —
            // the layouts are designed for a true 1920-epx canvas. Console
            // finding 2026-07-14. No-op (returns false) on desktop.
            ApplicationViewScaling.TrySetDisableLayoutScaling(true);

            this.InitializeComponent();
            this.Suspending += OnSuspending;

            // Requirement §11: with backgroundMediaPlayback declared, the app
            // keeps running while backgrounded but must respect the reduced
            // memory budget (strict on Xbox) or the OS terminates it.
            this.EnteredBackground += OnEnteredBackground;
            this.LeavingBackground += OnLeavingBackground;
            MemoryManager.AppMemoryUsageLimitChanging += OnMemoryUsageLimitChanging;
            MemoryManager.AppMemoryUsageIncreased += OnMemoryUsageIncreased;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            // Requirement §2: opt out of the system-reserved TV-safe border
            // (the default UseVisible bounds inset the whole window, showing
            // black bands on every edge — console finding 2026-07-14) and
            // draw edge to edge. Overscan is instead handled by the app's
            // own layout padding on the pages (§2 allows exactly this
            // trade). Harmless on desktop, where visible bounds already
            // equal the window.
            ApplicationView.GetForCurrentView().SetDesiredBoundsMode(
                ApplicationViewBoundsMode.UseCoreWindow);

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;

                // Requirement §12: apply the persisted theme preference to
                // the root frame before the first page renders (covers the
                // setup page too) and start following the system theme when
                // the mode is Automatic.
                Services.ThemeService.Initialize(rootFrame);
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // Slice 03 (§4): a stored login goes straight to the shell,
                    // otherwise first-launch setup. The client is rebuilt from
                    // storage without connecting — launch stays network-free.
                    var storedLogin = Services.CredentialStore.TryLoadActive();
                    var startPage = Services.AppSession.TryInitializeFromStored(storedLogin)
                        ? typeof(MainPage)
                        : typeof(SetupPage);
                    rootFrame.Navigate(startPage, e.Arguments);
                }
                // Ensure the current window is active
                Window.Current.Activate();
            }
        }

        private void OnEnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            _isInBackground = true;
            // Spike: skeleton only. Real implementation releases UI/view
            // resources (image caches, view models) here to fit the
            // background memory budget.
        }

        private void OnLeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            _isInBackground = false;
            // Spike: skeleton only. Real implementation restores/rebuilds
            // UI state here if it was dropped on entering the background.
        }

        private void OnMemoryUsageLimitChanging(object sender, AppMemoryUsageLimitChangingEventArgs e)
        {
            Debug.WriteLine("Memory limit changing: " + e.OldLimit + " -> " + e.NewLimit + " (background=" + _isInBackground + ")");
            if (MemoryManager.AppMemoryUsage >= e.NewLimit)
            {
                ReduceMemoryUsage();
            }
        }

        private void OnMemoryUsageIncreased(object sender, object e)
        {
            AppMemoryUsageLevel level = MemoryManager.AppMemoryUsageLevel;
            Debug.WriteLine("Memory usage increased: level=" + level + " (background=" + _isInBackground + ")");
            if (level == AppMemoryUsageLevel.OverLimit || level == AppMemoryUsageLevel.High)
            {
                ReduceMemoryUsage();
            }
        }

        private void ReduceMemoryUsage()
        {
            // Spike: GC is the only lever we have so far. Real implementation
            // additionally drops the XAML content tree while backgrounded.
            if (_isInBackground)
            {
                GC.Collect();
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}
