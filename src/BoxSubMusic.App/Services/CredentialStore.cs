using System;
using Windows.Security.Credentials;
using Windows.Storage;

namespace BoxSubMusic.App.Services
{
    /// <summary>A stored server login, reassembled from settings + vault.</summary>
    internal sealed class StoredServerLogin
    {
        public StoredServerLogin(string url, string username, string password)
        {
            Url = url;
            Username = username;
            Password = password;
        }

        public string Url { get; }
        public string Username { get; }
        public string Password { get; }
    }

    /// <summary>
    /// Persists the active server login per requirements §4: URL and username
    /// in LocalSettings, the password ONLY in the Credential Locker
    /// (PasswordVault). Secrets must never reach settings or log output.
    /// </summary>
    internal static class CredentialStore
    {
        private const string VaultResource = "BoxSubMusic.Server";
        private const string ServerUrlKey = "server.url";
        private const string UsernameKey = "server.username";

        public static void SaveActive(string serverUrl, string username, string password)
        {
            // One active server in v1: replace any previous vault entry.
            RemoveVaultEntries();
            var vault = new PasswordVault();
            vault.Add(new PasswordCredential(VaultResource, username, password));

            var settings = ApplicationData.Current.LocalSettings.Values;
            settings[ServerUrlKey] = serverUrl;
            settings[UsernameKey] = username;
        }

        /// <summary>
        /// The stored server URL + username WITHOUT touching the vault —
        /// for callers that only need to identify the active server (e.g.
        /// tagging/validating the §6.1 resume state), not authenticate.
        /// Returns false when no login is stored.
        /// </summary>
        public static bool TryGetActiveIdentity(out string serverUrl, out string username)
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            object urlValue;
            object usernameValue;
            settings.TryGetValue(ServerUrlKey, out urlValue);
            settings.TryGetValue(UsernameKey, out usernameValue);
            serverUrl = urlValue as string;
            username = usernameValue as string;
            return !string.IsNullOrEmpty(serverUrl) && !string.IsNullOrEmpty(username);
        }

        /// <summary>Returns the stored login, or null when none/incomplete.</summary>
        public static StoredServerLogin TryLoadActive()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            object urlValue;
            object usernameValue;
            if (!settings.TryGetValue(ServerUrlKey, out urlValue) ||
                !settings.TryGetValue(UsernameKey, out usernameValue))
            {
                return null;
            }

            var url = urlValue as string;
            var username = usernameValue as string;
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(username))
            {
                return null;
            }

            try
            {
                var credential = new PasswordVault().Retrieve(VaultResource, username);
                credential.RetrievePassword();
                return new StoredServerLogin(url, username, credential.Password);
            }
            catch (Exception)
            {
                // Retrieve throws when the vault has no matching entry — the
                // settings half is stale, so there is no usable login.
                return null;
            }
        }

        public static void ClearActive()
        {
            RemoveVaultEntries();
            var settings = ApplicationData.Current.LocalSettings.Values;
            settings.Remove(ServerUrlKey);
            settings.Remove(UsernameKey);
        }

        private static void RemoveVaultEntries()
        {
            try
            {
                var vault = new PasswordVault();
                foreach (var credential in vault.FindAllByResource(VaultResource))
                {
                    vault.Remove(credential);
                }
            }
            catch (Exception)
            {
                // FindAllByResource throws when nothing is stored yet.
            }
        }
    }
}
