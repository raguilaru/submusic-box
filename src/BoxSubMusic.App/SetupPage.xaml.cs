using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoxSubMusic.App.Services;
using BoxSubMusic.Core.Setup;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using Windows.System;
using Windows.System.Profile;
using Windows.UI.ViewManagement;
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
        private static readonly bool IsXbox =
            AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Xbox";

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

        private void ServerUrlBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (HandleGamepadTextEditing(ServerUrlBox, e))
            {
                return;
            }
            AdvanceOnEnter(e, UsernameBox);
        }

        private void UsernameBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (HandleGamepadTextEditing(UsernameBox, e))
            {
                return;
            }
            AdvanceOnEnter(e, PasswordInput);
        }

        private async void PasswordInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // PasswordBox exposes no caret/selection APIs and the characters
            // are masked anyway, so character-level gamepad editing is
            // impossible here: X clears the whole field for a clean retype.
            if (e.Key == VirtualKey.GamepadX)
            {
                PasswordInput.Password = string.Empty;
                e.Handled = true;
                return;
            }

            // Last field: Enter submits (§13.1) — the console OSK's accept
            // (Menu-button accelerator) or a hardware keyboard's Enter.
            if (e.Key == VirtualKey.Enter && !_connecting)
            {
                e.Handled = true;
                await ConnectAsync();
            }
        }

        /// <summary>
        /// Inline gamepad text editing (console finding 2026-07-14): with the
        /// OSK dismissed, the controller had no way to fix a typo — the D-pad
        /// only moves focus. Mirror the Xbox OSK's own accelerators on the
        /// field itself: X deletes backwards, LB/RB move the caret. These
        /// keys only ever come from a gamepad, so no device-family gate.
        /// </summary>
        private static bool HandleGamepadTextEditing(TextBox box, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.GamepadX:
                    if (box.SelectionLength > 0)
                    {
                        int selectionStart = box.SelectionStart;
                        box.Text = box.Text.Remove(selectionStart, box.SelectionLength);
                        box.Select(selectionStart, 0);
                    }
                    else if (box.SelectionStart > 0)
                    {
                        int caret = box.SelectionStart;
                        box.Text = box.Text.Remove(caret - 1, 1);
                        box.Select(caret - 1, 0);
                    }
                    e.Handled = true;
                    return true;

                case VirtualKey.GamepadLeftShoulder:
                {
                    // A selection collapses to its start, like a Left arrow.
                    int target = box.SelectionLength > 0
                        ? box.SelectionStart
                        : Math.Max(0, box.SelectionStart - 1);
                    box.Select(target, 0);
                    e.Handled = true;
                    return true;
                }

                case VirtualKey.GamepadRightShoulder:
                {
                    int end = box.SelectionStart + box.SelectionLength;
                    int target = box.SelectionLength > 0
                        ? end
                        : Math.Min(box.Text.Length, end + 1);
                    box.Select(target, 0);
                    e.Handled = true;
                    return true;
                }

                default:
                    return false;
            }
        }

        private void AdvanceOnEnter(KeyRoutedEventArgs e, Control next)
        {
            // Enter means "field done", not "submit": the OSK has no
            // next-field concept of its own, so the app moves focus. Only
            // the last field connects — otherwise accepting the first OSK
            // entry would attempt a connect with empty credentials.
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            e.Handled = true;
            next.Focus(FocusState.Keyboard);

            if (IsXbox)
            {
                // Chain the OSK onto the next field so the form is completed
                // without touching B or the D-pad. Console-only: on desktop
                // a hardware-keyboard Enter must not pop the touch keyboard.
                InputPane.GetForCurrentView().TryShow();
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
