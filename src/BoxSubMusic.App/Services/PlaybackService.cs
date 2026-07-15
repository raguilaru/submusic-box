using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoxSubMusic.Core.Playback;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.UI.Core;

namespace BoxSubMusic.App.Services
{
    /// <summary>
    /// Playback engine (Slice 10, requirements §10/§11): one app-lifetime
    /// MediaPlayer — the Xbox background-audio pattern proven in the Slice 02
    /// spike — now fed by a MediaPlaybackList mirrored from the Core
    /// <see cref="PlayQueue"/> (design:
    /// docs/architecture/30-playback-architecture.md). The list buys gapless
    /// playback with next-track prefetch and native SMTC next/previous via
    /// CommandManager; the queue owns order, shuffle mapping and repeat
    /// state. Must be first touched on the UI thread (it captures that
    /// dispatcher); <see cref="Changed"/> is always raised on the UI thread.
    /// </summary>
    public sealed class PlaybackService
    {
        // 4-minute cap on the scrobble threshold (standard Last.fm-style
        // convention: half the track or 4 minutes, whichever is less).
        private static readonly TimeSpan ScrobbleCap = TimeSpan.FromMinutes(4);

        // In-app Previous restarts the current track when it is already this
        // far in; earlier than this it goes to the previous track.
        private static readonly TimeSpan PreviousRestartThreshold = TimeSpan.FromSeconds(5);

        private const int ThumbnailArtSize = 300;

        private static PlaybackService _instance;

        public static PlaybackService Instance =>
            _instance ?? (_instance = new PlaybackService());

        private readonly CoreDispatcher _dispatcher;
        private readonly Dictionary<Song, MediaPlaybackItem> _songToItem =
            new Dictionary<Song, MediaPlaybackItem>();
        private readonly Dictionary<MediaPlaybackItem, Song> _itemToSong =
            new Dictionary<MediaPlaybackItem, Song>();

        private MediaPlaybackList _list;

        // Scrobble bookkeeping, touched from the media thread (PositionChanged).
        private Song _scrobbleSong;
        private bool _scrobbleSent;

        // Consecutive open failures; when a whole queue's worth fails in a row
        // (unreachable server), stop instead of skipping forever.
        private int _consecutiveFailures;

        public MediaPlayer Player { get; }

        public PlayQueue Queue { get; } = new PlayQueue();

        /// <summary>
        /// Coarse change signal for the UI (queue content, current track,
        /// play/pause state, shuffle/repeat), always on the UI thread.
        /// Consumers re-read the properties they display.
        /// </summary>
        public event Action Changed;

        public MediaPlaybackSession Session => Player.PlaybackSession;

        public bool IsPlaying => Session.PlaybackState == MediaPlaybackState.Playing;

        private PlaybackService()
        {
            _dispatcher = Windows.UI.Xaml.Window.Current.Dispatcher;

            Player = new MediaPlayer
            {
                AudioCategory = MediaPlayerAudioCategory.Media,
            };
            Player.CommandManager.IsEnabled = true;
            // Keep the SMTC Previous button consistent with the in-app one
            // (restart the track when it is already underway).
            Player.CommandManager.PreviousReceived += OnCommandManagerPreviousReceived;
            Player.MediaEnded += OnMediaEnded;
            Player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
            Player.PlaybackSession.PositionChanged += OnPositionChanged;

            Queue.Changed += () => Changed?.Invoke();
        }

        /// <summary>
        /// Replaces the queue and starts playing (§10: Play actions replace,
        /// never append). With <paramref name="shuffle"/> the pool order is
        /// randomized and <paramref name="startIndex"/> is ignored (§8).
        /// </summary>
        public void Play(IReadOnlyList<Song> tracks, int startIndex, bool shuffle, QueueContext context)
        {
            Queue.Replace(tracks, startIndex, shuffle, context);
            if (Queue.IsEmpty)
            {
                Stop();
                return;
            }

            RebuildList();
            Player.Play();
        }

        public void TogglePlayPause()
        {
            if (_list == null)
            {
                return;
            }

            if (IsPlaying)
            {
                Player.Pause();
            }
            else
            {
                Player.Play();
            }
        }

