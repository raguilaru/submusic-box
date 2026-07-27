using System;
using System.ComponentModel;
using BoxSubMusic.Core.Browsing;
using Windows.UI.Xaml;

namespace BoxSubMusic.App.Models
{
    /// <summary>
    /// Display model for one artist tile in the Artists browse view
    /// (requirements §7.1). Artwork is the authenticated getCoverArt URL or null
    /// when the server has none.
    ///
    /// Observable since 2026-07-27: getArtists reports only the album count, so
    /// the list row's track count and total play time arrive later, from the
    /// album-list aggregate pass (<see cref="ArtistTotals"/>), and the row
    /// repaints in place when they do.
    /// </summary>
    public sealed class ArtistCard : INotifyPropertyChanged
    {
        private string _metaLabel;

        public ArtistCard(
            string id, string name, string albumCountLabel, Uri artworkUri,
            int albumCount = 0, bool isStarred = false)
        {
            Id = id;
            Name = name;
            AlbumCountLabel = albumCountLabel;
            ArtworkUri = artworkUri;
            AlbumCount = albumCount;
            IsStarred = isStarred;
            // Until (or unless) the totals pass finishes, the row shows exactly
            // what the server told us.
            _metaLabel = albumCountLabel;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; }
        public string Name { get; }

        /// <summary>Pre-formatted subtitle, e.g. "3 albums".</summary>
        public string AlbumCountLabel { get; }

        /// <summary>
        /// The raw count behind <see cref="AlbumCountLabel"/>, so a view can
        /// sort by it (the Favorites "Most Albums" order) without re-mapping
        /// cards — re-mapping would mint fresh cover-art URLs and defeat the
        /// image cache. Optional: pages that never sort leave it unset.
        /// </summary>
        public int AlbumCount { get; }

        /// <summary>Favorite state, for the list row's starred marker.</summary>
        public bool IsStarred { get; }

        /// <summary>
        /// Shows the row's favorite-heart marker, matching the song lists'
        /// convention (checkpoint #4). Cards on pages that never carried the
        /// flag simply never show it.
        /// </summary>
        public Visibility StarredVisibility
        {
            get { return IsStarred ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Uri ArtworkUri { get; }

        /// <summary>
        /// The list row's right-hand line: the album count alone, or — once the
        /// album-list aggregate has completed — album count · tracks · total
        /// play time. Grid tiles keep <see cref="AlbumCountLabel"/>, which a
        /// 200-epx card has room for.
        /// </summary>
        public string MetaLabel
        {
            get { return _metaLabel; }
        }

        /// <summary>
        /// Applies this artist's summed figures. Called only when the aggregate
        /// pass covered the WHOLE album list: a partial sum would understate the
        /// artist, which is worse than showing the album count alone. Must be
        /// called on the UI thread — it raises <see cref="PropertyChanged"/>.
        /// </summary>
        public void ApplyTotals(ArtistTotal total)
        {
            string tracks = total.SongCount == 1 ? "1 track" : total.SongCount + " tracks";
            string label = AlbumCountLabel + "  ·  " + tracks;
            if (total.Duration > TimeSpan.Zero)
            {
                label += "  ·  " + DurationText.Total(total.Duration);
            }

            if (label == _metaLabel)
            {
                return;
            }

            _metaLabel = label;
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(nameof(MetaLabel)));
            }
        }
    }
}
