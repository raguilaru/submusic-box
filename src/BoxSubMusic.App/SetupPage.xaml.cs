using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoxSubMusic.App.Services;
using BoxSubMusic.Core.Setup;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace BoxSubMusic.App
{
    /// <summary>
    /// First-launch connection flow (Slice 03, requirements §4): sanitized
    /// URL, masked password, inline auth-vs-connection errors, Credential
    /// Locker storage, recent-servers list.
    /// </summary>
    public sealed partial class SetupPage : Page
    {
        private IReadOnlyList<RecentServer> _recentServers = new RecentServer[0];
        private bool _connecting;

        public SetupPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Controller-first rule: focus must have a starting point.
            ServerUrlBox.Focus(FocusState.Keyboard);

            _recentServers = await RecentServersStore.LoadAsync();
            if (_recentServers.Count > 0)
            {
                RecentList.ItemsSource = _recentServers;
                RecentPanel.Visibility = Visibility.Visible;
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            await ConnectAsync();
        }

        private async void Input_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Keyboard fallback (§13.1): Enter submits from any field.
            if (e.Key == VirtualKey.Enter && !_connecting)
            {
                e.Handled = true;
                await ConnectAsync();
            }
        }

        private void RecentList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var server = e.ClickedItem as RecentServer;
            if (server == null)
            {
                return;
            }

            // Recents never store secrets (§4), so only URL and username are
            // pre-filled; the password is typed again.
            ServerUrlBox.Text = server.Url;
            UsernameBox.Text = server.Username;
            HideError();
            PasswordInput.Focus(FocusState.Keyboard);
        }

        private async Task ConnectAsync()
        {
            HideError();

            Uri serverUrl = ServerUrlSanitizer.Sanitize(ServerUrlBox.Text);
            if (serverUrl == null)
            {
                ShowError("Enter a valid server address, like https://music.example.com or 10.0.0.5:4533.");
                return;
            }

            string username = UsernameBox.Text.Trim();
            if (username.Length == 0)
            {
                ShowError("Enter your username.");
                return;
            }

            // Never trimmed and never logged: passwords are opaque (§4).
            string password = PasswordInput.Password;

            SetBusy(true);
            try
            {
                var credentials = new SubsonicServerCredentials(serverUrl, username, password);
                var client = new SubsonicApiClient(credentials, AppSession.Http);
                await client.ConnectAsync();

                CredentialStore.SaveActive(serverUrl.AbsoluteUri, username, password);
                var updatedRecents = RecentServers.WithEntry(
                    _recentServers,
                    new RecentServer(serverUrl.AbsoluteUri, username, DateTimeOffset.UtcNow));
                await RecentServersStore.SaveAsync(updatedRecents);

                AppSession.SetActive(client);
                Frame.Navigate(typeof(MainPage));
                // Setup is done; B/back must not return here.
                Frame.BackStack.Clear();
            }
            catch (SubsonicApiException ex)
            {
                // The server answered but rejected us: an authentication-class
                // error, distinct from not reaching a server at all.
                ShowError(
                    ex.ErrorCode == SubsonicErrorCode.WrongUsernameOrPassword
                        ? "The server rejected this username or password."
                        : "The server refused the connection: " + ex.Message);
            }
            catch (SubsonicConnectionException)
            {
                ShowError(
                    "Could not reach a Subsonic server at " + serverUrl.AbsoluteUri +
                    ". Check the address and your network.");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _connecting = busy;
            ConnectButton.IsEnabled = !busy;
            ConnectProgress.IsActive = busy;
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorText.Text = string.Empty;
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }
}
