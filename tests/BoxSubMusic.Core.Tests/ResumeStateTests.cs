using BoxSubMusic.Core.Playback;
using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.Core.Tests;

public class ResumeStateTests
{
    private static Song MakeSong(string id, bool starred = false) => new(
        id: id,
        title: "Track " + id,
        artist: "Artist",
        artistId: "ar-1",
        album: "Album",
        albumId: "al-1",
        coverArtId: "cov-" + id,
        duration: TimeSpan.FromSeconds(245.5),
        track: 3,
        discNumber: 1,
        year: 1969,
        format: "flac",
        bitRate: 1024,
        contentType: "audio/flac",
        isStarred: starred);

    private static List<Song> MakeSongs(int count) =>
        Enumerable.Range(1, count).Select(i => MakeSong("tr-" + i)).ToList();

    private static ResumeState MakeState(
        IReadOnlyList<Song>? songs = null,
        IReadOnlyList<int>? playOrder = null,
        int currentIndex = 1,
        double positionSeconds = 42.5,
        bool shuffle = false,
        RepeatMode repeat = RepeatMode.All,
        QueueContext? context = null) => new(
            serverUrl: "https://music.example.com",
            username: "rafa",
            tracks: songs ?? MakeSongs(3),
            playOrderIndices: playOrder,
            currentIndex: currentIndex,
            positionSeconds: positionSeconds,
            shuffleEnabled: shuffle,
            repeatMode: repeat,
            context: context ?? new QueueContext("Abbey Road", "al-cov"));

    [Fact]
    public void RoundTrip_Unshuffled_PreservesEverything()
    {
        var songs = new List<Song> { MakeSong("a", starred: true), MakeSong("b") };
        var state = MakeState(songs, currentIndex: 1, positionSeconds: 12.25);

        var parsed = ResumeState.Parse(ResumeState.Serialize(state));

        Assert.NotNull(parsed);
        Assert.Equal("https://music.example.com", parsed!.ServerUrl);
        Assert.Equal("rafa", parsed.Username);
        Assert.Equal(2, parsed.Tracks.Count);
        Assert.Null(parsed.PlayOrderIndices);
        Assert.Equal(1, parsed.CurrentIndex);
        Assert.Equal(12.25, parsed.PositionSeconds);
        Assert.False(parsed.ShuffleEnabled);
        Assert.Equal(RepeatMode.All, parsed.RepeatMode);
        Assert.Equal("Abbey Road", parsed.Context?.DisplayName);
        Assert.Equal("al-cov", parsed.Context?.CoverArtId);

        Song song = parsed.Tracks[0];
        Assert.Equal("a", song.Id);
        Assert.Equal("Track a", song.Title);
        Assert.Equal("Artist", song.Artist);
        Assert.Equal("ar-1", song.ArtistId);
        Assert.Equal("Album", song.Album);
        Assert.Equal("al-1", song.AlbumId);
        Assert.Equal("cov-a", song.CoverArtId);
        Assert.Equal(TimeSpan.FromSeconds(245.5), song.Duration);
        Assert.Equal(3, song.Track);
        Assert.Equal(1, song.DiscNumber);
        Assert.Equal(1969, song.Year);
        Assert.Equal("flac", song.Format);
        Assert.Equal(1024, song.BitRate);
        Assert.Equal("audio/flac", song.ContentType);
        Assert.True(song.IsStarred);
        Assert.False(parsed.Tracks[1].IsStarred);
    }

    [Fact]
    public void RoundTrip_Shuffled_PreservesPlayOrder()
    {
        var state = MakeState(
            MakeSongs(4), playOrder: new[] { 2, 0, 3, 1 }, currentIndex: 2, shuffle: true);

        var parsed = ResumeState.Parse(ResumeState.Serialize(state));

        Assert.NotNull(parsed);
        Assert.True(parsed!.ShuffleEnabled);
        Assert.Equal(new[] { 2, 0, 3, 1 }, parsed.PlayOrderIndices);
        Assert.Equal(2, parsed.CurrentIndex);
    }

    [Fact]
    public void RoundTrip_MinimalSong_NullsSurvive()
    {
        var bare = new Song(
            id: "x", title: "T", artist: null, artistId: null, album: null,
            albumId: null, coverArtId: null, duration: TimeSpan.Zero,
            track: null, discNumber: null, year: null, format: null,
            bitRate: null, contentType: null, isStarred: false);
        // Not MakeState: it supplies a default context, and this test needs none.
        var state = new ResumeState(
            "https://s", "u", new List<Song> { bare }, null, 0, 0, false, RepeatMode.Off, null);
        var parsed = ResumeState.Parse(ResumeState.Serialize(state));

        Assert.NotNull(parsed);
        Assert.Null(parsed!.Context);
        Song song = parsed.Tracks[0];
        Assert.Null(song.Artist);
        Assert.Null(song.CoverArtId);
        Assert.Null(song.Track);
        Assert.Null(song.BitRate);
        Assert.Equal(TimeSpan.Zero, song.Duration);
    }

    [Fact]
    public void Constructor_ShuffleWithoutPermutation_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            MakeState(MakeSongs(3), playOrder: new[] { 0, 1 }, shuffle: true));
        Assert.Throws<ArgumentException>(() =>
            MakeState(MakeSongs(3), playOrder: new[] { 0, 1, 1 }, shuffle: true));
        Assert.Throws<ArgumentException>(() =>
            MakeState(MakeSongs(3), playOrder: null, shuffle: true));
    }

    [Fact]
    public void Constructor_IndexOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MakeState(MakeSongs(3), currentIndex: 3));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MakeState(MakeSongs(3), currentIndex: -1));
    }

    [Fact]
    public void Parse_WrongRoot_ReturnsNull()
    {
        Assert.Null(ResumeState.Parse("<somethingElse />"));
    }

    [Fact]
    public void Parse_TamperedIndex_ReturnsNull()
    {
        string xml = ResumeState.Serialize(MakeState(MakeSongs(3), currentIndex: 2));
        Assert.Null(ResumeState.Parse(xml.Replace("currentIndex=\"2\"", "currentIndex=\"7\"")));
    }

    [Fact]
    public void Parse_MalformedSong_RejectsWholeState()
    {
        // A song without an id must not silently drop (indices would shift).
        string xml = ResumeState.Serialize(MakeState(MakeSongs(3)))
            .Replace("id=\"tr-2\"", "id=\"\"");
        Assert.Null(ResumeState.Parse(xml));
    }

    [Fact]
    public void Parse_TamperedPlayOrder_ReturnsNull()
    {
        string xml = ResumeState.Serialize(MakeState(
            MakeSongs(3), playOrder: new[] { 2, 1, 0 }, currentIndex: 0, shuffle: true));
        Assert.Null(ResumeState.Parse(xml.Replace(">2 1 0<", ">2 2 0<")));
    }

    [Fact]
    public void Parse_UnknownRepeat_DefaultsToOff()
    {
        string xml = ResumeState.Serialize(MakeState(repeat: RepeatMode.All))
            .Replace("repeat=\"All\"", "repeat=\"Banana\"");
        Assert.Equal(RepeatMode.Off, ResumeState.Parse(xml)!.RepeatMode);
    }
}
