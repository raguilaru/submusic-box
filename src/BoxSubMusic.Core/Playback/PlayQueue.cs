using System;
using System.Collections.Generic;
using System.Linq;
using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.Core.Playback
{
    /// <summary>
    /// The play queue's pure logic (requirements §10): replace-not-append
    /// semantics, shuffle order computation, repeat state, and the current
    /// index. Owns no playback — the app's PlaybackService mirrors this into
    /// a MediaPlaybackList and reports advancement back via
    /// <see cref="NotifyAdvanced"/> (see
    /// docs/architecture/30-playback-architecture.md). UI-thread-only, like
    /// the rest of the app's session state.
    /// </summary>
    public sealed class PlayQueue
    {
        private readonly Random _random;
        private readonly List<Song> _tracks = new List<Song>();
        private readonly List<Song> _playOrder = new List<Song>();

        /// <param name="random">Shuffle source; injectable so tests are deterministic.</param>
        public PlayQueue(Random? random = null)
        {
            _random = random ?? new Random();
        }

        /// <summary>The queued tracks in their original (unshuffled) order.</summary>
        public IReadOnlyList<Song> Tracks => _tracks;

        /// <summary>
        /// The order tracks actually play in. Identical to <see cref="Tracks"/>
        /// unless shuffle is on.
        /// </summary>
        public IReadOnlyList<Song> PlayOrder => _playOrder;

        /// <summary>Index of the current track within <see cref="PlayOrder"/>; -1 when empty.</summary>
        public int CurrentIndex { get; private set; } = -1;

        public Song? CurrentTrack =>
            CurrentIndex >= 0 && CurrentIndex < _playOrder.Count ? _playOrder[CurrentIndex] : null;

        public bool IsEmpty => _playOrder.Count == 0;

        public bool ShuffleEnabled { get; private set; }

        public RepeatMode RepeatMode { get; private set; }

        /// <summary>What the user pressed Play on; null until the first Replace.</summary>
        public QueueContext? Context { get; private set; }

        /// <summary>
        /// Raised after any mutation (replace, shuffle toggle, repeat cycle,
        /// advancement, clear). Coarse by design: consumers re-read the
        /// properties they care about.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// Replaces the whole queue (§10: a new Play action never appends).
        /// With <paramref name="shuffle"/> the pool is fully randomized and
        /// <paramref name="startIndex"/> is ignored (§8: Shuffle queues the
        /// pool in randomized order — the first track is random too);
        /// otherwise playback starts at <paramref name="startIndex"/> in the
        /// given order.
        /// </summary>
        public void Replace(IReadOnlyList<Song> tracks, int startIndex, bool shuffle, QueueContext? context)
        {
            if (tracks == null)
            {
                throw new ArgumentNullException(nameof(tracks));
            }

            if (tracks.Count == 0)
            {
                Clear();
                return;
            }

            if (!shuffle && (startIndex < 0 || startIndex >= tracks.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex,
                    "The start index must point into the track list.");
            }

            _tracks.Clear();
            _tracks.AddRange(tracks);
            ShuffleEnabled = shuffle;
            Context = context;

            _playOrder.Clear();
            if (shuffle)
            {
                _playOrder.AddRange(Shuffled(_tracks));
                CurrentIndex = 0;
            }
            else
            {
                _playOrder.AddRange(_tracks);
                CurrentIndex = startIndex;
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Rehydrates the queue from a persisted §6.1 resume state in one
        /// atomic mutation: tracks, exact play order, current index, shuffle
        /// and repeat flags, and context. The state's own invariants
        /// (permutation, index range) were enforced at parse/construction;
        /// this trusts them.
        /// </summary>
        public void Restore(ResumeState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            _tracks.Clear();
            _tracks.AddRange(state.Tracks);
            ShuffleEnabled = state.ShuffleEnabled;
            RepeatMode = state.RepeatMode;
            Context = state.Context;

            _playOrder.Clear();
            if (state.PlayOrderIndices != null)
            {
                foreach (int index in state.PlayOrderIndices)
                {
                    _playOrder.Add(_tracks[index]);
                }
            }
            else
            {
                _playOrder.AddRange(_tracks);
            }

            CurrentIndex = state.CurrentIndex;
            Changed?.Invoke();
        }

        /// <summary>
        /// Inserts a track directly after the current one in the play order
        /// (the context menu's "Play next" — an explicit queue edit, distinct
        /// from the §10 replace-only Play actions). Repeated inserts each
        /// land at current+1, so the most recent insert plays first. With
        /// shuffle off the original order gets the track at the same spot;
        /// with shuffle on the original order gets it at the end, so
        /// un-shuffling places it last (the original order never contained
        /// it anywhere else). On an empty queue the track becomes a
        /// single-track queue at index 0.
        /// </summary>
        public void InsertNext(Song track)
        {
            if (track == null)
            {
                throw new ArgumentNullException(nameof(track));
            }

            if (IsEmpty)
            {
                AddFirst(track);
                return;
            }

            _playOrder.Insert(CurrentIndex + 1, track);
            if (ShuffleEnabled)
            {
                _tracks.Add(track);
            }
            else
            {
                _tracks.Insert(CurrentIndex + 1, track);
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Appends a track to the end of the play order and the original
        /// order (the context menu's "Add to queue"). On an empty queue the
        /// track becomes a single-track queue at index 0.
        /// </summary>
        public void Append(Song track)
        {
            if (track == null)
            {
                throw new ArgumentNullException(nameof(track));
            }

            if (IsEmpty)
            {
                AddFirst(track);
                return;
            }

            _tracks.Add(track);
            _playOrder.Add(track);
            Changed?.Invoke();
        }

        private void AddFirst(Song track)
        {
            _tracks.Add(track);
            _playOrder.Add(track);
            CurrentIndex = 0;
            Changed?.Invoke();
        }

        /// <summary>Empties the queue (sign-out / stop).</summary>
        public void Clear()
        {
            _tracks.Clear();
            _playOrder.Clear();
            CurrentIndex = -1;
            ShuffleEnabled = false;
            Context = null;
            Changed?.Invoke();
        }

        /// <summary>
        /// Toggles shuffle mid-play. On: the current track keeps playing and
        /// is pinned first in the new randomized order. Off: the original
        /// order returns, with the index re-pointed at the current track.
        /// </summary>
        public void SetShuffle(bool enabled)
        {
            if (enabled == ShuffleEnabled)
            {
                return;
            }

            ShuffleEnabled = enabled;
            if (IsEmpty)
            {
                Changed?.Invoke();
                return;
            }

            Song current = _playOrder[CurrentIndex];
            _playOrder.Clear();
            if (enabled)
            {
                _playOrder.Add(current);
                _playOrder.AddRange(Shuffled(_tracks.Where(t => !ReferenceEquals(t, current))));
                CurrentIndex = 0;
            }
            else
            {
                _playOrder.AddRange(_tracks);
                CurrentIndex = _playOrder.IndexOf(current);
            }

            Changed?.Invoke();
        }

        /// <summary>Off → All → One → Off (§10 repeat control).</summary>
        public RepeatMode CycleRepeat()
        {
            RepeatMode = RepeatMode == RepeatMode.Off
                ? RepeatMode.All
                : RepeatMode == RepeatMode.All ? RepeatMode.One : RepeatMode.Off;
            Changed?.Invoke();
            return RepeatMode;
        }

        /// <summary>
        /// Index sync from the player mirror: the MediaPlaybackList advances
        /// itself (track end, SMTC next/previous) and reports the track that
        /// is now current. Unknown tracks (stale event after a Replace) are
        /// ignored.
        /// </summary>
        public void NotifyAdvanced(Song track)
        {
            if (track == null)
            {
                throw new ArgumentNullException(nameof(track));
            }

            int index = _playOrder.IndexOf(track);
            if (index < 0 || index == CurrentIndex)
            {
                return;
            }

            CurrentIndex = index;
            Changed?.Invoke();
        }

        private IEnumerable<Song> Shuffled(IEnumerable<Song> source)
        {
            // Fisher–Yates over a copy.
            var items = source.ToList();
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                Song swap = items[i];
                items[i] = items[j];
                items[j] = swap;
            }

            return items;
        }
    }
}
