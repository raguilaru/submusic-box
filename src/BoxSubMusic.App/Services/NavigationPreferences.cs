using System;
using Windows.Storage;

namespace BoxSubMusic.App.Services
{
    /// <summary>
    /// Nav-visibility preferences (requirements §5/§12): Artists, Albums,
    /// Playlists and Favorites are independently hideable; Search and Home
    /// deliberately have no setting at all, so they cannot be hidden by
    /// construction. Simple values live in LocalSettings (decision
    /// 2026-07-09, §16).
    /// </summary>
    internal static class NavigationPreferences
    {
        private const string ShowArtistsKey = "nav.showArtists";
        private const string ShowAlbumsKey = "nav.showAlbums";
        private const string ShowPlaylistsKey = "nav.showPlaylists";
        private const string ShowFavoritesKey = "nav.showFavorites";

        /// <summary>Raised after any visibility preference changes (same session).</summary>
        public static event Action Changed;

        public static bool ShowArtists
        {
            get { return Get(ShowArtistsKey); }
            set { Set(ShowArtistsKey, value); }
        }

        public static bool ShowAlbums
        {
            get { return Get(ShowAlbumsKey); }
            set { Set(ShowAlbumsKey, value); }
        }

        public static bool ShowPlaylists
        {
            get { return Get(ShowPlaylistsKey); }
            set { Set(ShowPlaylistsKey, value); }
        }

        /// <summary>
        /// Favorites visibility (added 2026-07-27, §16 — Favorites was
        /// originally spec'd as non-hideable). Hiding it only removes the
        /// nav destination; the starred state it shows is unaffected, and
        /// every other surface's favorite hearts keep working.
        /// </summary>
        public static bool ShowFavorites
        {
            get { return Get(ShowFavoritesKey); }
            set { Set(ShowFavoritesKey, value); }
        }

        private static bool Get(string key)
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            object value;
            if (settings.TryGetValue(key, out value) && value is bool visible)
            {
                return visible;
            }

            return true; // everything is visible until the user hides it
        }

        private static void Set(string key, bool value)
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
            Changed?.Invoke();
        }
    }
}
