using System;
using BoxSubMusic.App.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BoxSubMusic.App
{
    /// <summary>
    /// Account / server info (§5 bottom area). Shows the stored server and
    /// username plus what capability detection found; sign-out clears the
    /// Credential Locker entry and returns to first-launch setup.
    /// </summary>
    public sealed partial class AccountPage : Page
    {
        private bool _suppressToggleEvents;

        public AccountPage()
        {
            InitializeComponent();

            // Focus convention: content owns focus on arrival (see ContentFocus).
            Loaded += (s, e) => ContentFocus.MoveInto(this);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Reflect the stored diagnostic state without re-raising Toggled.
            _suppressToggleEvents = true;
            SuppressArtworkToggle.IsOn = CoverArt.Suppressed;
            _suppressToggleEvents = false;

            var login = CredentialStore.TryLoadActive();
            if (login == null)
            {
                ServerText.Text = "No server configured.";
                UsernameText.Text = string.Empty;
                ServerInfoText.Text = string.Empty;
                return;
            }

            ServerText.Text = "Server: " + login.Url;
            UsernameText.Text = "Signed in as " + login.Username;

            // Capabilities exist only after a live ConnectAsync this session
            // (setup flow). A relaunch restores the client unconnected.
            var client = AppSession.Client;
            var capabilities = client == null ? null : client.Capabilities;
            if (capabilities != null)
            {
                var serverName = capabilities.ServerType ?? "Subsonic-compatible server";
                var serverVersion = capabilities.ServerVersion ?? "";
                ServerInfoText.Text =
                    (serverName + " " + serverVersion).Trim() +
                    (capabilities.IsOpenSubsonic ? " (OpenSubsonic)" : "") +
                    " — API " + capabilities.ApiVersion;
            }
            else
            {
                ServerInfoText.Text = "Server details will appear after the next connection.";
            }
        }

        /// <summary>
        /// Diagnostics toggle (§11, 2026-08-13). Persisted through
        /// <see cref="CoverArt.Suppressed"/> so the measurement run starts
        /// from launch: pages rebuild their cards on navigation, but the bar
        /// and the overlay only rebuild artwork on a track change, so flipping
        /// this mid-session would leave a mixed state that measures nothing.
        /// </summary>
        private void SuppressArtworkToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_suppressToggleEvents)
            {
                return;
            }

            CoverArt.Suppressed = SuppressArtworkToggle.IsOn;
            BackgroundDiagnostics.Log("Diagnostics: " + CoverArt.DescribeMode() + " (restart to measure)");
            Toast.Show(SuppressArtworkToggle.IsOn
                ? "Artwork off. Restart the app to measure."
                : "Artwork on. Restart the app to measure.");
        }

        private void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            // Slice 10: no audio (or queue, or now-playing bar) may survive
            // into the setup page — the stream URLs belong to this login.
            PlaybackService.Instance.Stop();

            CredentialStore.ClearActive();
            AppSession.Clear();

            // Setup lives on the ROOT frame (this page sits in the shell's
            // content frame). Clear the stack so B cannot re-enter the shell.
            var rootFrame = Window.Current.Content as Frame;
            if (rootFrame != null)
            {
                rootFrame.Navigate(typeof(SetupPage));
                rootFrame.BackStack.Clear();
            }
        }
    }
}
