using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.Storage;

namespace BoxSubMusic.App.Services
{
    /// <summary>
    /// The one place the app builds cover-art URLs (§11, 2026-08-13).
    ///
    /// It exists because of a memory finding, not a tidiness one.
    /// <c>SubsonicApiClient.GetCoverArtUri</c> goes through the same request
    /// builder every other endpoint does, and in token auth mode that builder
    /// mints a FRESH RANDOM SALT per call — so two requests for the same cover
    /// at the same size produce two different URLs. XAML's decoded-image cache
    /// is keyed by URI and process-wide, and the HTTP cache is keyed by URL, so
    /// unique URLs mean:
    ///
    /// - neither cache can ever hit, and every appearance of a cover is a fresh
    ///   download and a fresh decode;
    /// - the framework's image cache accumulates an entry per request that
    ///   nothing will ever look up again, in native memory the app does not own
    ///   and cannot release. Dropping the whole visual tree reclaimed ~1 MB on
    ///   device precisely because the cost is not charged to the tree.
    ///
    /// Two things follow, and this class does both:
    ///
    /// 1. **URLs are minted once per (cover, tier) and reused** for the life of
    ///    the session, so both caches behave the way the platform intends.
    /// 2. **Display sizes are rounded up to a short tier ladder**, so the same
    ///    album cover is one cached image across the grids, the carousels and
    ///    the resume card instead of one per call site. Before this the app
    ///    asked for a single cover at up to eight distinct sizes.
    ///
    /// <see cref="Suppressed"/> is the measurement switch for the same
    /// investigation: with it on, every call here returns null, so the app runs
    /// with no artwork anywhere. Browsing the same path with it on and off
    /// bounds artwork's total contribution to the background footprint in two
    /// runs — the question no amount of reading the code settles.
    ///
    /// Note that the DECODE size is deliberately not this class's business.
    /// Callers keep decoding at their own display box (the standing rule from
    /// 2026-08-12: decode size equals the display box, because layout scaling
    /// is off and 1 epx is 1 physical pixel). A tier only decides which URL —
    /// and therefore which shared cache entry — the caller draws from.
    /// </summary>
    internal static class CoverArt
    {
        private const string SuppressArtworkKey = "diagnostics.suppressArtwork";

        /// <summary>
        /// Request sizes the app will ask a server for, smallest first. A
        /// display box is served by the first tier that is at least as large,
        /// so nearby sizes collapse onto one shared URL: every grid card (200),
        /// carousel tile (220) and resume card (160) now shares tier 220, and
        /// the detail headers (320) share tier 320 with the SMTC thumbnail
        /// (300). Tiers are real display sizes rather than round numbers so no
        /// tier downloads more than the largest box it serves.
        /// </summary>
        private static readonly int[] RequestTiers = { 100, 220, 320, 400 };

        /// <summary>
        /// URL-cache ceiling. Entries are small but not free — a cover URL
        /// carries the auth query, so an entry costs roughly 650 bytes with its
        /// key. At this cap the cache is about 1.3 MB, which is a good trade
        /// against the image-cache entries it prevents, and a library large
        /// enough to reach the cap in one session gets a clean start rather
        /// than an eviction policy nothing here needs.
        /// </summary>
        private const int MaxCachedUris = 2000;

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Uri> Cache = new Dictionary<string, Uri>();

        // Read from LocalSettings once; the hot path runs per card.
        private static bool? _suppressed;

        /// <summary>
        /// Diagnostic switch (§11 investigation): when true the app requests no
        /// artwork at all. Persisted so it survives the relaunch the
        /// measurement needs — pages build their cards on navigation, but the
        /// bar and the overlay only rebuild artwork on a track change, so a
        /// clean comparison starts from launch.
        /// </summary>
        public static bool Suppressed
        {
            get
            {
                if (_suppressed == null)
                {
                    object value;
                    _suppressed =
                        ApplicationData.Current.LocalSettings.Values.TryGetValue(SuppressArtworkKey, out value)
                        && value is bool flag
                        && flag;
                }

                return _suppressed.Value;
            }

            set
            {
                _suppressed = value;
                ApplicationData.Current.LocalSettings.Values[SuppressArtworkKey] = value;
                Reset();
            }
        }

        /// <summary>
        /// The cover-art URL for a display box of <paramref name="displaySize"/>
        /// epx, or null when there is no artwork to show — no cover id, no
        /// active server, or artwork suppressed. Callers pass the size of the
        /// box they draw into and let the ladder pick the request size.
        /// </summary>
        public static Uri UriFor(string coverArtId, int displaySize)
        {
            if (string.IsNullOrEmpty(coverArtId) || Suppressed)
            {
                return null;
            }

            var client = AppSession.Client;
            if (client == null)
            {
                return null;
            }

            int tier = RequestTier(displaySize);
            string key = coverArtId + "|" + tier.ToString(CultureInfo.InvariantCulture);

            lock (Gate)
            {
                Uri cached;
                if (Cache.TryGetValue(key, out cached))
                {
                    return cached;
                }

                Uri uri;
                try
                {
                    uri = client.GetCoverArtUri(coverArtId, tier);
                }
                catch (Exception)
                {
                    // A malformed cover id is a missing image, never a crash.
                    return null;
                }

                if (Cache.Count >= MaxCachedUris)
                {
                    Cache.Clear();
                }

                Cache[key] = uri;
                return uri;
            }
        }

        /// <summary>
        /// Drops every minted URL. Mandatory on any change of server or user:
        /// a cover URL embeds the credentials it was built with, so a stale one
        /// would authenticate as the previous login.
        /// </summary>
        public static void Reset()
        {
            lock (Gate)
            {
                Cache.Clear();
            }
        }

        /// <summary>Describes the current mode for the diagnostics log.</summary>
        public static string DescribeMode()
        {
            return Suppressed
                ? "artwork=SUPPRESSED (diagnostic; no covers are requested)"
                : "artwork=on";
        }

        private static int RequestTier(int displaySize)
        {
            for (int i = 0; i < RequestTiers.Length; i++)
            {
                if (displaySize <= RequestTiers[i])
                {
                    return RequestTiers[i];
                }
            }

            // Larger than anything the app draws today: ask for exactly the box.
            return displaySize;
        }
    }
}
