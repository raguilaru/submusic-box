using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;

namespace BoxSubMusic.App.Models
{
    /// <summary>
    /// Display model for one playlist tile (requirements §7.4). The tile artwork
    /// is composed client-side from the playlist's constituent album covers and
    /// loaded lazily per card (getPlaylists does not carry the track list), so
    /// this model is observable: the browse page fills in the covers after the
    /// grid is already on screen. A playlist that spans multiple albums shows a
    /// composite that adapts so no cell is ever empty (2 covers = side-by-side
    /// halves, 3 = one full-height + two stacked, 4 = 2×2); one that shares a
    /// single album collapses to one cover; while loading (or when artless) the
    /// placeholder shows through.
    /// </summary>
    public sealed class PlaylistCard : INotifyPropertyChanged
    {
        private Uri _singleCover;
        private Uri _cover0;
        private Uri _cover1;
        private Uri _cover2;
        private Uri _cover3;
        private int _cover0RowSpan = 1;
        private int _cover1RowSpan = 1;
        private Visibility _singleVisibility = Visibility.Collapsed;
        private Visibility _compositeVisibility = Visibility.Collapsed;

        public PlaylistCard(string id, string name, string subtitle)
        {
            Id = id;
            Name = name;
            Subtitle = subtitle;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; }
        public string Name { get; }
        public string Subtitle { get; }

        /// <summary>Full-tile cover used when the playlist shares one album.</summary>
        public Uri SingleCover { get => _singleCover; private set => Set(ref _singleCover, value); }

        /// <summary>Composite slots: 0 = top-left, 1 = top-right, 2 = bottom-left, 3 = bottom-right.</summary>
        public Uri Cover0 { get => _cover0; private set => Set(ref _cover0, value); }
        public Uri Cover1 { get => _cover1; private set => Set(ref _cover1, value); }
        public Uri Cover2 { get => _cover2; private set => Set(ref _cover2, value); }
        public Uri Cover3 { get => _cover3; private set => Set(ref _cover3, value); }

        /// <summary>Row spans letting covers stretch to fill the tile (see SetCovers).</summary>
        public int Cover0RowSpan { get => _cover0RowSpan; private set => Set(ref _cover0RowSpan, value); }
        public int Cover1RowSpan { get => _cover1RowSpan; private set => Set(ref _cover1RowSpan, value); }

        public Visibility SingleVisibility { get => _singleVisibility; private set => Set(ref _singleVisibility, value); }
        public Visibility CompositeVisibility { get => _compositeVisibility; private set => Set(ref _compositeVisibility, value); }

        /// <summary>
        /// Applies the resolved composite covers (0..4, in playlist order). Zero
        /// leaves the placeholder showing; one uses the single-cover layout; two
        /// or more fill the composite so no cell is ever empty: 2 covers render
        /// as full-height halves side by side, 3 as one full-height cover plus
        /// two stacked, 4 as a 2×2. Safe to call on the UI thread after lazy load.
        /// </summary>
        public void SetCovers(IReadOnlyList<Uri> covers)
        {
            covers = covers ?? Array.Empty<Uri>();
            if (covers.Count == 1)
            {
                SingleCover = covers[0];
                SingleVisibility = Visibility.Visible;
                CompositeVisibility = Visibility.Collapsed;
                return;
            }

            if (covers.Count >= 2)
            {
                Cover0 = covers[0];
                Cover1 = covers[1];
                // 2 covers: both columns span the full height. 3 covers: the
                // first spans the left column, the other two stack on the
                // right (slot 3 = bottom-right; bottom-left stays covered by
                // the span). 4 covers: plain 2×2.
                Cover0RowSpan = covers.Count <= 3 ? 2 : 1;
                Cover1RowSpan = covers.Count == 2 ? 2 : 1;
                Cover2 = covers.Count > 3 ? covers[2] : null;
                Cover3 = covers.Count > 3 ? covers[3]
                       : covers.Count == 3 ? covers[2]
                       : null;
                CompositeVisibility = Visibility.Visible;
                SingleVisibility = Visibility.Collapsed;
                return;
            }

            // No artwork: leave the placeholder visible.
            SingleVisibility = Visibility.Collapsed;
            CompositeVisibility = Visibility.Collapsed;
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
