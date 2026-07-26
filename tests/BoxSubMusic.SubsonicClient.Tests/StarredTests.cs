namespace BoxSubMusic.SubsonicClient.Tests;

public class StarredTests
{
    [Fact]
    public async Task GetStarred2_MapsAllThreeScopes()
    {
        var inner =
            "<artist id=\"ar-1\" name=\"Air\" coverArt=\"ar-1\" albumCount=\"2\" starred=\"2026-05-06T07:08:09Z\"/>" +
            "<artist id=\"ar-2\" name=\"Beck\" albumCount=\"5\" starred=\"2026-05-07T07:08:09Z\"/>" +
            "<album id=\"al-1\" name=\"Moon Safari\" artist=\"Air\" artistId=\"ar-1\" coverArt=\"al-1\" " +
            "songCount=\"10\" duration=\"1800\" year=\"1998\" starred=\"2026-05-06T07:08:09Z\"/>" +
            "<song id=\"s-1\" title=\"La Femme d'Argent\" artist=\"Air\" artistId=\"ar-1\" album=\"Moon Safari\" " +
            "albumId=\"al-1\" duration=\"429\" track=\"1\" discNumber=\"1\" suffix=\"flac\" bitRate=\"1024\" " +
            "created=\"2025-11-02T10:20:30Z\" starred=\"2026-05-06T07:08:09Z\"/>";
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Starred2(inner)));
        var client = TestHelpers.CreateClient(handler);

        var starred = await client.GetStarred2Async();

        Assert.Equal("/rest/getStarred2", TestHelpers.EndpointOf(handler.Requests[0]));

        Assert.Equal(2, starred.Artists.Count);
        Assert.Equal("Air", starred.Artists[0].Name);
        Assert.Equal(2, starred.Artists[0].AlbumCount);
        Assert.Equal(5, starred.Artists[1].AlbumCount);
        Assert.All(starred.Artists, a => Assert.True(a.IsStarred));

        var album = Assert.Single(starred.Albums);
        Assert.Equal("al-1", album.Id);
        Assert.Equal(1998, album.Year);
        Assert.True(album.IsStarred);

        var song = Assert.Single(starred.Songs);
        Assert.Equal("s-1", song.Id);
        Assert.Equal("al-1", song.AlbumId);
        Assert.True(song.IsStarred);
        // "Date Added" sorts on this; the tags carry it only for endpoints
        // whose entries include `created`.
        Assert.Equal(
            new DateTimeOffset(2025, 11, 2, 10, 20, 30, TimeSpan.Zero),
            song.Created);
    }

    [Fact]
    public async Task GetStarred2_NoFavorites_ReturnsEmptyScopes()
    {
        // A user with nothing starred gets an empty <starred2/> — not an
        // error, and not a missing element.
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Starred2(string.Empty)));
        var client = TestHelpers.CreateClient(handler);

        var starred = await client.GetStarred2Async();

        Assert.Empty(starred.Artists);
        Assert.Empty(starred.Albums);
        Assert.Empty(starred.Songs);
    }

    [Fact]
    public async Task GetStarred2_MissingStarredElement_SurfacesConnectionError()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(Canned.Ok()));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<SubsonicConnectionException>(() => client.GetStarred2Async());
    }

    [Fact]
    public async Task GetStarred2_ServerError_SurfacesAsApiException()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Error(50, "User is not authorized.")));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<SubsonicApiException>(() => client.GetStarred2Async());
    }
}
