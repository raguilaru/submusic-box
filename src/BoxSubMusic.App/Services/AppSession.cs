using System;
using System.Net.Http;
using BoxSubMusic.SubsonicClient;

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

        public static void SetActive(SubsonicApiClient client)
        {
            Client = client;
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
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
