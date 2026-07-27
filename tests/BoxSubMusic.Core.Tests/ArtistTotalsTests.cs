using System;
using System.Collections.Generic;
using BoxSubMusic.Core.Browsing;
using BoxSubMusic.SubsonicClient.Models;
using Xunit;

namespace BoxSubMusic.Core.Tests;

/// <summary>
/// Per-artist track counts and play time summed from album pages (§7.1). This
/// is the whole reason the Artists list can show totals at all — getArtists
/// reports none — so the summing and the "unknown vs zero" distinction are
/// what these tests pin down.
/// </summary>
public class ArtistTotalsTests
{
    private static Album Make(
        string id,
        string? artistId,
        int songCount = 1,
        int durationMinutes = 10)
    {
        return new Album(
            id: id,
            name: id,
            artist: artistId,
            artistId: artistId,
            coverArtId: null,
            songCount: songCount,
            duration: TimeSpan.FromMinutes(durationMinutes),
            created: null,
            year: null,
            genre: null,
            isStarred: false);
    }

    [Fact]
    public void SumsEveryAlbumOfAnArtist()
    {
        var totals = new ArtistTotals();
        totals.AddPage(new List<Album>
        {
            Make("a1", "miles", songCount: 5, durationMinutes: 45),
            Make("a2", "miles", songCount: 7, durationMinutes: 38),
        });

        Assert.True(totals.TryGet("miles", out ArtistTotal total));
        Assert.Equal(2, total.AlbumCount);
        Assert.Equal(12, total.SongCount);
        Assert.Equal(TimeSpan.FromMinutes(83), total.Duration);
    }

    [Fact]
    public void KeepsArtistsApart()
    {
        var totals = new ArtistTotals();
        totals.AddPage(new List<Album>
        {
            Make("a1", "miles", songCount: 5),
            Make("a2", "coltrane", songCount: 9),
        });

        Assert.True(totals.TryGet("miles", out ArtistTotal miles));
        Assert.True(totals.TryGet("coltrane", out ArtistTotal coltrane));
        Assert.Equal(5, miles.SongCount);
        Assert.Equal(9, coltrane.SongCount);
        Assert.Equal(2, totals.ArtistCount);
    }

    [Fact]
    public void AccumulatesAcrossPages()
    {
        // The pass streams the library a page at a time; the sums must survive
        // the page boundary an artist happens to straddle.
        var totals = new ArtistTotals();
        totals.AddPage(new List<Album> { Make("a1", "miles", songCount: 5, durationMinutes: 45) });
        totals.AddPage(new List<Album> { Make("a2", "miles", songCount: 7, durationMinutes: 38) });

        Assert.True(totals.TryGet("miles", out ArtistTotal total));
        Assert.Equal(2, total.AlbumCount);
        Assert.Equal(12, total.SongCount);
        Assert.Equal(TimeSpan.FromMinutes(83), total.Duration);
    }

    [Fact]
    public void SkipsAlbumsWithNoArtistId()
    {
        // Compilations and loose albums can't be attributed to a row; counting
        // them under some artist would make the totals wrong, not incomplete.
        var totals = new ArtistTotals();
        totals.AddPage(new List<Album>
        {
            Make("a1", null, songCount: 4),
            Make("a2", string.Empty, songCount: 6),
            Make("a3", "miles", songCount: 5),
        });

        Assert.Equal(1, totals.ArtistCount);
        Assert.True(totals.TryGet("miles", out ArtistTotal total));
        Assert.Equal(5, total.SongCount);
    }

    [Fact]
    public void UnseenArtistIsUnknownNotZero()
    {
        // The caller must be able to tell "no albums in this pass named them"
        // from "they have nothing", so the row can keep its album count instead
        // of claiming 0 tracks.
        var totals = new ArtistTotals();
        totals.AddPage(new List<Album> { Make("a1", "miles") });

        Assert.False(totals.TryGet("coltrane", out ArtistTotal _));
        Assert.False(totals.TryGet(null, out ArtistTotal _));
        Assert.False(totals.TryGet(string.Empty, out ArtistTotal _));
    }

    [Fact]
    public void ArtistIdsAreComparedExactly()
    {
        // Ids are opaque server strings: two ids differing only in case are two
        // different artists, not one.
        var totals = new ArtistTotals();
        totals.AddPage(new List<Album>
        {
            Make("a1", "Miles", songCount: 5),
            Make("a2", "miles", songCount: 9),
        });

        Assert.Equal(2, totals.ArtistCount);
        Assert.True(totals.TryGet("Miles", out ArtistTotal upper));
        Assert.Equal(5, upper.SongCount);
    }

    [Fact]
    public void EmptyPageChangesNothing()
    {
        var totals = new ArtistTotals();
        totals.AddPage(new List<Album>());

        Assert.Equal(0, totals.ArtistCount);
    }

    [Fact]
    public void AddPageRejectsNull()
    {
        var totals = new ArtistTotals();

        Assert.Throws<ArgumentNullException>(() => totals.AddPage(null!));
    }
}