        public void Next()
        {
            _list?.MoveNext();
        }

        public void Previous()
        {
            if (_list == null)
            {
                return;
            }

            if (Session.Position > PreviousRestartThreshold)
            {
                Session.Position = TimeSpan.Zero;
            }
            else
            {
                _list.MovePrevious();
            }
        }

        public void SetShuffle(bool enabled)
        {
            Queue.SetShuffle(enabled);
            ApplyShuffleToList();
        }

        public void CycleRepeat()
        {
            Queue.CycleRepeat();
            if (_list != null)
            {
                // All = the list's native wrap-around; One is handled manually
                // on end-of-stream (the list has no per-item repeat).
                _list.AutoRepeatEnabled = Queue.RepeatMode == RepeatMode.All;
            }
        }

        /// <summary>Sign-out / hard stop: silence, empty queue, bar hides.</summary>
        public void Stop()
        {
            Player.Pause();
            DetachList();
            Player.Source = null;
            _scrobbleSong = null;
            Queue.Clear();
        }

        // --- Queue → MediaPlaybackList mirror ---------------------------------

        private void RebuildList()
        {
            DetachList();

            var client = AppSession.Client;
            if (client == null)
            {
                Queue.Clear();
                return;
            }

            var list = new MediaPlaybackList
            {
                // Bound how many opened (buffered) items the list keeps alive:
                // the Xbox background memory budget is strict (§11).
                MaxPlayedItemsToKeepOpen = 2,
                AutoRepeatEnabled = Queue.RepeatMode == RepeatMode.All,
            };

            // Items always sit in the list in ORIGINAL order; shuffle is
            // expressed via SetShuffledItems below, so toggling shuffle later
            // never interrupts the current track.
            foreach (Song song in Queue.Tracks)
            {
                var item = CreateItem(client, song);
                _songToItem[song] = item;
                _itemToSong[item] = song;
                list.Items.Add(item);
            }

            list.StartingItem = _songToItem[Queue.CurrentTrack];
            list.CurrentItemChanged += OnCurrentItemChanged;
            list.ItemFailed += OnItemFailed;
            _list = list;
            ApplyShuffleToList();

            _consecutiveFailures = 0;
            _scrobbleSong = Queue.CurrentTrack;
            _scrobbleSent = false;

            Player.Source = list;
        }

        private void DetachList()
        {
            if (_list != null)
            {
                _list.CurrentItemChanged -= OnCurrentItemChanged;
                _list.ItemFailed -= OnItemFailed;
                _list = null;
            }

            _songToItem.Clear();
            _itemToSong.Clear();
        }

        private void ApplyShuffleToList()
        {
            if (_list == null)
            {
                return;
            }

            if (Queue.ShuffleEnabled)
            {
                _list.ShuffleEnabled = true;
                var order = new List<MediaPlaybackItem>(Queue.PlayOrder.Count);
                foreach (Song song in Queue.PlayOrder)
                {
                    order.Add(_songToItem[song]);
                }

                _list.SetShuffledItems(order);
            }
            else
            {
                _list.ShuffleEnabled = false;
            }
        }

        private MediaPlaybackItem CreateItem(SubsonicApiClient client, Song song)
        {
            var item = new MediaPlaybackItem(
                MediaSource.CreateFromUri(client.GetStreamUri(song.Id)));

            // SMTC metadata (§11): shown by the system media flyout and on
            // whatever the console casts it to.
            MediaItemDisplayProperties props = item.GetDisplayProperties();
            props.Type = MediaPlaybackType.Music;
            props.MusicProperties.Title = song.Title;
            props.MusicProperties.Artist = song.Artist ?? string.Empty;
            props.MusicProperties.AlbumTitle = song.Album ?? string.Empty;
            if (song.CoverArtId != null)
            {
                props.Thumbnail = RandomAccessStreamReference.CreateFromUri(
                    client.GetCoverArtUri(song.CoverArtId, ThumbnailArtSize));
            }

            item.ApplyDisplayProperties(props);
            return item;
        }

