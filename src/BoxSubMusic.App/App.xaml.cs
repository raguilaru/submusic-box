using System;
using System.Reflection;
using System.Threading;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        private bool _isInBackground;

        // Guards against the duplicate reduction pass (see ReduceMemoryUsage).
        // Both memory events arrive on threadpool threads, but the reduction
        // itself marshals to the UI thread and blocks, so the second caller is
        // always still inside the first's window — a plain bool is enough, and
        // a lock would risk holding the second handler for the whole budget.
        private volatile bool _isReducing;

        /// <summary>
        /// The UI thread's dispatcher, captured while on it. Memory-pressure
        /// events arrive on a threadpool thread, where <c>Window.Current</c>
        /// is null, so the release has to be marshalled back through this.
        /// </summary>
        private CoreDispatcher _uiDispatcher;

        // Three bounded waits, summing inside the OS's ~2 s budget: drop the
        // tree, WAIT FOR THE UNLOAD to actually happen, then collect. See
        // CollectAfterRelease for why that middle step exists.
        private static readonly TimeSpan ReleaseTimeout = TimeSpan.FromMilliseconds(700);
        private static readonly TimeSpan UnloadTimeout = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan CollectTimeout = TimeSpan.FromMilliseconds(600);

        /// <summary>
        /// Signalled when the shell's Unloaded callback has run, which is what
        /// makes the released tree collectable (§11, 2026-08-13).
        ///
        /// Run 14 tried to get this ordering from <c>CoreDispatcherPriority.Low</c>
        /// and it held in only **2 of 5** cycles — a dispatcher priority is a
        /// scheduling hint, not a happens-after guarantee, and in the three
        /// losing cycles the collection still ran with 85-87 handlers attached.
        /// Waiting on the actual event is the deterministic form.
        ///
        /// Safe to block a threadpool thread on: the drop has already been
        /// dispatched and completed by then, so the UI thread is free to raise
        /// Unloaded. Bounded by <see cref="UnloadTimeout"/> regardless, because
        /// no shell exists to unload when setup is showing.
        /// </summary>
        private static readonly ManualResetEventSlim ShellUnloaded =
            new ManualResetEventSlim(false);

        /// <summary>Called by the shell from its Unloaded callback.</summary>
        internal static void NotifyShellUnloaded()
        {
            ShellUnloaded.Set();
        }

        /// <summary>
        /// When this assembly was compiled. Bump-free build identity: the
        /// package version rarely changes during a debugging round, but this
        /// moves on every rebuild, so the log can distinguish a fresh deploy
        /// from a stale one on the console.
        /// </summary>
        private static readonly string BuildStamp =
            new System.IO.FileInfo(typeof(App).GetTypeInfo().Assembly.Location)
                .LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");

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
            this.UnhandledException += OnUnhandledException;
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

            // Package version + build time, so a log answers "is the console
            // actually running the build I just deployed?" without guesswork.
            // A stale deploy and a failed fix look identical otherwise.
            try
            {
                PackageVersion v = Package.Current.Id.Version;
                Services.BackgroundDiagnostics.Log(
                    "Build: package=" + v.Major + "." + v.Minor + "." + v.Build + "." + v.Revision
                    + ", compiled=" + BuildStamp);
            }
            catch (Exception ex)
            {
                Services.BackgroundDiagnostics.Log("Build: version unavailable (" + ex.Message + ")");
            }

            // Stamped on every launch because it decides whether the rest of
            // the log means anything: Xbox does NOT enforce background memory
            // limits on a debugged process, so an F5 run never receives the
            // 1024 → 128 MB transition and never exercises the release path.
            // Two console passes were spent discovering this from the limit
            // value alone (an unrestricted ~3168 MB vs. the real 1024 MB).
            Services.BackgroundDiagnostics.Log(
                "OnLaunched: debuggerAttached=" + System.Diagnostics.Debugger.IsAttached
                + (System.Diagnostics.Debugger.IsAttached
                    ? " — background memory limits are NOT enforced in this run; it cannot test background survival"
                    : " — background memory limits apply; this run is valid for background testing"));
            // Which artwork mode this run measures (§11, 2026-08-13). The
            // suppressed run exists to bound artwork's share of the footprint;
            // without this line the two runs are indistinguishable in the log.
            Services.BackgroundDiagnostics.LogMemory("OnLaunched");

            // Flushed immediately so the run is identifiable even if the app
            // is killed before any other event writes.
            Services.BackgroundDiagnostics.FlushSoon();

            if (e.PrelaunchActivated == false)
            {
                CreateRootFrame(e.Arguments);

                // Ensure the current window is active
                Window.Current.Activate();
            }
        }

        /// <summary>
        /// Builds the window content. Called at launch and again from
        /// <see cref="OnLeavingBackground"/> when the view was released to
        /// survive the background memory limit — which is why this is a
        /// separate method rather than inline in OnLaunched.
        /// </summary>
        private void CreateRootFrame(string arguments)
        {
            // Captured here because this always runs on the UI thread, at
            // launch and on every rebuild. The memory-pressure handlers need
            // it to get back onto this thread.
            _uiDispatcher = Window.Current.Dispatcher;

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content.
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;

                // Requirement §12: apply the persisted theme preference to
                // the root frame before the first page renders (covers the
                // setup page too) and start following the system theme when
                // the mode is Automatic. Idempotent — a rebuilt view
                // re-points the service at the new frame without stacking a
                // second system-theme subscription.
                Services.ThemeService.Initialize(rootFrame);
            }

            if (rootFrame.Content == null)
            {
                // Slice 03 (§4): a stored login goes straight to the shell,
                // otherwise first-launch setup. The client is rebuilt from
                // storage without connecting — launch stays network-free.
                //
                // AppSession is process-wide and outlives a released view, so
                // a rebuild after a background memory drop goes straight back
                // to the shell without re-reading the credential locker.
                Type startPage;
                if (Services.AppSession.Client != null)
                {
                    startPage = typeof(MainPage);
                }
                else
                {
                    var storedLogin = Services.CredentialStore.TryLoadActive();
                    startPage = Services.AppSession.TryInitializeFromStored(storedLogin)
                        ? typeof(MainPage)
                        : typeof(SetupPage);
                }

                rootFrame.Navigate(startPage, arguments);
            }
        }

        private void OnEnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            _isInBackground = true;
            Services.BackgroundDiagnostics.LogMemory("EnteredBackground");

            // Drop the decoded covers (§11, 2026-08-13). This is the one thing
            // the app could never do before: with covers loaded from a
            // UriSource they lived in XAML's process-wide cache, which is why
            // releasing the whole visual tree reclaimed 0 MB on every console
            // run. Cheap and safe here — images still assigned to an on-screen
            // element survive until that element goes, so nothing blanks; only
            // the app's own retention goes, which is what lets the later view
            // release actually reclaim something.
            // Dropping the app-owned cover cache is the one artwork release
            // that works: a stream-decoded bitmap has no URI, so the framework
            // caches nothing and this reference is the only one. The megabytes
            // register a few seconds later rather than immediately (§11), which
            // is why the figure here is what was released, not what the reading
            // moved.
            long dropped = Services.CoverArt.CachedBitmapBytes;
            Services.CoverArt.DropBitmaps();
            Services.BackgroundDiagnostics.LogMemory(
                "AfterDropCovers (released " + Services.BackgroundDiagnostics.Megabytes((ulong)dropped) + " MB of cached covers)");

            Services.BackgroundDiagnostics.FlushSoon();

            // Deliberately NOT releasing the view here. Microsoft's guidance
            // is to defer that to AppMemoryUsageLimitChanging: this event also
            // fires for transitions the app comes straight back from, and
            // tearing down the shell every time would make returning to the
            // app visibly slow for no benefit. The handler below does the work
            // only when the numbers actually demand it.
            //
            // "Do not perform long running operations in the EnteredBackground
            // event because you can cause the transition between applications
            // to appear slow to the user."
        }

        private void OnLeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            _isInBackground = false;
            Services.BackgroundDiagnostics.LogMemory("LeavingBackground");
            Services.PlaybackService.NotifyLeavingBackground();

            // Rebuild the view if it was released to survive the background
            // limit. Playback never stopped — PlaybackService and its
            // MediaPlayer are process-wide and hold no reference to the
            // visual tree — so this restores the UI around audio that has
            // been running the whole time.
            //
            // Marshalled for the same thread-static reason the release is
            // (see ReleaseViewContent). This event is expected on the UI
            // thread, but a null Window.Current read from the wrong thread
            // would skip the rebuild and leave the app permanently blank —
            // too severe a failure to leave resting on an assumption.
            CoreDispatcher dispatcher = _uiDispatcher;
            if (dispatcher == null || dispatcher.HasThreadAccess)
            {
                RebuildViewIfReleased();
            }
            else
            {
                _ = dispatcher.RunAsync(CoreDispatcherPriority.High, RebuildViewIfReleased);
            }
        }

        private void RebuildViewIfReleased()
        {
            if (Window.Current == null || Window.Current.Content != null)
            {
                return;
            }

            Services.BackgroundDiagnostics.Log("Rebuilding released view.");
            CreateRootFrame(string.Empty);

            // The resting floor climbed roughly 15-30 MB per release/rebuild
            // cycle on device (2026-08-12: 136 MB then 165 MB after
            // equivalent cycles). If the released tree is not actually being
            // collected, each rebuild stacks another one, and this line is
            // where that shows: compare it against the LeavingBackground
            // reading immediately above.
            GC.Collect();
            Services.BackgroundDiagnostics.LogMemory("AfterViewRebuild");
            Services.BackgroundDiagnostics.FlushSoon();
        }

        private void OnMemoryUsageLimitChanging(object sender, AppMemoryUsageLimitChangingEventArgs e)
        {
            // THE critical handler on Xbox: this is where the limit drops from
            // ~1 GB to 128 MB because a game is taking the foreground. The app
            // has roughly 2 seconds to get under the new limit or the OS
            // suspends/terminates it — which is exactly the "music stops when
            // I launch a game" symptom.
            Services.BackgroundDiagnostics.Log(
                "AppMemoryUsageLimitChanging: usage="
                + Services.BackgroundDiagnostics.Megabytes(MemoryManager.AppMemoryUsage) + " MB"
                + ", old=" + Services.BackgroundDiagnostics.Megabytes(e.OldLimit) + " MB"
                + ", new=" + Services.BackgroundDiagnostics.Megabytes(e.NewLimit) + " MB"
                + ", background=" + _isInBackground);

            // Reduce against a safety margin rather than the bare limit.
            // AppMemoryUsage lags the allocations the graphics/XAML layers
            // charge back to the app, so sitting just under the line is not
            // the same as being safe; and the cost of shedding the view when
            // it turns out not to have been strictly necessary is one page
            // rebuild on return, against a terminated app if we cut it fine.
            ulong threshold = e.NewLimit - (e.NewLimit / 4);
            if (MemoryManager.AppMemoryUsage >= threshold)
            {
                ReduceMemoryUsage();
            }

            Services.BackgroundDiagnostics.LogMemory("AfterLimitChangingHandler");
            Services.BackgroundDiagnostics.FlushSoon();
        }

        private void OnMemoryUsageIncreased(object sender, object e)
        {
            AppMemoryUsageLevel level = MemoryManager.AppMemoryUsageLevel;
            Services.BackgroundDiagnostics.LogMemory("AppMemoryUsageIncreased");
            if (level == AppMemoryUsageLevel.OverLimit || level == AppMemoryUsageLevel.High)
            {
                ReduceMemoryUsage();
                Services.BackgroundDiagnostics.FlushSoon();
            }
        }

        /// <summary>
        /// Releases the app's view while backgrounded (§11).
        ///
        /// Dropping the window content is the one lever big enough to matter
        /// here, because it takes the whole visual tree with it: the shell,
        /// the navigation frame and — importantly for this app — the two
        /// pages held by <c>NavigationCacheMode.Required</c>, Songs and
        /// Search. The Songs page alone retains the entire song library
        /// (§7.3's client-side sort requires it), which is the largest
        /// single allocation the app makes and one an ordinary GC will never
        /// reclaim while the frame keeps the page alive.
        ///
        /// This works only because every page unregisters its handlers in
        /// its Unloaded callback (MainPage drops the static
        /// NavigationPreferences and Toast registrations there): a single
        /// surviving reference from a static to a page would keep the whole
        /// tree charged to the app and make the release a no-op.
        /// </summary>
        private void ReduceMemoryUsage()
        {
            // Both memory handlers call this, and on Xbox they BOTH fire for the
            // same backgrounding — the console log shows the pair arriving
            // within a millisecond of each other, each running a full tree drop
            // and two GCs (§11, run 14: two AfterViewDropped/AfterCollect/
            // AfterViewRelease triples per excursion). The second pass has
            // nothing left to release and simply spends the budget the first
            // one needs. The OS allows ~2 s in total, so this guard is about
            // time, not megabytes.
            //
            // Deliberately NOT a "have we ever reduced" flag: a later, genuine
            // pressure event during the same backgrounding must still be able
            // to act. This only collapses the duplicate pair.
            if (_isReducing)
            {
                Services.BackgroundDiagnostics.Log("ReduceMemoryUsage: already reducing, skipped.");
                return;
            }

            _isReducing = true;
            try
            {
                ReduceMemoryUsageCore();
            }
            finally
            {
                _isReducing = false;
            }
        }

        private void ReduceMemoryUsageCore()
        {
            if (_isInBackground)
            {
                // Native memory, which is where this app's footprint actually
                // is: the played-item decode buffers cost real megabytes,
                // especially for high-bitrate FLAC.
                Services.PlaybackService.NotifyBackgroundMemoryPressure();

                // Covers again: EnteredBackground already dropped them, but a
                // pressure event can arrive without one (and the user may have
                // returned and re-browsed since).
                Services.CoverArt.DropBitmaps();
                ReleaseViewContent();
            }

            // Forced collection is the documented exception to leaving the GC
            // alone: the 2-second budget is shorter than the time the
            // collector would take to notice on its own, and what is being
            // reclaimed is precisely what the OS is about to measure.
            //
            // The heavy reclamation happens inside ReleaseViewContent, which
            // runs the finalizer pass the visual tree actually needs. This is
            // the tail collection for anything the release itself dropped.
            GC.Collect();
        }

        /// <summary>
        /// Drops the window content, on the UI thread.
        ///
        /// The marshalling is the whole point of this method. MemoryManager
        /// raises its events on a threadpool thread, and
        /// <c>Window.Current</c> is THREAD-STATIC — it returns null on any
        /// thread but the UI one. Touching it directly in those handlers
        /// therefore looks like "there is no view to release" and silently
        /// does nothing, which is exactly how the first version of this fix
        /// failed on device (console log 2026-08-12: the limit dropped to
        /// 128 MB with usage at 161 MB, no release happened, and usage was
        /// unchanged afterwards).
        /// </summary>
        private void ReleaseViewContent()
        {
            CoreDispatcher dispatcher = _uiDispatcher;
            if (dispatcher == null)
            {
                Services.BackgroundDiagnostics.Log(
                    "Cannot release view content: no UI dispatcher captured yet.");
                return;
            }

            if (dispatcher.HasThreadAccess)
            {
                // Already on the UI thread, so we cannot yield to let the
                // Unloaded callbacks run before collecting — the collection is
                // queued behind them instead and this call returns without it.
                // Not the path the memory events take (they arrive on the
                // threadpool); kept correct rather than optimal.
                ReleaseViewContentOnUiThread();
                _ = dispatcher.RunAsync(CoreDispatcherPriority.Low, CollectAfterRelease);
                return;
            }

            // Block until the UI thread has actually dropped the tree: the OS
            // measures us within ~2 s of the limit change, so firing this
            // off asynchronously and returning would race the measurement.
            // The wait is bounded well inside that budget — an unresponsive
            // UI thread must not be the reason we blow the deadline.
            try
            {
                ShellUnloaded.Reset();
                dispatcher
                    .RunAsync(CoreDispatcherPriority.High, ReleaseViewContentOnUiThread)
                    .AsTask()
                    .Wait(ReleaseTimeout);

                // Nulling the window content does not run the pages' Unloaded
                // callbacks synchronously — it schedules them — and those
                // callbacks are what unregister the shell and its controls from
                // process-lifetime statics. Collect before they run and the
                // tree is still fully reachable, and the collection reclaims
                // nothing — which is exactly what the console measured. So wait
                // for the unload to actually happen.
                bool unloaded = ShellUnloaded.Wait(UnloadTimeout);
                Services.BackgroundDiagnostics.Log(unloaded
                    ? "Shell unload observed; collecting."
                    : "Shell unload NOT observed within the wait — collecting anyway.");

                dispatcher
                    .RunAsync(CoreDispatcherPriority.Normal, CollectAfterRelease)
                    .AsTask()
                    .Wait(CollectTimeout);
            }
            catch (Exception ex)
            {
                Services.BackgroundDiagnostics.Log("Releasing view content failed: " + ex.Message);
            }
        }

        private void ReleaseViewContentOnUiThread()
        {
            if (Window.Current == null || Window.Current.Content == null)
            {
                return;
            }

            Services.BackgroundDiagnostics.Log("Releasing view content to fit the background limit.");

            // Explicitly break parent/child links before dropping the root.
            // A single stray reference into the middle of the tree would
            // otherwise keep the whole thing alive, and this severs the links
            // rather than trusting that none exists.
            try
            {
                VisualTreeHelper.DisconnectChildrenRecursive(Window.Current.Content);
            }
            catch (Exception ex)
            {
                Services.BackgroundDiagnostics.Log("DisconnectChildrenRecursive failed: " + ex.Message);
            }

            // Pages release further resources from their own Unloaded
            // handlers, which this assignment SCHEDULES rather than runs — see
            // CollectAfterRelease, which is why the collection is no longer
            // here.
            Window.Current.Content = null;

            Services.BackgroundDiagnostics.LogMemory("AfterViewDropped");
        }

        /// <summary>
        /// Collects, in a dispatcher pass AFTER the one that dropped the tree
        /// (§11, 2026-08-13).
        ///
        /// The ordering is the entire point. `Window.Current.Content = null`
        /// schedules the pages' Unloaded callbacks; it does not run them. Those
        /// callbacks are what unregister the shell and its controls from
        /// process-lifetime statics, and until they run the whole tree is still
        /// reachable — so a collection in the same pass has nothing it is
        /// allowed to reclaim. The console showed this plainly: the release
        /// reclaimed 0 MB with 87 handlers still attached, MainPage.Unloaded
        /// arrived 111 ms afterwards, and usage had dropped 238 to 220 MB by the
        /// time anything looked again. Ordering this with a
        /// CoreDispatcherPriority.Low hint was tried first and held in only 2 of
        /// 5 cycles — a dispatcher priority is not a happens-after guarantee.
        ///
        /// The finalizer pass is not optional. The XAML tree and its decoded
        /// bitmaps are overwhelmingly native objects reached through RCWs:
        /// collecting frees the managed wrappers, but the native memory the OS
        /// measures comes back only once those wrappers' finalizers run — hence
        /// collect, drain, collect again.
        /// </summary>
        private void CollectAfterRelease()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Services.BackgroundDiagnostics.LogMemory("AfterViewRelease");
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
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            // §6.1: flush the in-track resume position — termination happens
            // from the suspended state, and the periodic saves are ~10 s
            // coarse. The queue structure is already on disk (persisted on
            // every structural change).
            Services.PlaybackService.NotifySuspending();

            // Suspension is the last code that runs before a termination, so
            // this is what makes the run-up to a kill readable afterwards.
            Services.BackgroundDiagnostics.LogMemory("Suspending");
            await Services.BackgroundDiagnostics.FlushAsync();

            deferral.Complete();
        }

        /// <summary>
        /// Records faults that would otherwise look identical to a
        /// memory-policy termination from the outside — the app simply stops.
        /// The log is what tells the two apart.
        /// </summary>
        private void OnUnhandledException(
            object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Services.BackgroundDiagnostics.Log(
                "UnhandledException (background=" + _isInBackground + "): " + e.Exception);
            Services.BackgroundDiagnostics.LogMemory("AtUnhandledException");
            Services.BackgroundDiagnostics.FlushSoon();

            // Deliberately not marking it handled: swallowing the fault would
            // hide the very thing being investigated.
        }
    }
}
