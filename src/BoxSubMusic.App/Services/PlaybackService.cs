using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoxSubMusic.Core.Playback;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;

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

        // A seek is clamped this far short of the end so landing on the last
        // lyric line cannot trip end-of-stream and advance the track instead.
        private static readonly TimeSpan SeekEndGuard = TimeSpan.FromMilliseconds(500);

        private const int ThumbnailArtSize = 300;

        private static PlaybackService _instance;

        public static PlaybackService Instance =>
            _instance ?? (_instance = new PlaybackService());

        /// <summary>
        /// Queue length for diagnostics, or -1 when playback was never
        /// started. Deliberately does NOT create the instance: it is read from
        /// the memory-pressure handlers on a threadpool thread, and the
        /// service must be constructed on the UI thread (it captures that
        /// dispatcher).
        ///
        /// Logged because the queue is the prime suspect for the app's native
        /// footprint: RebuildList materializes a MediaPlaybackItem and a
        /// MediaSource for EVERY track up front, so a few queued playlists
        /// mean hundreds of native objects that no view release can touch.
        /// </summary>
        public static int QueueCountForDiagnostics
        {
            get
            {
                var instance = _instance;
                return instance == null ? -1 : instance.Queue.Tracks.Count;
            }
        }

        private readonly CoreDispatcher _dispatcher;
        private readonly Dictionary<Song, MediaPlaybackItem> _songToItem =
            new Dictionary<Song, MediaPlaybackItem>();
        private readonly Dictionary<MediaPlaybackItem, Song> _itemToSong =
            new Dictionary<MediaPlaybackItem, Song>();

        /// <summary>
        /// Guards both item maps. They are written on the UI thread and read
        /// on the media thread (<see cref="OnCurrentItemChanged"/>,
        /// <see cref="OnItemFailed"/>), and a plain Dictionary is not safe for
        /// concurrent read + write. The 2026-07-23 code review flagged it as
        /// latent with a tiny window; closed 2026-08-13. Same reasoning as
        /// <see cref="_retryGate"/>.
        /// </summary>
        private readonly object _mapGate = new object();

        private MediaPlaybackList _list;

        /// <summary>
        /// Played items the list keeps open for responsive skip-back. **0 since
        /// 2026-08-13** (§11): at 2, a track change left the one you came from
        /// open on top of the new one and its prefetched successor. The console
        /// measured this returning ~10 MB per jump with no observed downside,
        /// so it stays — but it is NOT the fix for the background limit, which
        /// is still open. Keeping played items open buys a marginally faster
        /// Previous; against a 128 MB budget that is the wrong trade.
        ///
        /// The background-pressure path below still sets 0 explicitly, so
        /// raising this constant again cannot silently un-fix the background
        /// case.
        /// </summary>
        private const int DefaultPlayedItemsKept = 0;

        // Diagnostics: how long after a stream opens to take the settled
        // reading, and the one-shot timer that takes it. The 2026-08-13 console
        // log showed the cost of a jump arriving here, not at MoveTo — +41,
        // +25 and +37 MB across three jumps, none of it returned. The reading
        // now hangs off EVERY track change, not just a jump, because the
        // open question is whether an ordinary advance costs the same.
        private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(2500);
        private DispatcherTimer _settleTimer;
        private int _jumpCount;
        private int _streamOpenCount;

        // Scrobble bookkeeping, touched from the media thread (PositionChanged).
        private Song _scrobbleSong;
        private bool _scrobbleSent;

        // Consecutive open failures; when a whole queue's worth fails in a row
        // (unreachable server), stop instead of skipping forever.
        private int _consecutiveFailures;

        // Decode-failure mp3 fallback (Slice 14, §16 decision 2026-07-19):
        // a server enforcing its own bitrate restriction may transcode to a
        // format the Xbox cannot decode (Navidrome's default is Opus/Ogg).
        // Such a track is retried once with format=mp3 — the server still
        // applies its own bitrate cap to that transcode, so the restriction
        // stays respected. _retriedSongs is checked on the media thread
        // (ItemFailed) and cleared on the UI thread, hence the lock;
        // _fallbackItems and _preferMp3Fallback are UI-thread only. Once a
        // fallback item audibly plays, the session prefers mp3 for future
        // items so a fully-transcoded queue does not hiccup on every track.
        private readonly object _retryGate = new object();
        private readonly HashSet<Song> _retriedSongs = new HashSet<Song>();
        private readonly HashSet<MediaPlaybackItem> _fallbackItems =
            new HashSet<MediaPlaybackItem>();
        private bool _preferMp3Fallback;

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

        /// <summary>
        /// What the decoder is ACTUALLY playing right now — codec and, when
        /// the stream declares one, bitrate — read from the current item's
        /// audio track (§10 display fix, 2026-07-19): a server enforcing its
        /// own bitrate restriction transcodes, so the Song's tag-derived
        /// format/bitrate can describe a stream that is not the one being
        /// delivered. Platform-sourced, so it is server-agnostic and also
        /// reflects the mp3 decode-fallback. Returns false until the current
        /// item has opened (callers fall back to the tag values; the next
        /// Changed after the open refreshes them). UI thread.
        /// </summary>
        public bool TryGetDeliveredStreamInfo(out string format, out int? bitrateKbps)
        {
            format = null;
            bitrateKbps = null;

            MediaPlaybackItem item = _list?.CurrentItem;
            if (item == null || item.AudioTracks.Count == 0)
            {
                return false;
            }

            int index = item.AudioTracks.SelectedIndex;
            AudioEncodingProperties props =
                item.AudioTracks[index >= 0 ? index : 0].GetEncodingProperties();
            if (!string.IsNullOrEmpty(props.Subtype))
            {
                format = props.Subtype.ToUpperInvariant();
            }

            if (props.Bitrate > 0)
            {
                // Bits per second → kbit/s. VBR streams (e.g. Opus) may not
                // declare one; the callers then show the codec alone rather
                // than a number that would be a guess.
                bitrateKbps = (int)(props.Bitrate / 1000);
            }

            return format != null || bitrateKbps.HasValue;
        }

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
                // Plays immediately (the no-invisible-state rule) — the bar
                // revealing itself and the audio ARE the feedback; no toast.
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
                added.Add(IsMapped(song) || !seen.Add(song)
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
            // Bracketed like RebuildList (§11): a queue built by "Add to
            // queue" never goes through RebuildList, so without this an A/B
            // that grows the queue by queue edits would report no per-item
            // cost at all. NOT split into two passes the way RebuildList is —
            // this list is live and attached to the player, and mutating it is
            // precisely what destabilized the reverted item-recycling attempt.
            // One number for the pair is enough here; RebuildList supplies the
            // split.
            BackgroundDiagnostics.LogMemory(
                "QueueAddStart (adding " + added.Count + " to " + Queue.Tracks.Count + ")");

            foreach (Song song in added)
            {
                var item = CreateItem(client, song);
                MapItem(song, item);
                _list.Items.Insert(IndexOfInTracks(song), item);
            }

            BackgroundDiagnostics.LogMemory(
                "QueueAddComplete (added " + added.Count + ", queue " + Queue.Tracks.Count + ")");
            BackgroundDiagnostics.FlushSoon();

            if (Queue.ShuffleEnabled)
            {
                ApplyShuffleToList();
            }

            PersistQueueStructure(Session.Position);

            // Slice 14 (§15 spirit): a queue edit's only effect is invisible
            // until the queue is opened — confirm it happened.
            string what = added.Count == 1
                ? "“" + added[0].Title + "”"
                : (string.IsNullOrEmpty(emptyQueueContext?.DisplayName)
                    ? added.Count + " songs"
                    : emptyQueueContext.DisplayName + " (" + added.Count + " songs)");
            Toast.Show(insertNext
                ? what + " will play next"
                : what + " added to the queue");
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
            if (_list == null || song == null || !TryGetItem(song, out MediaPlaybackItem item))
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

            // Diagnostics (§11, 2026-08-13): the jump is the operation the
            // console log implicated, so it is bracketed the way the overlay
            // open already is. The three readings answer different questions —
            // "Requested" is the baseline, "MoveTo" catches an allocation the
            // platform makes synchronously when it opens the target item, and
            // "Settled" catches one that accrues while the new stream buffers.
            // The track's own format rides along because an open item's native
            // buffers are several times larger for lossless.
            _jumpCount++;
            BackgroundDiagnostics.LogMemory(
                "JumpRequested #" + _jumpCount + " (to " + DescribeStream(song) + ")");

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

            // The settled reading is taken by the track-change handler this
            // MoveTo triggers, so it covers ordinary advances too.
            BackgroundDiagnostics.LogMemory("AfterJumpMoveTo #" + _jumpCount);
        }

        /// <summary>Format/bitrate of a queued track, for the jump diagnostics.</summary>
        private static string DescribeStream(Song song)
        {
            string format = string.IsNullOrEmpty(song.Format) ? "unknown" : song.Format;
            return song.BitRate.HasValue
                ? format + " " + song.BitRate.Value + " kbps"
                : format;
        }

        /// <summary>
        /// Takes one memory reading a couple of seconds after a stream opens,
        /// once it has buffered — which is where the 2026-08-13 console log
        /// showed the cost landing. One-shot and restarted per open, so a burst
        /// of track changes logs a single settled figure for the burst rather
        /// than one line each. UI thread (see the class remarks).
        /// </summary>
        private void LogSettledSoon()
        {
            if (_settleTimer == null)
            {
                _settleTimer = new DispatcherTimer { Interval = SettleDelay };
                _settleTimer.Tick += (s, e) =>
                {
                    _settleTimer.Stop();
                    BackgroundDiagnostics.LogMemory("AfterTrackSettled #" + _streamOpenCount);
                    BackgroundDiagnostics.FlushSoon();
                };
            }

            _settleTimer.Stop();
            _settleTimer.Start();
        }

        /// <summary>
        /// UI-thread half of a track change (§11, 2026-08-13): logs the stream
        /// open. <see cref="LogSettledSoon"/> takes the reading 2.5 s later,
        /// which is where the console showed the cost landing.
        /// </summary>
        private void OnTrackChangedOnUi(Song newSong)
        {
            _streamOpenCount++;
            BackgroundDiagnostics.LogMemory(
                "TrackChanged #" + _streamOpenCount
                + (newSong == null ? string.Empty : " (to " + DescribeStream(newSong) + ")"));

            LogSettledSoon();
        }

        /// <summary>
        /// Moves the current track's playback position (§10.1, 2026-07-26:
        /// picking a synced lyric line seeks to it — the app's only in-track
        /// seek). The position is clamped into the open stream, so callers
        /// can hand over a raw lyric timestamp. Returns false when the stream
        /// will not seek (no open item, or a transcode delivered without a
        /// known duration), leaving playback untouched so the caller can fall
        /// back to a display-only action. UI thread.
        /// </summary>
        public bool SeekTo(TimeSpan position)
        {
            MediaPlaybackSession session = Session;
            if (_list?.CurrentItem == null || !session.CanSeek)
            {
                return false;
            }

            TimeSpan duration = session.NaturalDuration;
            if (duration <= TimeSpan.Zero)
            {
                return false;
            }

            TimeSpan max = duration - SeekEndGuard;
            if (max < TimeSpan.Zero)
            {
                max = TimeSpan.Zero;
            }

            if (position < TimeSpan.Zero)
            {
                position = TimeSpan.Zero;
            }
            else if (position > max)
            {
                position = max;
            }

            session.Position = position;

            // Re-baseline the §6.1 resume scalars now: OnPositionChanged's
            // ~10 s throttle would otherwise leave a stale position behind a
            // short backward seek. (_lastSavedPosition is media-thread owned
            // and deliberately not touched from here; a large seek trips its
            // own threshold and re-baselines it on the next tick.)
            if (!Queue.IsEmpty)
            {
                ResumeStore.UpdatePosition(Queue.CurrentIndex, position.TotalSeconds);
            }

            Changed?.Invoke();
            return true;
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

        /// <summary>
        /// Background memory hook (App.ReduceMemoryUsage, §11): drop the
        /// played-item buffers the list keeps for fast skip-back.
        ///
        /// Each open <see cref="MediaPlaybackItem"/> holds demuxer and decoder
        /// state plus its network buffer, and that memory is NATIVE — which is
        /// where the app's footprint actually lives (console 2026-08-12:
        /// 236 MB total against a 2 MB managed heap). The cost is scale-free
        /// for lossy files but real for high-bitrate FLAC, whose buffers are
        /// several times larger.
        ///
        /// Only the responsiveness of pressing Previous is lost, and only
        /// while backgrounded, where there is no UI to press it from. The
        /// value is restored when the app returns to the foreground.
        /// </summary>
        public static void NotifyBackgroundMemoryPressure()
        {
            SetPlayedItemsKept(0);
        }

        /// <summary>Foreground hook: restore normal skip-back buffering.</summary>
        public static void NotifyLeavingBackground()
        {
            SetPlayedItemsKept(DefaultPlayedItemsKept);
        }

        private static void SetPlayedItemsKept(int count)
        {
            var instance = _instance;
            MediaPlaybackList list = instance?._list;
            if (list == null)
            {
                return;
            }

            try
            {
                list.MaxPlayedItemsToKeepOpen = (uint)count;
            }
            catch (Exception)
            {
                // Never let a memory optimization take down playback — the
                // whole point of the exercise is that audio keeps running.
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
                MaxPlayedItemsToKeepOpen = DefaultPlayedItemsKept,
                AutoRepeatEnabled = Queue.RepeatMode == RepeatMode.All,
            };

            // Bracketed for §11: this loop is the leading candidate for the
            // app's native footprint — an item, a MediaSource and a full set
            // of display properties per track, all up front, scaling linearly
            // with queue length, which is the exact shape of the reported bug.
            // The estimate in hand (~16–26 MB for 200 tracks) is indirect;
            // these three readings measure it, and split it into the two
            // halves that have different fixes. Deliberately measurement
            // only — nothing below changes what is built, just the order the
            // two halves run in.
            int trackCount = Queue.Tracks.Count;
            string tracks = " (" + trackCount + " tracks)";
            BackgroundDiagnostics.LogMemory("RebuildListStart" + tracks);

            // Items always sit in the list in ORIGINAL order; shuffle is
            // expressed via SetShuffledItems below, so toggling shuffle later
            // never interrupts the current track.
            string format = _preferMp3Fallback ? "mp3" : null;
            var created = new List<MediaPlaybackItem>(trackCount);
            foreach (Song song in Queue.Tracks)
            {
                var item = CreateItemCore(client, song, format);
                MapItem(song, item);
                created.Add(item);
                list.Items.Add(item);
            }

            BackgroundDiagnostics.LogMemory("RebuildListItemsCreated" + tracks);

            // Second pass rather than one call per item, so the SMTC half is
            // separable. Safe here in a way it would not be on a live list:
            // this list is not yet the player's source, so nothing can read a
            // half-populated item in between.
            for (int i = 0; i < created.Count; i++)
            {
                ApplyDisplayPropertiesTo(created[i], Queue.Tracks[i]);
            }

            BackgroundDiagnostics.LogMemory("RebuildListDisplayPropsApplied" + tracks);
            BackgroundDiagnostics.FlushSoon();

            TryGetItem(Queue.CurrentTrack, out MediaPlaybackItem startingItem);
            list.StartingItem = startingItem;
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

            ClearMaps();
            _fallbackItems.Clear();
            lock (_retryGate)
            {
                _retriedSongs.Clear();
            }
            // _preferMp3Fallback deliberately survives: the Xbox's decoder
            // set does not change between queues within a session.
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
                    if (TryGetItem(song, out MediaPlaybackItem ordered))
                    {
                        order.Add(ordered);
                    }
                }

                _list.SetShuffledItems(order);
            }
            else
            {
                _list.ShuffleEnabled = false;
            }
        }

        // --- Item maps (see _mapGate) -----------------------------------------

        private void MapItem(Song song, MediaPlaybackItem item)
        {
            lock (_mapGate)
            {
                _songToItem[song] = item;
                _itemToSong[item] = song;
            }
        }

        private bool TryGetSong(MediaPlaybackItem item, out Song song)
        {
            lock (_mapGate)
            {
                return _itemToSong.TryGetValue(item, out song);
            }
        }

        private bool TryGetItem(Song song, out MediaPlaybackItem item)
        {
            lock (_mapGate)
            {
                return _songToItem.TryGetValue(song, out item);
            }
        }

        private bool IsMapped(Song song)
        {
            lock (_mapGate)
            {
                return _songToItem.ContainsKey(song);
            }
        }

        /// <summary>Repoints <paramref name="song"/> at a replacement item and
        /// forgets the old one. Used by the mp3 fallback and by item
        /// recycling.</summary>
        private void RemapItem(Song song, MediaPlaybackItem oldItem, MediaPlaybackItem newItem)
        {
            lock (_mapGate)
            {
                _itemToSong.Remove(oldItem);
                _songToItem[song] = newItem;
                _itemToSong[newItem] = song;
            }
        }

        private void ClearMaps()
        {
            lock (_mapGate)
            {
                _songToItem.Clear();
                _itemToSong.Clear();
            }
        }

        private MediaPlaybackItem CreateItem(SubsonicApiClient client, Song song)
        {
            return CreateItem(client, song, _preferMp3Fallback ? "mp3" : null);
        }

        private MediaPlaybackItem CreateItem(SubsonicApiClient client, Song song, string format)
        {
            MediaPlaybackItem item = CreateItemCore(client, song, format);
            ApplyDisplayPropertiesTo(item, song);
            return item;
        }

        /// <summary>
        /// The item and its source, without SMTC metadata. Split out from
        /// <see cref="CreateItem(SubsonicApiClient, Song, string)"/> purely so
        /// <see cref="RebuildList"/> can measure the two halves separately
        /// (§11): the per-item queue cost is the leading candidate, and which
        /// half of it is expensive decides between applying display properties
        /// lazily (cheap, does not touch <c>_list.Items</c>) and a windowed
        /// list (expensive, and mutating that collection is what sank the item
        /// recycling attempt). Every other caller still gets both halves in
        /// one call.
        /// </summary>
        private MediaPlaybackItem CreateItemCore(SubsonicApiClient client, Song song, string format)
        {
            var item = new MediaPlaybackItem(
                MediaSource.CreateFromUri(client.GetStreamUri(song.Id, format: format)));
            if (format != null)
            {
                _fallbackItems.Add(item);
            }

            return item;
        }

        /// <summary>
        /// SMTC metadata (§11): shown by the system media flyout and on
        /// whatever the console casts it to. Includes a thumbnail stream
        /// reference per item.
        /// </summary>
        private static void ApplyDisplayPropertiesTo(MediaPlaybackItem item, Song song)
        {
            MediaItemDisplayProperties props = item.GetDisplayProperties();
            props.Type = MediaPlaybackType.Music;
            props.MusicProperties.Title = song.Title;
            props.MusicProperties.Artist = song.Artist ?? string.Empty;
            props.MusicProperties.AlbumTitle = song.Album ?? string.Empty;
            Uri thumbnail = CoverArt.UriFor(song.CoverArtId, ThumbnailArtSize);
            if (thumbnail != null)
            {
                props.Thumbnail = RandomAccessStreamReference.CreateFromUri(thumbnail);
            }

            item.ApplyDisplayProperties(props);
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
                TryGetSong(newItem, out song);
            }

            // §11 diagnostics (2026-08-13): every stream open is logged, with
            // the settled reading 2.5 s later where the cost actually lands.
            // Marshalled because this handler is on the media thread.
            if (!ReferenceEquals(args.OldItem, newItem))
            {
                Song openedSong = song;
                _ = RunOnUiAsync(() => OnTrackChangedOnUi(openedSong));
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
                TryGetSong(currentItem, out currentSong);
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
            Song song = null;
            if (args.Item != null)
            {
                TryGetSong(args.Item, out song);
            }

            // Decode-failure mp3 fallback (§16 decision 2026-07-19): a
            // stream the Xbox cannot DECODE — as opposed to not reach — is
            // most likely the server's own transcode in a format this
            // hardware lacks (a server-side bitrate restriction typically
            // means Opus from Navidrome). Retry the track once with
            // format=mp3; the server still applies its bitrate cap to that
            // transcode. Not counted as a failure and not toasted — if the
            // retry works nothing was lost, and if the mp3 item fails too
            // it comes back here once more and takes the normal skip path.
            MediaPlaybackItemErrorCode error = args.Error?.ErrorCode
                ?? MediaPlaybackItemErrorCode.None;
            bool decodeClass = error == MediaPlaybackItemErrorCode.DecodeError
                || error == MediaPlaybackItemErrorCode.SourceNotSupportedError;
            if (song != null && decodeClass && MarkRetried(song))
            {
                MediaPlaybackItem failedItem = args.Item;
                _ = RunOnUiAsync(() => RetryWithMp3(song, failedItem));
                return;
            }

            // The list skips a failed item on its own; only guard against a
            // whole queue of failures (server unreachable) looping forever.
            // Slice 14 (§3/§15): neither the skip nor the stop may pass
            // silently — a track vanishing mid-album reads as a bug. During
            // a failure burst the newest toast replaces the previous one, so
            // the surface never backs up.
            int failures = ++_consecutiveFailures;
            if (failures >= sender.Items.Count)
            {
                Toast.Show("Playback stopped: no track in the queue could be played. Check the server connection.");
                _ = RunOnUiAsync(Stop);
                return;
            }

            Toast.Show(song != null
                ? "Skipped “" + song.Title + "” — the track could not be played."
                : "Skipped a track that could not be played.");
        }

        /// <summary>
        /// Records that <paramref name="song"/> got its one fallback retry.
        /// Returns false when it already had it. Media thread (ItemFailed);
        /// the lock covers the UI thread's clear in <see cref="DetachList"/>.
        /// </summary>
        private bool MarkRetried(Song song)
        {
            lock (_retryGate)
            {
                return _retriedSongs.Add(song);
            }
        }

        /// <summary>
        /// Swaps the failed item for a format=mp3 one in place and moves
        /// playback back to it (UI thread — it mutates the list and maps).
        /// The list has usually auto-advanced past the failure by now, so
        /// the successor may audibly start for an instant before the MoveTo
        /// lands; accepted (same class as the JumpTo shuffle correction).
        /// </summary>
        private void RetryWithMp3(Song song, MediaPlaybackItem failedItem)
        {
            var client = AppSession.Client;
            if (_list == null || client == null)
            {
                return;
            }

            int index = _list.Items.IndexOf(failedItem);
            if (index < 0)
            {
                return; // the queue was rebuilt or stopped in the meantime
            }

            var item = CreateItem(client, song, "mp3");
            RemapItem(song, failedItem, item);
            _list.Items[index] = item;
            if (Queue.ShuffleEnabled)
            {
                ApplyShuffleToList();
            }

            // Same undocumented-index-space dance as JumpTo: try the
            // play-order index under shuffle, verify, correct once.
            int moveIndex = Queue.ShuffleEnabled ? IndexOfInPlayOrder(song) : index;
            if (moveIndex < 0)
            {
                return;
            }

            MediaPlaybackItem landed = _list.MoveTo((uint)moveIndex);
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

        private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            if (sender.PlaybackState == MediaPlaybackState.Playing)
            {
                // A track opened and is audibly playing: the failure streak,
                // if any, is over.
                _consecutiveFailures = 0;

                // If the playing item is an mp3-fallback one, the server's
                // preferred transcode format is confirmed undecodable here:
                // build future items as mp3 directly, so a fully-transcoded
                // queue does not fail-and-retry on every track. UI thread
                // (the flag and set are UI-owned).
                _ = RunOnUiAsync(() =>
                {
                    if (!_preferMp3Fallback && _list?.CurrentItem != null
                        && _fallbackItems.Contains(_list.CurrentItem))
                    {
                        _preferMp3Fallback = true;
                    }
                });
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
