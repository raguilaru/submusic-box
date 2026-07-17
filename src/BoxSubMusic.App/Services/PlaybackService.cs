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

        // §6.1 resume: one-shot seek applied when the first item after a
        // Resume() opens (a MediaPlaybackList position can only be set once
        // its media is open). Pinned to the track it was saved for, so a
        // failed-and-skipped first item cannot start the NEXT track mid-song.
        // Media-thread consumed.
        private TimeSpan? _pendingSeek;
        private Song _pendingSeekSong;

        // Last position written to the resume scalars, media thread; bounds
        // the LocalSettings write rate.
        private TimeSpan _lastSavedPosition;

        private static readonly TimeSpan PositionSaveInterval = TimeSpan.FromSeconds(10);

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
            Player.MediaOpened += OnMediaOpened;
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
            PersistQueueStructure(TimeSpan.Zero);
            Player.Play();
        }

        /// <summary>
        /// Restores a persisted queue and starts playing at its saved track
        /// and position (§6.1 resume card — decision 2026-07-16: Play on the
        /// card plays immediately; launch itself never restores anything).
        /// The in-track seek is applied once the stream opens; if the stream
        /// is not seekable the track simply starts over.
        /// </summary>
        public void Resume(ResumeState state)
        {
            Queue.Restore(state);
            RebuildList();
            if (state.PositionSeconds > 0)
            {
                _pendingSeekSong = Queue.CurrentTrack;
                _pendingSeek = TimeSpan.FromSeconds(state.PositionSeconds);
            }

            // The structure is already on disk (it was just loaded), but the
            // rebuilt list re-persists so the scalars re-baseline too.
            PersistQueueStructure(TimeSpan.FromSeconds(state.PositionSeconds));
            Player.Play();
        }

        /// <summary>
        /// Queue edit (context-menu "Play next", 2026-07-17): the song plays
        /// directly after the current track; playback is not interrupted.
        /// With no queue there is nothing to insert into — the song plays
        /// immediately as its own queue (confirmed with Rafael: never build
        /// invisible/silent state).
        /// </summary>
        public void PlayNext(Song song)
        {
            if (song != null)
            {
                AddSongs(new[] { song }, insertNext: true,
                    emptyQueueContext: new QueueContext(song.Title, song.CoverArtId));
            }
        }

        /// <summary>
        /// Queue edit (context-menu "Add to queue", 2026-07-17): the song is
        /// appended to the end of the play order. Empty queue: same
        /// play-immediately rule as <see cref="PlayNext(Song)"/>.
        /// </summary>
        public void AddToQueue(Song song)
        {
            if (song != null)
            {
                AddSongs(new[] { song }, insertNext: false,
                    emptyQueueContext: new QueueContext(song.Title, song.CoverArtId));
            }
        }

        /// <summary>
        /// Container form of "Play next" (2026-07-18): the whole block —
        /// an album, a playlist, an artist pool — is inserted directly after
        /// the current track in its given order. With no active queue the
        /// block simply plays from its first song under
        /// <paramref name="context"/> (the no-invisible-state rule).
        /// </summary>
        public void PlayNext(IReadOnlyList<Song> songs, QueueContext context)
        {
            AddSongs(songs, insertNext: true, emptyQueueContext: context);
        }

        /// <summary>Container form of "Add to queue" (2026-07-18): appends the block.</summary>
        public void AddToQueue(IReadOnlyList<Song> songs, QueueContext context)
        {
            AddSongs(songs, insertNext: false, emptyQueueContext: context);
        }

        private void AddSongs(IReadOnlyList<Song> songs, bool insertNext, QueueContext emptyQueueContext)
        {
            if (songs == null || songs.Count == 0)
            {
                return;
            }

            if (_list == null || Queue.IsEmpty)
            {
                Play(songs, 0, shuffle: false, context: emptyQueueContext);
                return;
            }

            var client = AppSession.Client;
            if (client == null)
            {
                return;
            }

            // The queue and the item maps key tracks by reference identity;
            // an instance that is already queued (e.g. "Play next" on the
            // playing album's own row) — or repeated within this very batch —
            // needs a distinct instance.
            var added = new List<Song>(songs.Count);
            var seen = new HashSet<Song>();
            foreach (Song song in songs)
            {
                added.Add(_songToItem.ContainsKey(song) || !seen.Add(song)
                    ? CloneSong(song)
                    : song);
            }

            if (insertNext)
            {
                Queue.InsertNext(added);
            }
            else
            {
                Queue.Append(added);
            }

            // Mirror: list items always sit in original (Tracks) order, so
            // each item goes wherever the queue put its track there (the
            // block occupies contiguous ascending indices, so inserting in
            // block order lands correctly); under shuffle the play order is
            // re-expressed via SetShuffledItems (console-verified not to
            // interrupt the current track).
            foreach (Song song in added)
            {
                var item = CreateItem(client, song);
                _songToItem[song] = item;
                _itemToSong[item] = song;
                _list.Items.Insert(IndexOfInTracks(song), item);
            }

            if (Queue.ShuffleEnabled)
            {
                ApplyShuffleToList();
            }

            PersistQueueStructure(Session.Position);
        }

        private int IndexOfInTracks(Song song)
        {
            IReadOnlyList<Song> tracks = Queue.Tracks;
            for (int i = 0; i < tracks.Count; i++)
            {
                if (ReferenceEquals(tracks[i], song))
                {
                    return i;
                }
            }

            return tracks.Count - 1;
        }

        private static Song CloneSong(Song song)
        {
            return new Song(
                song.Id,
                song.Title,
                song.Artist,
                song.ArtistId,
                song.Album,
                song.AlbumId,
                song.CoverArtId,
                song.Duration,
                song.Track,
                song.DiscNumber,
                song.Year,
                song.Format,
                song.BitRate,
                song.ContentType,
                song.IsStarred);
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
            PersistQueueStructure(Session.Position);
        }

        /// <summary>
        /// Jumps playback to an arbitrary queued track (§10: the expanded
        /// queue supports direct jumping) without rebuilding the list —
        /// shuffle order, repeat state and prefetch all survive. Jumping to
        /// the track that is already current restarts it.
        /// </summary>
        public void JumpTo(Song song)
        {
            if (_list == null || song == null || !_songToItem.TryGetValue(song, out MediaPlaybackItem item))
            {
                return;
            }

            if (ReferenceEquals(song, Queue.CurrentTrack))
            {
                Session.Position = TimeSpan.Zero;
                Player.Play();
                return;
            }

            // MoveTo's docs leave the index space unstated when shuffle is
            // on (Items vs ShuffledItems). Try the shuffled-order index
            // first (we set ShuffledItems = PlayOrder ourselves) and verify
            // against MoveTo's returned item; if the platform indexed the
            // other list, correct with the Items index. Without shuffle the
            // two orders are the same list, so the first call always lands.
            int index = Queue.ShuffleEnabled
                ? IndexOfInPlayOrder(song)
                : _list.Items.IndexOf(item);
            if (index < 0)
            {
                return;
            }

            MediaPlaybackItem landed = _list.MoveTo((uint)index);
            if (landed != item)
            {
                int itemsIndex = _list.Items.IndexOf(item);
                if (itemsIndex >= 0)
                {
                    _list.MoveTo((uint)itemsIndex);
                }
            }

            Player.Play();
        }

        private int IndexOfInPlayOrder(Song song)
        {
            IReadOnlyList<Song> order = Queue.PlayOrder;
            for (int i = 0; i < order.Count; i++)
            {
                if (ReferenceEquals(order[i], song))
                {
                    return i;
                }
            }

            return -1;
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

            PersistQueueStructure(Session.Position);
        }

        /// <summary>Sign-out / hard stop: silence, empty queue, bar hides.</summary>
        public void Stop()
        {
            Player.Pause();
            DetachList();
            Player.Source = null;
            _scrobbleSong = null;
            _pendingSeek = null;
            _pendingSeekSong = null;
            Queue.Clear();

            // No queue on screen = nothing to resume (§6.1 stays honest with
            // what the user sees). Sign-out lands here too.
            _ = ResumeStore.ClearAsync();
        }

        /// <summary>
        /// Suspend hook (App.OnSuspending): flush the current position so a
        /// termination from the suspended state resumes where the listener
        /// actually was. Static so a launch that never touched playback does
        /// not create the instance.
        /// </summary>
        public static void NotifySuspending()
        {
            var instance = _instance;
            if (instance != null && !instance.Queue.IsEmpty)
            {
                ResumeStore.UpdatePosition(
                    instance.Queue.CurrentIndex, instance.Session.Position.TotalSeconds);
            }
        }

        // --- §6.1 persistence ---------------------------------------------------

        /// <summary>
        /// Fire-and-forget snapshot of the queue structure (UI thread only —
        /// reads the queue). Called on the rare structural mutations:
        /// replace, shuffle toggle, repeat change, resume.
        /// </summary>
        private void PersistQueueStructure(TimeSpan position)
        {
            string serverUrl;
            string username;
            if (Queue.IsEmpty || !CredentialStore.TryGetActiveIdentity(out serverUrl, out username))
            {
                _ = ResumeStore.ClearAsync();
                return;
            }

            IReadOnlyList<int> playOrderIndices = null;
            if (Queue.ShuffleEnabled)
            {
                // PlayOrder → original-order indices, by reference identity.
                var indexOf = new Dictionary<Song, int>(Queue.Tracks.Count);
                for (int i = 0; i < Queue.Tracks.Count; i++)
                {
                    indexOf[Queue.Tracks[i]] = i;
                }

                var indices = new int[Queue.PlayOrder.Count];
                for (int i = 0; i < Queue.PlayOrder.Count; i++)
                {
                    indices[i] = indexOf[Queue.PlayOrder[i]];
                }

                playOrderIndices = indices;
            }

            var state = new ResumeState(
                serverUrl,
                username,
                new List<Song>(Queue.Tracks),
                playOrderIndices,
                Queue.CurrentIndex,
                position.TotalSeconds,
                Queue.ShuffleEnabled,
                Queue.RepeatMode,
                Queue.Context);
            _ = ResumeStore.SaveAsync(state);
        }

        // --- Queue → MediaPlaybackList mirror ---------------------------------

        private void RebuildList()
        {
            DetachList();
            _pendingSeek = null; // only Resume() seeks, and it sets this after the rebuild
            _pendingSeekSong = null;

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

            // A new track starts at zero; re-baseline the resume scalars and
            // the throttle (both consumed on this thread).
            _lastSavedPosition = TimeSpan.Zero;

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

                _ = RunOnUiAsync(() =>
                {
                    Queue.NotifyAdvanced(song);
                    if (!Queue.IsEmpty)
                    {
                        ResumeStore.UpdatePosition(Queue.CurrentIndex, 0);
                    }
                });
            }
        }

        private void OnMediaOpened(MediaPlayer sender, object args)
        {
            // One-shot §6.1 resume seek: the position can only be set once
            // the item's media is open. A non-seekable stream leaves the
            // position at zero — the track starts over, accepted degradation.
            TimeSpan? seek = _pendingSeek;
            if (seek == null)
            {
                return;
            }

            _pendingSeek = null;
            Song intended = _pendingSeekSong;
            _pendingSeekSong = null;

            MediaPlaybackItem currentItem = _list?.CurrentItem;
            Song currentSong = null;
            if (currentItem != null)
            {
                _itemToSong.TryGetValue(currentItem, out currentSong);
            }

            if (intended != null && !ReferenceEquals(currentSong, intended))
            {
                return; // the resumed track was skipped; don't seek its successor
            }

            MediaPlaybackSession session = sender.PlaybackSession;
            TimeSpan duration = session.NaturalDuration;
            if (duration > TimeSpan.Zero && seek.Value < duration)
            {
                session.Position = seek.Value;
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
            else if (sender.PlaybackState == MediaPlaybackState.Paused && _scrobbleSong != null)
            {
                // Pausing is the classic prelude to leaving: flush the exact
                // position (Queue.CurrentIndex is an atomic int read; a
                // mid-transition stale value self-heals on the next write).
                ResumeStore.UpdatePosition(Queue.CurrentIndex, sender.Position.TotalSeconds);
            }

            _ = RunOnUiAsync(() => Changed?.Invoke());
        }

        private void OnPositionChanged(MediaPlaybackSession sender, object args)
        {
            Song song = _scrobbleSong;
            if (song == null)
            {
                return;
            }

            // §6.1: keep the resume position fresh (~10 s granularity) so a
            // crash/power loss still resumes near where the listener was.
            // Media thread; LocalSettings writes are agile and cheap.
            TimeSpan position = sender.Position;
            if ((position - _lastSavedPosition).Duration() >= PositionSaveInterval)
            {
                _lastSavedPosition = position;
                ResumeStore.UpdatePosition(Queue.CurrentIndex, position.TotalSeconds);
            }

            // Cheap threshold check on the media thread so scrobbling also
            // works while the app is backgrounded (§11); the UI's elapsed
            // display uses its own DispatcherTimer instead of this event.
            if (_scrobbleSent)
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
