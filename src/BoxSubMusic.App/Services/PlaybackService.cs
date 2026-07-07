using System;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace BoxSubMusic.App.Services
{
    /// <summary>
    /// Slice 02 spike: single-MediaPlayer playback skeleton.
    /// One app-lifetime MediaPlayer instance is the pattern Xbox background
    /// audio requires (requirement §11); SMTC integration comes for free via
    /// MediaPlayer.CommandManager. Queue management and real sources arrive
    /// with the SubsonicClient in Phase 2.
    /// </summary>
    public sealed class PlaybackService
    {
        private static readonly PlaybackService _instance = new PlaybackService();

        public static PlaybackService Instance
        {
            get { return _instance; }
        }

        public MediaPlayer Player { get; }

        private PlaybackService()
        {
            Player = new MediaPlayer
            {
                AudioCategory = MediaPlayerAudioCategory.Media,
                IsLoopingEnabled = true,
            };
            // SMTC: expose play/pause and next/previous once a queue exists.
            Player.CommandManager.IsEnabled = true;
        }

        public bool IsPlaying
        {
            get { return Player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing; }
        }

        /// <summary>
        /// Plays the bundled looping test tone with SMTC metadata so the
        /// system media flyout shows the app while backgrounded.
        /// </summary>
        public void PlayTestTone()
        {
            var item = new MediaPlaybackItem(
                MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/TestTone.wav")));

            MediaItemDisplayProperties props = item.GetDisplayProperties();
            props.Type = MediaPlaybackType.Music;
            props.MusicProperties.Title = "Background playback spike";
            props.MusicProperties.Artist = "BoxSubMusic";
            item.ApplyDisplayProperties(props);

            Player.Source = item;
            Player.Play();
        }

        public void TogglePlayPause()
        {
            if (Player.Source == null)
            {
                PlayTestTone();
            }
            else if (IsPlaying)
            {
                Player.Pause();
            }
            else
            {
                Player.Play();
            }
        }
    }
}
