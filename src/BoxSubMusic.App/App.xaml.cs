using System;
using System.Diagnostics;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default <see cref="Application"/> class.
    /// </summary>
    public sealed partial class App : Application
    {
        private bool _isInBackground;

        /// <summary>
        /// Initializes the singleton application object. This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

            Suspending += OnSuspending;

            // Requirement §11: with backgroundMediaPlayback declared, the app
            // keeps running while backgrounded but must respect the reduced
            // memory budget (strict on Xbox) or the OS terminates it.
            EnteredBackground += OnEnteredBackground;
            LeavingBackground += OnLeavingBackground;
            MemoryManager.AppMemoryUsageLimitChanging += OnMemoryUsageLimitChanging;
            MemoryManager.AppMemoryUsageIncreased += OnMemoryUsageIncreased;
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
            Debug.WriteLine($"Memory limit changing: {e.OldLimit} -> {e.NewLimit} (background={_isInBackground})");
            if (MemoryManager.AppMemoryUsage >= e.NewLimit)
            {
                ReduceMemoryUsage();
            }
        }

        private void OnMemoryUsageIncreased(object sender, object e)
        {
            AppMemoryUsageLevel level = MemoryManager.AppMemoryUsageLevel;
            Debug.WriteLine($"Memory usage increased: level={level} (background={_isInBackground})");
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

        /// <inheritdoc/>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active.
            if (Window.Current.Content is not Frame rootFrame)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page, configuring
                    // the new page by passing required information as a navigation parameter.
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }

                // Ensure the current window is active
                Window.Current.Activate();
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails.
        /// </summary>
        /// <param name="sender">The Frame which failed navigation.</param>
        /// <param name="e">Details about the navigation failure.</param>
        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception($"Failed to load page '{e.SourcePageType.FullName}'.");
        }

        /// <summary>
        /// Invoked when application execution is being suspended. Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            SuspendingDeferral deferral = e.SuspendingOperation.GetDeferral();

            // TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}
