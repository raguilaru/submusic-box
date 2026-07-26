using System;
using System.Collections.Generic;
using System.Linq;
using BoxSubMusic.Core.Browsing;
using BoxSubMusic.SubsonicClient.Models;
using Xunit;

namespace BoxSubMusic.Core.Tests;

/// <summary>
/// The Songs view's client-side orderings (§7.3). The Subsonic API cannot
/// sort songs, so these orderings are the whole feature.
/// </summary>
public class SongSorterTests
{
    private static Song Make(
        string title,
        string? artist = null,
        string? album = null,
        int? disc = null,
        int? track = null,
        DateTimeOffset? created = null)
    {
        return new Song(
            id: title,
            title: title,
            artist: artist,
            artistId: null,
            album: album,
            albumId: null,
            coverArtId: null,
            duration: TimeSpan.FromMinutes(3),
            track: track,
            discNumber: disc,
            year: null,
            format: "mp3",
            bitRate: 320,
            contentType: null,
            isStarred: false,
            created: created);
    }

    private static IReadOnlyList<string> Titles(IReadOnlyList<Song> songs) =>
        songs.Select(s => s.Title).ToList();

    [Fact]
    public void DefaultOrder_ReturnsTheServerOrderUntouched()
    {
        var songs = new[] { Make("Zulu"), Make("Alpha"), Make("Mike") };

        var sorted = SongSorter.Sort(songs, SongSort.DefaultOrder);

        Assert.Equal(new[] { "Zulu", "Alpha", "Mike" }, Titles(sorted));
    }

    [Fact]
    public void Title_SortsAlphabeticallyIgnoringCase()
    {
        var songs = new[] { Make("delta"), Make("Alpha"), Make("charlie"), Make("Bravo") };

        var sorted = SongSorter.Sort(songs, SongSort.Title);

        Assert.Equal(new[] { "Alpha", "Bravo", "charlie", "delta" }, Titles(sorted));
    }

    [Fact]
    public void Artist_GroupsByArtistThenOrdersEachArtistByAlbumAndTrack()
    {
        var songs = new[] {
            Make("B2", artist: "Beta", album: "Second", track: 2),
            Make("A2", artist: "Alpha", album: "Anthology", disc: 2, track: 1),
            Make("A1", artist: "Alpha", album: "Anthology", disc: 1, track: 1),
            Make("B1", artist: "Beta", album: "First", track: 1),
        };

        var sorted = SongSorter.Sort(songs, SongSort.Artist);

        Assert.Equal(new[] { "A1", "A2", "B1", "B2" }, Titles(sorted));
    }

    [Fact]
    public void Album_OrdersByAlbumThenDiscThenTrack()
    {
        var songs = new[] {
            Make("second-disc", album: "Anthology", disc: 2, track: 1),
            Make("later-album", album: "Beta"),
            Make("first-disc", album: "Anthology", disc: 1, track: 5),
        };

        var sorted = SongSorter.Sort(songs, SongSort.Album);

        Assert.Equal(new[] { "first-disc", "second-disc", "later-album" }, Titles(sorted));
    }

    [Fact]
    public void DateAdded_PutsNewestFirst()
    {
        var songs = new[] {
            Make("old", created: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Make("new", created: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Make("middle", created: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        };

        var sorted = SongSorter.Sort(songs, SongSort.DateAdded);

        Assert.Equal(new[] { "new", "middle", "old" }, Titles(sorted));
    }

    [Fact]
    public void DateAdded_SongsWithoutACreatedDateSortLast()
    {
        var songs = new[] {
            Make("undated"),
            Make("dated", created: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        };

        var sorted = SongSorter.Sort(songs, SongSort.DateAdded);

        Assert.Equal(new[] { "dated", "undated" }, Titles(sorted));
    }

    [Fact]
    public void Sort_IsStable_SoTiedSongsKeepTheServerOrder()
    {
        // Same title and artist: only the server's order can separate them.
        var first = Make("Same", artist: "One");
        var second = Make("Same", artist: "One");
        var songs = new[] { first, second };

        var sorted = SongSorter.Sort(songs, SongSort.Title);

        Assert.Same(first, sorted[0]);
        Assert.Same(second, sorted[1]);
    }

    [Fact]
    public void Sort_ProjectsThroughASelector_SoDisplayRowsCanBeSortedDirectly()
    {
        // The Songs page sorts its SongRow wrappers, not bare Songs.
        var rows = new[] {
            new { Row = "z", Song = Make("Zulu") },
            new { Row = "a", Song = Make("Alpha") },
        };

        var sorted = SongSorter.Sort(rows, r => r.Song, SongSort.Title);

        Assert.Equal(new[] { "a", "z" }, sorted.Select(r => r.Row).ToArray());
    }

    [Fact]
    public void Sort_RejectsAnUnknownSort()
    {
        var songs = new[] { Make("Alpha") };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => SongSorter.Sort(songs, (SongSort)999));
    }
}
