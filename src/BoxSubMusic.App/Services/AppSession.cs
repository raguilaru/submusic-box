using System;
using System.Net.Http;
using System.Threading.Tasks;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.App.Services
{
    /// <summary>
    /// Process-wide connection state: the one shared HttpClient and the API
    /// client for the active server. The setup flow sets it after a verified
    /// connect; at relaunch it is rebuilt from stored credentials without
    /// touching the network (capability re-detection happens on first use).
    /// </summary>
    internal static class AppSession
    {
        // 15 s: setup errors must surface as inline feedback quickly; the
        // HttpClient default of 100 s reads as a hang from the couch.
        public static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        /// <summary>Client for the active server; null until setup completes once.</summary>
        public static SubsonicApiClient Client { get; private set; }

        // In-flight ConnectAsync shared by concurrent callers. UI-thread-only
        // (like everything else here), so no locking.
        private static Task<ServerCapabilities> _connectTask;

        public static void SetActive(SubsonicApiClient client)
        {
            Client = client;
            _connectTask = null;

            // Minted cover-art URLs embed the credentials they were built
            // with, so they cannot outlive the login that made them.
            CoverArt.Reset();
        }

        /// <summary>Drops the active client (sign-out).</summary>
        public static void Clear()
        {
            Client = null;
            _connectTask = null;
            CoverArt.Reset();
        }

        /// <summary>
        /// Connects the restored client on first use (§3: capability
        /// detection on connect). After a relaunch the client exists but has
        /// not pinged the server yet; pages calling data endpoints await this
        /// first. Concurrent callers share one attempt; a failed attempt is
        /// forgotten so a retry starts fresh.
        /// </summary>
        public static Task EnsureConnectedAsync()
        {
            var client = Client;
            if (client == null)
            {
                throw new InvalidOperationException("No active server session.");
            }

            if (client.Capabilities != null)
            {
                return Task.CompletedTask;
            }

            // A finished-but-failed attempt is replaced, so Retry actually
            // retries instead of re-awaiting the cached failure.
            if (_connectTask == null || _connectTask.IsFaulted || _connectTask.IsCanceled)
            {
                _connectTask = client.ConnectAsync();
            }

            return _connectTask;
        }

        /// <summary>
        /// Rebuilds the client from a stored login. Returns false when the
        /// stored data is unusable (e.g. corrupt URL) so the caller can fall
        /// back to the setup page instead of crashing at launch.
        /// </summary>
        public static bool TryInitializeFromStored(StoredServerLogin login)
        {
            if (login == null)
            {
                return false;
            }

            try
            {
                var credentials = new SubsonicServerCredentials(
                    new Uri(login.Url), login.Username, login.Password);
                Client = new SubsonicApiClient(credentials, Http);
                CoverArt.Reset();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
