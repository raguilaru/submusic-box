using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.SubsonicClient.Tests;

public class AlbumDetailTests
{
    private const string AlbumAttrs =
        "id=\"al-1\" name=\"Physical Graffiti\" artist=\"Led Zeppelin\" artistId=\"ar-1\" coverArt=\"al-1\" " +
        "songCount=\"3\" duration=\"5400\" year=\"1975\" genre=\"Rock\" starred=\"2024-05-06T07:08:09Z\"";

    [Fact]
    public async Task GetAlbum_MapsAlbumAndSongs()
    {
        var songs =
            "<song id=\"s-1\" title=\"Custard Pie\" artist=\"Led Zeppelin\" artistId=\"ar-1\" " +
            "album=\"Physical Graffiti\" albumId=\"al-1\" coverArt=\"al-1\" duration=\"253\" track=\"1\" " +
            "discNumber=\"1\" year=\"1975\" suffix=\"flac\" bitRate=\"1044\" contentType=\"audio/flac\" " +
            "starred=\"2024-05-06T07:08:09Z\"/>" +
            "<song id=\"s-2\" title=\"Sparse Song\" duration=\"0\"/>";
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Album(AlbumAttrs, songs)));
        var client = TestHelpers.CreateClient(handler);

        var detail = await client.GetAlbumAsync("al-1");

        Assert.Equal("/rest/getAlbum", TestHelpers.EndpointOf(handler.Requests[0]));
        Assert.Equal("al-1", TestHelpers.QueryOf(handler.Requests[0])["id"]);

        Assert.Equal("Physical Graffiti", detail.Album.Name);
        Assert.Equal("Led Zeppelin", detail.Album.Artist);
        Assert.Equal(1975, detail.Album.Year);
        Assert.Equal(3, detail.Album.SongCount);
        Assert.Equal(TimeSpan.FromSeconds(5400), detail.Album.Duration);
        Assert.True(detail.Album.IsStarred);

        Assert.Equal(2, detail.Songs.Count);
        var full = detail.Songs[0];
        Assert.Equal("s-1", full.Id);
        Assert.Equal("Custard Pie", full.Title);
        Assert.Equal("Led Zeppelin", full.Artist);
        Assert.Equal("Physical Graffiti", full.Album);
        Assert.Equal(TimeSpan.FromSeconds(253), full.Duration);
        Assert.Equal(1, full.Track);
        Assert.Equal(1, full.DiscNumber);
        Assert.Equal("flac", full.Format);
        Assert.Equal(1044, full.BitRate);
        Assert.Equal("audio/flac", full.ContentType);
        Assert.True(full.IsStarred);

        var sparse = detail.Songs[1];
        Assert.Null(sparse.Artist);
        Assert.Null(sparse.Track);
        Assert.Null(sparse.DiscNumber);
        Assert.Null(sparse.Format);
        Assert.Null(sparse.BitRate);
        Assert.False(sparse.IsStarred);
    }

    [Fact]
    public async Task GetAlbum_OrdersSongsByDiscThenTrack()
    {
        // Server order is deliberately scrambled; disc 2 track 1 arrives first
        // and one song has no track number at all.
        var songs =
            "<song id=\"s-d2t1\" title=\"D2 T1\" discNumber=\"2\" track=\"1\" duration=\"1\"/>" +
            "<song id=\"s-d1t2\" title=\"D1 T2\" discNumber=\"1\" track=\"2\" duration=\"1\"/>" +
            "<song id=\"s-d1t1\" title=\"D1 T1\" discNumber=\"1\" track=\"1\" duration=\"1\"/>" +
            "<song id=\"s-untracked\" title=\"No Track\" discNumber=\"1\" duration=\"1\"/>";
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Album(AlbumAttrs, songs)));
        var client = TestHelpers.CreateClient(handler);

        var detail = await client.GetAlbumAsync("al-1");

        // Missing disc defaults to 1; missing track sorts last within its disc (§9).
        Assert.Equal(
            new[] { "s-d1t1", "s-d1t2", "s-untracked", "s-d2t1" },
            detail.Songs.Select(s => s.Id).ToArray());
    }

    [Fact]
    public async Task GetAlbum_AlbumWithNoSongs_ReturnsEmptyList()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Album("id=\"al-9\" name=\"Empty\" songCount=\"0\" duration=\"0\"", string.Empty)));
        var client = TestHelpers.CreateClient(handler);

        var detail = await client.GetAlbumAsync("al-9");

        Assert.Empty(detail.Songs);
        Assert.False(detail.Album.IsStarred);
    }

    [Fact]
    public async Task GetAlbum_MissingAlbumElement_SurfacesConnectionError()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(Canned.Ok()));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<SubsonicConnectionException>(() => client.GetAlbumAsync("al-1"));
    }

    [Fact]
    public async Task GetAlbum_RejectsEmptyId()
    {
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Ok())));

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetAlbumAsync(" "));
    }

    [Fact]
    public async Task GetAlbum_ServerError_SurfacesAsApiException()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Error(70, "Album not found.")));
        var client = TestHelpers.CreateClient(handler);

        var ex = await Assert.ThrowsAsync<SubsonicApiException>(() => client.GetAlbumAsync("nope"));

        Assert.Equal(SubsonicErrorCode.DataNotFound, ex.ErrorCode);
    }
}
