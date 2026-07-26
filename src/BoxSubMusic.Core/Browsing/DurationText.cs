using System;
using System.Globalization;

namespace BoxSubMusic.Core.Browsing
{
    /// <summary>
    /// The two duration wordings the app shows: a track's own length and a
    /// collection's total. Lives in Core so every surface that prints a
    /// duration — the album/playlist detail headers, the browse list rows
    /// (§7.1/§7.2), the artist detail counts (§8) — reads identically and the
    /// wording stays unit-tested without a UWP host.
    ///
    /// Culture-invariant on purpose: these are the same fixed shapes the
    /// detail pages have shown since Slice 07 ("4:12", "1 hr 22 min").
    /// </summary>
    public static class DurationText
    {
        /// <summary>
        /// One track's length: <c>m:ss</c>, growing an hours field only when
        /// the track actually runs an hour or more.
        /// </summary>
        public static string Track(TimeSpan duration)
        {
            return duration.TotalHours >= 1
                ? string.Format(
                    CultureInfo.InvariantCulture, "{0}:{1:mm\\:ss}", (int)duration.TotalHours, duration)
                : duration.ToString("m\\:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A collection's total: "1 hr 22 min", or "48 min" under an hour.
        /// Seconds are noise at this scale, so a non-empty collection shorter
        /// than a minute still reads "1 min" rather than "0 min".
        /// </summary>
        public static string Total(TimeSpan duration)
        {
            int hours = (int)duration.TotalHours;
            if (hours >= 1)
            {
                return string.Format(
                    CultureInfo.InvariantCulture, "{0} hr {1} min", hours, duration.Minutes);
            }

            return string.Format(
                CultureInfo.InvariantCulture, "{0} min", Math.Max(1, duration.Minutes));
        }
    }
}