        // --- Player events (media thread unless noted) -------------------------

        private void OnCurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
        {
            // Repeat One: the list has advanced past the ended track; move
            // straight back. The re-entrant CurrentItemChanged this causes is
            // harmless (NotifyAdvanced is index-idempotent).
            if (Queue.RepeatMode == RepeatMode.One
                && args.Reason == MediaPlaybackItemChangedReason.EndOfStream
                && args.OldItem != null)
            {
                int index = sender.Items.IndexOf(args.OldItem);
                if (index >= 0)
                {
                    sender.MoveTo((uint)index);
                    return;
                }
            }

            MediaPlaybackItem newItem = args.NewItem;
            Song song = null;
            if (newItem != null)
            {
                _itemToSong.TryGetValue(newItem, out song);
            }

            // Scrobble bookkeeping stays on this thread (PositionChanged
            // consumes it here too).
            _scrobbleSong = song;
            _scrobbleSent = false;

            if (song != null)
            {
                // "Now playing" report (submission=false): keeps the server's
                // now-playing panel live. The play-count submission still
                // waits for the min(50 %, 4 min) threshold. Added after
                // checkpoint #4: without it Navidrome's UI shows nothing
                // until a track passes the threshold, which read as broken.
                var client = AppSession.Client;
                if (client != null)
                {
                    _ = ScrobbleQuietlyAsync(client, song.Id, submission: false);
                }

                _ = RunOnUiAsync(() => Queue.NotifyAdvanced(song));
            }
        }

        private void OnItemFailed(MediaPlaybackList sender, MediaPlaybackItemFailedEventArgs args)
        {
            // The list skips a failed item on its own; only guard against a
            // whole queue of failures (server unreachable) looping forever.
            int failures = ++_consecutiveFailures;
            if (failures >= sender.Items.Count)
            {
                _ = RunOnUiAsync(Stop);
            }
        }

        private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            if (sender.PlaybackState == MediaPlaybackState.Playing)
            {
                // A track opened and is audibly playing: the failure streak,
                // if any, is over.
                _consecutiveFailures = 0;
            }

            _ = RunOnUiAsync(() => Changed?.Invoke());
        }

        private void OnPositionChanged(MediaPlaybackSession sender, object args)
        {
            // Cheap threshold check on the media thread so scrobbling also
            // works while the app is backgrounded (§11); the UI's elapsed
            // display uses its own DispatcherTimer instead of this event.
            Song song = _scrobbleSong;
            if (song == null || _scrobbleSent)
            {
                return;
            }

            TimeSpan threshold = song.Duration > TimeSpan.Zero
                ? TimeSpan.FromTicks(Math.Min(song.Duration.Ticks / 2, ScrobbleCap.Ticks))
                : ScrobbleCap;
            if (sender.Position < threshold)
            {
                return;
            }

            _scrobbleSent = true;
            var client = AppSession.Client;
            if (client != null)
            {
                _ = ScrobbleQuietlyAsync(client, song.Id);
            }
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            // The whole list finished (AutoRepeatEnabled off). Repeat One on
            // the LAST track lands here instead of CurrentItemChanged.
            if (Queue.RepeatMode == RepeatMode.One && _list?.CurrentItem != null)
            {
                _scrobbleSent = false;
                sender.PlaybackSession.Position = TimeSpan.Zero;
                sender.Play();
                return;
            }

            _ = RunOnUiAsync(() => Changed?.Invoke());
        }

        private void OnCommandManagerPreviousReceived(
            MediaPlaybackCommandManager sender,
            MediaPlaybackCommandManagerPreviousReceivedEventArgs args)
        {
            args.Handled = true;
            Previous();
        }

        private static async Task ScrobbleQuietlyAsync(
            SubsonicApiClient client, string songId, bool submission = true)
        {
            try
            {
                await client.ScrobbleAsync(songId, submission);
            }
            catch (Exception)
            {
                // Play-count reporting is telemetry, not UX (§16 decision
                // 2026-07-15): a failed scrobble must never surface.
            }
        }

        private IAsyncAction RunOnUiAsync(Action action) =>
            _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
    }
}
