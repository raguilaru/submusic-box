using System;
using System.Collections.Generic;
using BoxSubMusic.App.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace BoxSubMusic.App.Controls
{
    /// <summary>
    /// One Home carousel row (requirements §6.2–§6.4). The host page drives
    /// the state machine explicitly: ShowLoading → ShowAlbums / ShowError;
    /// an empty result collapses into the empty state. Simple method calls
    /// instead of bindings, matching the app's code-behind style.
    /// </summary>
    public sealed partial class AlbumCarousel : UserControl
    {
        /// <summary>Raised when the user asks to retry after an error.</summary>
        public event RoutedEventHandler RetryRequested;

        public AlbumCarousel()
        {
            InitializeComponent();
        }

        /// <summary>Row heading, set from XAML.</summary>
        public string Title
        {
            get { return TitleText.Text; }
            set { TitleText.Text = value ?? string.Empty; }
        }

        /// <summary>Copy for the empty state, set from XAML per carousel.</summary>
        public string EmptyMessage { get; set; } = "Nothing here yet.";

        public void ShowLoading()
        {
            SetState(loading: true, list: false, empty: false, error: false);
        }

        public void ShowAlbums(IReadOnlyList<AlbumCard> albums)
        {
            if (albums == null || albums.Count == 0)
            {
                EmptyText.Text = EmptyMessage;
                SetState(loading: false, list: false, empty: true, error: false);
                return;
            }

            AlbumList.ItemsSource = albums;
            SetState(loading: false, list: true, empty: false, error: false);
        }

        public void ShowError(string message)
        {
            ErrorText.Text = message ?? "Something went wrong.";
            SetState(loading: false, list: false, empty: false, error: true);
        }

        private void SetState(bool loading, bool list, bool empty, bool error)
        {
            LoadingRing.IsActive = loading;
            LoadingRing.Visibility = ToVisibility(loading);
            AlbumList.Visibility = ToVisibility(list);
            EmptyText.Visibility = ToVisibility(empty);
            ErrorPanel.Visibility = ToVisibility(error);
        }

        private static Visibility ToVisibility(bool visible)
        {
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            RetryRequested?.Invoke(this, e);
        }
    }
}
