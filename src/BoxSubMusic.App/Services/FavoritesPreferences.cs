using BoxSubMusic.Core.Browsing;
using Windows.Storage;

namespace BoxSubMusic.App.Services
{
    /// <summary>
    /// Remembered state for the Favorites view: which tab was last open, and
    /// the sort chosen within each tab. Scalars in LocalSettings (the
    /// 2026-07-09 persistence split); values are stored as the enum's integer
    /// so a future reorder of the enum would be the only way to corrupt them —
    /// out-of-range values fall back to the default rather than throwing.
    /// </summary>
    internal static class FavoritesPreferences
    {
        private const string TabKey = "favorites.tab";
        private const string ArtistSortKey = "favorites.sort.artists";
        private const string AlbumSortKey = "favorites.sort.albums";
        private const string SongSortKey = "favorites.sort.songs";

        /// <summary>
        /// Songs is the default tab until the user picks another. Note this is
        /// deliberately NOT the first tab: the tab strip reads
        /// Artists / Albums / Songs, but favorited songs are the thing people
        /// come here for, so that is where a first visit lands.
        /// </summary>
        public static FavoritesTab Tab
        {
            get { return (FavoritesTab)Get(TabKey, (int)FavoritesTab.Songs, 0, 2); }
            set { Set(TabKey, (int)value); }
        }

        public static ArtistSort ArtistSort
        {
            get { return (ArtistSort)Get(ArtistSortKey, (int)Services.ArtistSort.Alphabetical, 0, 1); }
            set { Set(ArtistSortKey, (int)value); }
        }

        public static AlbumSort AlbumSort
        {
            get { return (AlbumSort)Get(AlbumSortKey, (int)Services.AlbumSort.Year, 0, 1); }
            set { Set(AlbumSortKey, (int)value); }
        }

        /// <summary>
        /// The Songs tab reuses Core's <see cref="Core.Browsing.SongSort"/> —
        /// the same orderings (and the same menu labels) the Songs view uses,
        /// so the two pages cannot drift apart and the ordering stays
        /// unit-tested in Core.
        /// </summary>
        public static SongSort SongSort
        {
            get { return (SongSort)Get(SongSortKey, (int)Core.Browsing.SongSort.DefaultOrder, 0, 4); }
            set { Set(SongSortKey, (int)value); }
        }

        private static int Get(string key, int fallback, int min, int max)
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            object value;
            if (settings.TryGetValue(key, out value) && value is int stored
                && stored >= min && stored <= max)
            {
                return stored;
            }

            return fallback;
        }

        private static void Set(string key, int value)
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
        }
    }

    /// <summary>The three Favorites scopes (§5 Favorites addition, 2026-07-26).</summary>
    internal enum FavoritesTab
    {
        Artists = 0,
        Albums = 1,
        Songs = 2,
    }

    /// <summary>Sort options offered on the Artists tab.</summary>
    internal enum ArtistSort
    {
        Alphabetical = 0,
        MostAlbums = 1,
    }

    /// <summary>Sort options offered on the Albums tab.</summary>
    internal enum AlbumSort
    {
        Year = 0,
        Alphabetical = 1,
    }

    // The Songs tab's sort options are Core's SongSort/SongSorter, shared with
    // the Songs view — deliberately not duplicated here.
}
