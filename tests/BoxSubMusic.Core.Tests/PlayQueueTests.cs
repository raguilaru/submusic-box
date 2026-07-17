using BoxSubMusic.Core.Playback;
using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.Core.Tests;

public class PlayQueueTests
{
    private static Song MakeSong(string id) => new(
        id: id,
        title: "Track " + id,
        artist: "Artist",
        artistId: "ar-1",
        album: "Album",
        albumId: "al-1",
        coverArtId: null,
        duration: TimeSpan.FromSeconds(180),
        track: null,
        discNumber: null,
        year: null,
        format: "flac",
        bitRate: 1000,
        contentType: "audio/flac",
        isStarred: false);

    private static List<Song> MakeSongs(int count) =>
        Enumerable.Range(1, count).Select(i => MakeSong("tr-" + i)).ToList();

    // Seeded: shuffle results are deterministic per seed, but tests only
    // assert properties (pool, pinning), never a specific order.
    private static PlayQueue MakeQueue(int seed = 1) => new(new Random(seed));

    [Fact]
    public void NewQueue_IsEmpty()
    {
        var queue = MakeQueue();

        Assert.True(queue.IsEmpty);
        Assert.Equal(-1, queue.CurrentIndex);
        Assert.Null(queue.CurrentTrack);
        Assert.Equal(RepeatMode.Off, queue.RepeatMode);
    }

    [Fact]
    public void Replace_Unshuffled_KeepsOrderAndStartsAtIndex()
    {
        var queue = MakeQueue();
        var songs = MakeSongs(5);

        queue.Replace(songs, startIndex: 2, shuffle: false, context: null);

        Assert.Equal(songs, queue.PlayOrder);
        Assert.Equal(songs, queue.Tracks);
        Assert.Equal(2, queue.CurrentIndex);
        Assert.Same(songs[2], queue.CurrentTrack);
        Assert.False(queue.ShuffleEnabled);
    }

    [Fact]
    public void Replace_ReplacesNotAppends()
    {
        var queue = MakeQueue();
        queue.Replace(MakeSongs(5), 0, false, null);
        var second = MakeSongs(3);

        queue.Replace(second, 0, false, null);

        Assert.Equal(3, queue.PlayOrder.Count);
        Assert.Equal(second, queue.PlayOrder);
    }

    [Fact]
    public void Replace_Shuffled_RandomizesPoolAndStartsAtZero()
    {
        var queue = MakeQueue();
        var songs = MakeSongs(50);

        queue.Replace(songs, startIndex: 30, shuffle: true, context: null);

        Assert.Equal(0, queue.CurrentIndex);
        Assert.True(queue.ShuffleEnabled);
        // Same pool, different container order; original order preserved in Tracks.
        Assert.Equal(songs.OrderBy(s => s.Id), queue.PlayOrder.OrderBy(s => s.Id));
        Assert.Equal(songs, queue.Tracks);
        Assert.NotEqual(songs, queue.PlayOrder);
    }

    [Fact]
    public void Replace_EmptyList_ClearsTheQueue()
    {
        var queue = MakeQueue();
        queue.Replace(MakeSongs(3), 0, false, new QueueContext("Album", "al-1"));

        queue.Replace(new List<Song>(), 0, false, null);

        Assert.True(queue.IsEmpty);
        Assert.Null(queue.CurrentTrack);
        Assert.Null(queue.Context);
    }

