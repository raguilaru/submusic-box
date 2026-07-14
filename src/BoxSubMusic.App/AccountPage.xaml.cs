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
        public AccountPage()
        {
            InitializeComponent();

            // Focus convention: content owns focus on arrival (see ContentFocus).
            Loaded += (s, e) => ContentFocus.MoveInto(this);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

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

        private void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
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
