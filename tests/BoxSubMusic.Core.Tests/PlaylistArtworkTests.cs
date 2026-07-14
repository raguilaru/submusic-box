using BoxSubMusic.Core.Playlists;
using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.Core.Tests;

/// <summary>
/// Requirements §7.4: client-side playlist tile artwork — up to four distinct
/// album covers, a single cover when every track shares one album, no server
/// artwork endpoint.
/// </summary>
public class PlaylistArtworkTests
{
    private static Song Track(string? albumId, string? coverArtId) =>
        new(
            id: "s-" + coverArtId,
            title: "t",
            artist: null,
            artistId: null,
            album: null,
            albumId: albumId,
            coverArtId: coverArtId,
            duration: TimeSpan.Zero,
            track: null,
            discNumber: null,
            year: null,
            format: null,
            bitRate: null,
            contentType: null,
            isStarred: false);

    [Fact]
    public void SingleAlbumCollapsesToOneCover()
    {
        var songs = new[] { Track("al-1", "al-1"), Track("al-1", "al-1"), Track("al-1", "al-1") };

        var covers = PlaylistArtwork.SelectCoverArtIds(songs);

        Assert.Equal(new[] { "al-1" }, covers.ToArray());
    }

    [Fact]
    public void MultipleAlbumsYieldDistinctCoversInOrder()
    {
        var songs = new[]
        {
            Track("al-1", "al-1"),
            Track("al-2", "al-2"),
            Track("al-1", "al-1"), // repeat of al-1 -> not added again
            Track("al-3", "al-3"),
        };

        var covers = PlaylistArtwork.SelectCoverArtIds(songs);

        Assert.Equal(new[] { "al-1", "al-2", "al-3" }, covers.ToArray());
    }

    [Fact]
    public void CapsAtFourDistinctCovers()
    {
        var songs = new[]
        {
            Track("al-1", "al-1"),
            Track("al-2", "al-2"),
            Track("al-3", "al-3"),
            Track("al-4", "al-4"),
            Track("al-5", "al-5"), // beyond the cap
        };

        var covers = PlaylistArtwork.SelectCoverArtIds(songs);

        Assert.Equal(new[] { "al-1", "al-2", "al-3", "al-4" }, covers.ToArray());
    }

    [Fact]
    public void SkipsTracksWithoutCoverArt()
    {
        var songs = new[]
        {
            Track("al-1", null),
            Track("al-2", "al-2"),
        };

        var covers = PlaylistArtwork.SelectCoverArtIds(songs);

        Assert.Equal(new[] { "al-2" }, covers.ToArray());
    }

    [Fact]
    public void UnknownAlbumDedupesByCoverId()
    {
        // No albumId: identical cover ids must not repeat, distinct ones count.
        var songs = new[]
        {
            Track(null, "art-1"),
            Track(null, "art-1"),
            Track(null, "art-2"),
        };

        var covers = PlaylistArtwork.SelectCoverArtIds(songs);

        Assert.Equal(new[] { "art-1", "art-2" }, covers.ToArray());
    }

    [Fact]
    public void ArtlessPlaylistYieldsEmpty()
    {
        var songs = new[] { Track("al-1", null), Track("al-2", null) };

        var covers = PlaylistArtwork.SelectCoverArtIds(songs);

        Assert.Empty(covers);
    }
}