    [Fact]
    public void Replace_StartIndexOutOfRange_Throws()
    {
        var queue = MakeQueue();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => queue.Replace(MakeSongs(3), startIndex: 3, shuffle: false, context: null));
    }

    [Fact]
    public void Replace_CarriesContext()
    {
        var queue = MakeQueue();
        var context = new QueueContext("Abbey Road", "al-1");

        queue.Replace(MakeSongs(3), 0, false, context);

        Assert.Same(context, queue.Context);
    }

    [Fact]
    public void SetShuffle_On_PinsCurrentTrackFirst()
    {
        var queue = MakeQueue();
        var songs = MakeSongs(20);
        queue.Replace(songs, startIndex: 7, shuffle: false, context: null);
        var current = queue.CurrentTrack;

        queue.SetShuffle(true);

        Assert.Same(current, queue.CurrentTrack);
        Assert.Equal(0, queue.CurrentIndex);
        Assert.Equal(songs.OrderBy(s => s.Id), queue.PlayOrder.OrderBy(s => s.Id));
    }

    [Fact]
    public void SetShuffle_Off_RestoresOriginalOrderAtCurrentTrack()
    {
        var queue = MakeQueue();
        var songs = MakeSongs(20);
        queue.Replace(songs, startIndex: 0, shuffle: true, context: null);
        // Simulate having advanced somewhere into the shuffled order.
        queue.NotifyAdvanced(queue.PlayOrder[5]);
        var current = queue.CurrentTrack!;

        queue.SetShuffle(false);

        Assert.Equal(songs, queue.PlayOrder);
        Assert.Same(current, queue.CurrentTrack);
        Assert.Equal(songs.IndexOf(current), queue.CurrentIndex);
    }

    [Fact]
    public void SetShuffle_SameValue_IsANoOp()
    {
        var queue = MakeQueue();
        queue.Replace(MakeSongs(5), 1, false, null);
        int changes = 0;
        queue.Changed += () => changes++;

        queue.SetShuffle(false);

        Assert.Equal(0, changes);
        Assert.Equal(1, queue.CurrentIndex);
    }

    [Fact]
    public void CycleRepeat_WalksOffAllOneOff()
    {
        var queue = MakeQueue();

        Assert.Equal(RepeatMode.All, queue.CycleRepeat());
        Assert.Equal(RepeatMode.One, queue.CycleRepeat());
        Assert.Equal(RepeatMode.Off, queue.CycleRepeat());
    }

    [Fact]
    public void NotifyAdvanced_MovesTheIndex()
    {
        var queue = MakeQueue();
        var songs = MakeSongs(5);
        queue.Replace(songs, 0, false, null);

        queue.NotifyAdvanced(songs[3]);

        Assert.Equal(3, queue.CurrentIndex);
        Assert.Same(songs[3], queue.CurrentTrack);
    }

    [Fact]
    public void NotifyAdvanced_UnknownTrack_IsIgnored()
    {
        var queue = MakeQueue();
        var songs = MakeSongs(5);
        queue.Replace(songs, 2, false, null);

        queue.NotifyAdvanced(MakeSong("stale"));

        Assert.Equal(2, queue.CurrentIndex);
    }

    [Fact]
    public void Clear_EmptiesEverything()
    {
        var queue = MakeQueue();
        queue.Replace(MakeSongs(5), 2, true, new QueueContext("Mix", null));

        queue.Clear();

        Assert.True(queue.IsEmpty);
        Assert.Equal(-1, queue.CurrentIndex);
        Assert.False(queue.ShuffleEnabled);
        Assert.Null(queue.Context);
        Assert.Empty(queue.Tracks);
    }

    [Fact]
    public void Changed_FiresOnMutations()
    {
        var queue = MakeQueue();
        int changes = 0;
        queue.Changed += () => changes++;
        var songs = MakeSongs(5);

        queue.Replace(songs, 0, false, null);   // 1
        queue.SetShuffle(true);                 // 2
        queue.CycleRepeat();                    // 3
        queue.NotifyAdvanced(queue.PlayOrder[2]); // 4
        queue.Clear();                          // 5

        Assert.Equal(5, changes);
    }

    [Fact]
    public void Restore_Shuffled_RebuildsExactPlayOrderAndState()
    {
        var songs = MakeSongs(4);
        var state = new ResumeState(
            "https://s", "u", songs, new[] { 2, 0, 3, 1 },
            currentIndex: 1, positionSeconds: 30, shuffleEnabled: true,
            repeatMode: RepeatMode.One, context: new QueueContext("Mix", "cov"));
        var queue = MakeQueue();
        int changes = 0;
        queue.Changed += () => changes++;

        queue.Restore(state);

        Assert.Equal(songs, queue.Tracks);
        Assert.Equal(new[] { songs[2], songs[0], songs[3], songs[1] }, queue.PlayOrder);
        Assert.Equal(1, queue.CurrentIndex);
        Assert.Same(songs[0], queue.CurrentTrack);
        Assert.True(queue.ShuffleEnabled);
        Assert.Equal(RepeatMode.One, queue.RepeatMode);
        Assert.Equal("Mix", queue.Context?.DisplayName);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Restore_Unshuffled_PlayOrderIsOriginal()
    {
        var songs = MakeSongs(3);
        var state = new ResumeState(
            "https://s", "u", songs, null,
            currentIndex: 2, positionSeconds: 0, shuffleEnabled: false,
            repeatMode: RepeatMode.Off, context: null);
        var queue = MakeQueue();

        queue.Restore(state);

        Assert.Equal(songs, queue.PlayOrder);
        Assert.Equal(2, queue.CurrentIndex);
        Assert.False(queue.ShuffleEnabled);
        Assert.Null(queue.Context);
    }

    [Fact]
    public void Restore_ThenShuffleOff_ReturnsToOriginalOrderAtCurrentTrack()
    {
        // The restored shuffled queue must behave like a live one afterwards.
        var songs = MakeSongs(4);
        var state = new ResumeState(
            "https://s", "u", songs, new[] { 3, 1, 0, 2 },
            currentIndex: 0, positionSeconds: 0, shuffleEnabled: true,
            repeatMode: RepeatMode.Off, context: null);
        var queue = MakeQueue();
        queue.Restore(state);

        queue.SetShuffle(false);

        Assert.Equal(songs, queue.PlayOrder);
        Assert.Same(songs[3], queue.CurrentTrack);
        Assert.Equal(3, queue.CurrentIndex);
    }

    // --- InsertNext / Append (queue edits, 2026-07-17) ---------------------

    [Fact]
    public void InsertNext_Unshuffled_LandsAfterCurrentInBothOrders()
    {
        var queue = MakeQueue();
        var songs = MakeSongs(4);
        queue.Replace(songs, startIndex: 1, shuffle: false, context: null);
        var added = MakeSong("added");

        queue.InsertNext(added);

        Assert.Same(added, queue.PlayOrder[2]);
        Assert.Same(added, queue.Tracks[2]);
        Assert.Equal(5, queue.PlayOrder.Count);
        Assert.Equal(1, queue.CurrentIndex);
        Assert.Same(songs[1], queue.CurrentTrack);
    }

    [Fact]
    public void InsertNext_Twice_MostRecentPlaysFirst()
    {
        var queue = MakeQueue();
        queue.Replace(MakeSongs(3), startIndex: 0, shuffle: false, context: null);
        var first = MakeSong("first");
        var second = MakeSong("second");

        queue.InsertNext(first);
        queue.InsertNext(second);

        Assert.Same(second, queue.PlayOrder[1]);
        Assert.Same(first, queue.PlayOrder[2]);
    }

    [Fact]
    public void InsertNext_AtLastPosition_Appends()
    {
        var queue = MakeQueue();
        var songs = MakeSongs(3);
        queue.Replace(songs, startIndex: 2, shuffle: false, context: null);
        var added = MakeSong("added");

        queue.InsertNext(added);

        Assert.Same(added, queue.PlayOrder[3]);
        Assert.Same(added, queue.Tracks[3]);
    }

    [Fact]
    public void InsertNext_Shuffled_PlayOrderAfterCurrent_OriginalOrderAtEnd()
    {
        var queue = MakeQueue();
        var songs = MakeSongs(4);
        queue.Replace(songs, startIndex: 0, shuffle: true, context: null);
        Song current = queue.CurrentTrack!;
        var added = MakeSong("added");

        queue.InsertNext(added);

        Assert.Same(added, queue.PlayOrder[queue.CurrentIndex + 1]);
        Assert.Same(added, queue.Tracks[4]);
        Assert.Same(current, queue.CurrentTrack);

        // Un-shuffling restores the original order — the insert sits last.
        queue.SetShuffle(false);
        Assert.Equal(songs.Append(added), queue.PlayOrder);
        Assert.Same(current, queue.CurrentTrack);
    }

    [Fact]
    public void Append_AddsToEndOfBothOrders_CurrentUnchanged()
    {
        var queue = MakeQueue();
        var songs = MakeSongs(3);
        queue.Replace(songs, startIndex: 1, shuffle: false, context: null);
        var added = MakeSong("added");

        queue.Append(added);

        Assert.Same(added, queue.PlayOrder[3]);
        Assert.Same(added, queue.Tracks[3]);
        Assert.Equal(1, queue.CurrentIndex);
    }

    [Fact]
    public void Append_Shuffled_AddsToEndOfBothOrders()
    {
        var queue = MakeQueue();
        queue.Replace(MakeSongs(4), startIndex: 0, shuffle: true, context: null);
        var added = MakeSong("added");

        queue.Append(added);

        Assert.Same(added, queue.PlayOrder[4]);
        Assert.Same(added, queue.Tracks[4]);
    }

    [Fact]
    public void InsertNext_And_Append_OnEmptyQueue_BecomeSingleTrackQueue()
    {
        var insertQueue = MakeQueue();
        var insertSong = MakeSong("a");
        insertQueue.InsertNext(insertSong);
        Assert.Same(insertSong, insertQueue.CurrentTrack);
        Assert.Equal(0, insertQueue.CurrentIndex);

        var appendQueue = MakeQueue();
        var appendSong = MakeSong("b");
        appendQueue.Append(appendSong);
        Assert.Same(appendSong, appendQueue.CurrentTrack);
        Assert.Equal(0, appendQueue.CurrentIndex);
    }

    [Fact]
    public void InsertNext_Block_KeepsInternalOrderAfterCurrent()
    {
        var queue = MakeQueue();
        var songs = MakeSongs(3);
        queue.Replace(songs, startIndex: 1, shuffle: false, context: null);
        var block = new[] { MakeSong("b1"), MakeSong("b2"), MakeSong("b3") };

        queue.InsertNext(block);

        Assert.Same(block[0], queue.PlayOrder[2]);
        Assert.Same(block[1], queue.PlayOrder[3]);
        Assert.Same(block[2], queue.PlayOrder[4]);
        Assert.Same(songs[2], queue.PlayOrder[5]);
        Assert.Equal(queue.PlayOrder, queue.Tracks);
        Assert.Equal(1, queue.CurrentIndex);
    }

    [Fact]
    public void InsertNext_Block_Shuffled_PlayOrderAfterCurrent_OriginalOrderAtEnd()
    {
        var queue = MakeQueue();
        var songs = MakeSongs(4);
        queue.Replace(songs, startIndex: 0, shuffle: true, context: null);
        Song current = queue.CurrentTrack!;
        var block = new[] { MakeSong("b1"), MakeSong("b2") };

        queue.InsertNext(block);

        Assert.Same(block[0], queue.PlayOrder[queue.CurrentIndex + 1]);
        Assert.Same(block[1], queue.PlayOrder[queue.CurrentIndex + 2]);
        Assert.Same(block[0], queue.Tracks[4]);
        Assert.Same(block[1], queue.Tracks[5]);

        queue.SetShuffle(false);
        Assert.Equal(songs.Concat(block), queue.PlayOrder);
        Assert.Same(current, queue.CurrentTrack);
    }

    [Fact]
    public void Append_Block_AddsToEndOfBothOrders()
    {
        var queue = MakeQueue();
        var songs = MakeSongs(2);
        queue.Replace(songs, startIndex: 0, shuffle: false, context: null);
        var block = new[] { MakeSong("b1"), MakeSong("b2") };

        queue.Append(block);

        Assert.Equal(songs.Concat(block), queue.PlayOrder);
        Assert.Equal(songs.Concat(block), queue.Tracks);
        Assert.Equal(0, queue.CurrentIndex);
    }

    [Fact]
    public void Block_EmptyList_IsANoOp()
    {
        var queue = MakeQueue();
        queue.Replace(MakeSongs(2), startIndex: 0, shuffle: false, context: null);
        int fired = 0;
        queue.Changed += () => fired++;

        queue.InsertNext(Array.Empty<Song>());
        queue.Append(Array.Empty<Song>());

        Assert.Equal(0, fired);
        Assert.Equal(2, queue.PlayOrder.Count);
    }

    [Fact]
    public void Block_OnEmptyQueue_BecomesQueueAtIndexZero()
    {
        var queue = MakeQueue();
        var block = new[] { MakeSong("b1"), MakeSong("b2") };

        queue.InsertNext(block);

        Assert.Equal(block, queue.PlayOrder);
        Assert.Equal(block, queue.Tracks);
        Assert.Equal(0, queue.CurrentIndex);
        Assert.Same(block[0], queue.CurrentTrack);
    }

    [Fact]
    public void InsertNext_And_Append_RaiseChanged()
    {
        var queue = MakeQueue();
        queue.Replace(MakeSongs(2), startIndex: 0, shuffle: false, context: null);
        int fired = 0;
        queue.Changed += () => fired++;

        queue.InsertNext(MakeSong("a"));
        queue.Append(MakeSong("b"));

        Assert.Equal(2, fired);
    }
}
