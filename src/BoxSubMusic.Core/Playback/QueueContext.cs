using System;

namespace BoxSubMusic.Core.Playback
{
    /// <summary>
    /// Lightweight originating-context metadata for a play queue: what the
    /// user pressed Play on. Carried now so the §6.1 resume card (Slice 05b)
    /// can persist and show it later; nothing reads it in Slice 10.
    /// </summary>
    public sealed class QueueContext
    {
        public QueueContext(string displayName, string? coverArtId)
        {
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            CoverArtId = coverArtId;
        }

        /// <summary>E.g. "Abbey Road" or "The Beatles".</summary>
        public string DisplayName { get; }

        /// <summary>Artwork reference for the resume card; null when the source has none.</summary>
        public string? CoverArtId { get; }
    }
}
