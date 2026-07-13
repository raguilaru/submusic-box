namespace BoxSubMusic.App.Models
{
    /// <summary>
    /// Display model for one row of the album track list (requirements §9).
    /// Immutable; the artist is empty when it matches the album artist, so
    /// regular albums stay uncluttered while compilations still credit each
    /// track.
    /// </summary>
    public sealed class TrackRow
    {
        public TrackRow(string id, string number, string title, string artist, string durationLabel)
        {
            Id = id;
            Number = number;
            Title = title;
            Artist = artist;
            DurationLabel = durationLabel;
        }

        public string Id { get; }

        /// <summary>Track number as display text; empty when the server has none.</summary>
        public string Number { get; }

        public string Title { get; }

        /// <summary>Per-track artist; empty when it matches the album artist.</summary>
        public string Artist { get; }

        public string DurationLabel { get; }
    }
}
