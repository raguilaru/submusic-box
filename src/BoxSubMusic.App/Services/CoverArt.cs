using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

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

            // Decoded covers were fetched with those credentials too, and a
            // different user's library has different artwork behind the same
            // ids.
            DropBitmaps();
        }

        // --- App-owned bitmap cache (§11, 2026-08-13) --------------------------
        //
        // Why the app decodes its own covers instead of letting XAML do it from
        // a UriSource: a BitmapImage built from a URI is cached by the
        // framework in a PROCESS-WIDE, URI-keyed cache that the app cannot
        // reach. That is why releasing the entire visual tree reclaimed 0 MB on
        // every console run of this investigation — the decoded images were
        // never charged to the tree in the first place.
        //
        // Decoding from a stream produces a BitmapImage with no URI, so the
        // framework caches nothing and the only reference is the one held here.
        // "Drop the artwork" therefore becomes an operation the app can
        // actually perform (see DropBitmaps), which is the whole point:
        // browsing adds 27+ MB of covers and pushes a session from the ~143 MB
        // that survives backgrounding into the 183–194 MB band that does not.

        /// <summary>
        /// Decoded-bitmap budget. Covers are held at roughly
        /// width x width x 4 bytes, so this is about 100 grid cards at 200 epx
        /// — comfortably more than any one screen needs, and all of it is
        /// released on the way to the background.
        /// </summary>
        private const long MaxCachedBitmapBytes = 24L * 1024 * 1024;

        /// <summary>Concurrent cover downloads, matching the bounded fan-out
        /// used elsewhere (§16 2026-07-19) so a grid cannot flood the server.</summary>
        private static readonly SemaphoreSlim DownloadGate = new SemaphoreSlim(4, 4);

        private static readonly object BitmapGate = new object();
        private static readonly Dictionary<string, BitmapImage> Bitmaps =
            new Dictionary<string, BitmapImage>();
        private static readonly Dictionary<string, long> BitmapBytes =
            new Dictionary<string, long>();
        private static readonly LinkedList<string> BitmapOrder = new LinkedList<string>();
        private static long _bitmapBytes;

        // Requests in flight, so two containers asking for the same cover
        // download it once. UI-thread only (the attached property drives it).
        private static readonly Dictionary<string, Task<BitmapImage>> InFlight =
            new Dictionary<string, Task<BitmapImage>>();

        /// <summary>
        /// The decoded cover for <paramref name="uri"/> at
        /// <paramref name="decodeWidth"/> epx, or null when it cannot be
        /// fetched — callers show their placeholder, exactly as they did when a
        /// UriSource failed. UI thread (it creates a BitmapImage).
        /// </summary>
        public static Task<BitmapImage> GetBitmapAsync(Uri uri, int decodeWidth)
        {
            if (uri == null || decodeWidth <= 0)
            {
                return Task.FromResult<BitmapImage>(null);
            }

            string key = uri.AbsoluteUri + "|" + decodeWidth.ToString(CultureInfo.InvariantCulture);

            lock (BitmapGate)
            {
                BitmapImage cached;
                if (Bitmaps.TryGetValue(key, out cached))
                {
                    Touch(key);
                    return Task.FromResult(cached);
                }
            }

            Task<BitmapImage> pending;
            if (InFlight.TryGetValue(key, out pending))
            {
                return pending;
            }

            Task<BitmapImage> load = LoadBitmapAsync(key, uri, decodeWidth);
            InFlight[key] = load;
            return load;
        }

        private static async Task<BitmapImage> LoadBitmapAsync(string key, Uri uri, int decodeWidth)
        {
            try
            {
                byte[] bytes;
                await DownloadGate.WaitAsync().ConfigureAwait(true);
                try
                {
                    bytes = await AppSession.Http.GetByteArrayAsync(uri).ConfigureAwait(true);
                }
                finally
                {
                    DownloadGate.Release();
                }

                var image = new BitmapImage { DecodePixelWidth = decodeWidth };
                using (var stream = new InMemoryRandomAccessStream())
                {
                    await stream.WriteAsync(bytes.AsBuffer());
                    stream.Seek(0);
                    await image.SetSourceAsync(stream);
                }

                Store(key, image, decodeWidth);
                return image;
            }
            catch (Exception)
            {
                // A missing or unreachable cover is a placeholder, never an
                // error surface — same contract as a failed UriSource.
                return null;
            }
            finally
            {
                InFlight.Remove(key);
            }
        }

        private static void Store(string key, BitmapImage image, int decodeWidth)
        {
            // Covers are square in every layout the app uses; the estimate only
            // has to be good enough to bound the cache.
            long bytes = (long)decodeWidth * decodeWidth * 4;

            lock (BitmapGate)
            {
                if (!Bitmaps.ContainsKey(key))
                {
                    BitmapOrder.AddLast(key);
                }

                Bitmaps[key] = image;
                BitmapBytes[key] = bytes;
                _bitmapBytes += bytes;
                Touch(key);

                while (_bitmapBytes > MaxCachedBitmapBytes && BitmapOrder.Count > 1)
                {
                    string oldest = BitmapOrder.First.Value;
                    BitmapOrder.RemoveFirst();
                    Bitmaps.Remove(oldest);
                    long evicted;
                    if (BitmapBytes.TryGetValue(oldest, out evicted))
                    {
                        _bitmapBytes -= evicted;
                        BitmapBytes.Remove(oldest);
                    }
                }
            }
        }

        /// <summary>Moves a key to the most-recently-used end. Caller holds
        /// <see cref="BitmapGate"/>.</summary>
        private static void Touch(string key)
        {
            LinkedListNode<string> node = BitmapOrder.Find(key);
            if (node != null)
            {
                BitmapOrder.Remove(node);
            }

            BitmapOrder.AddLast(key);
        }

        /// <summary>
        /// Releases every cached cover. Called on the way to the background and
        /// again under memory pressure (§11). Images currently assigned to an
        /// on-screen element survive until that element goes away, so this does
        /// not blank the UI — it only drops the app's own retention, which is
        /// what lets the visual-tree release actually reclaim something.
        ///
        /// Safe from any thread: it drops references, and the objects
        /// themselves are only touched on the UI thread.
        /// </summary>
        public static void DropBitmaps()
        {
            lock (BitmapGate)
            {
                Bitmaps.Clear();
                BitmapBytes.Clear();
                BitmapOrder.Clear();
                _bitmapBytes = 0;
            }
        }

        /// <summary>Cached-cover total, for the diagnostics log.</summary>
        public static long CachedBitmapBytes
        {
            get
            {
                lock (BitmapGate)
                {
                    return _bitmapBytes;
                }
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
