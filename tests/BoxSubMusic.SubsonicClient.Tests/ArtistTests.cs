namespace BoxSubMusic.SubsonicClient.Tests;

public class ArtistTests
{
    [Fact]
    public async Task GetArtists_FlattensIndexesAndMapsArtists()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Artists(Canned.TwoIndexes)));
        var client = TestHelpers.CreateClient(handler);

        var artists = await client.GetArtistsAsync();

        Assert.Equal("/rest/getArtists", TestHelpers.EndpointOf(handler.Requests[0]));

        // Buckets are flattened in server order (A then B).
        Assert.Equal(new[] { "ABBA", "Air", "Beck" }, artists.Select(a => a.Name).ToArray());

        var abba = artists[0];
        Assert.Equal("ar-1", abba.Id);
        Assert.Equal("ar-1", abba.CoverArtId);
        Assert.Equal(3, abba.AlbumCount);
        Assert.False(abba.IsStarred);

        // `starred` timestamp present -> favorite.
        Assert.True(artists[1].IsStarred);

        // No coverArt attribute -> null (so the UI shows a placeholder).
        Assert.Null(artists[2].CoverArtId);
    }

    [Fact]
    public async Task GetArtists_MissingArtistsElement_SurfacesConnectionError()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(Canned.Ok()));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<SubsonicConnectionException>(() => client.GetArtistsAsync());
    }

    [Fact]
    public async Task GetArtist_MapsArtistAlbumsAndSumsSongCount()
    {
        var albums =
            "<album id=\"al-1\" name=\"Debut\" songCount=\"11\" duration=\"2400\" year=\"1993\" coverArt=\"al-1\"/>" +
            "<album id=\"al-2\" name=\"Post\" songCount=\"11\" duration=\"3000\" year=\"1995\" coverArt=\"al-2\"/>";
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(
            Canned.Artist("id=\"ar-9\" name=\"Björk\" coverArt=\"ar-9\" albumCount=\"2\" starred=\"2024-01-01T00:00:00Z\"", albums)));
        var client = TestHelpers.CreateClient(handler);

        var detail = await client.GetArtistAsync("ar-9");

        Assert.Equal("/rest/getArtist", TestHelpers.EndpointOf(handler.Requests[0]));
        Assert.Equal("ar-9", TestHelpers.QueryOf(handler.Requests[0])["id"]);

        Assert.Equal("Björk", detail.Artist.Name);
        Assert.Equal(2, detail.Artist.AlbumCount);
        Assert.True(detail.Artist.IsStarred);
        Assert.Equal(2, detail.Albums.Count);
        // Album order is preserved and song counts are summed for §8's total.
        Assert.Equal(new[] { "Debut", "Post" }, detail.Albums.Select(a => a.Name).ToArray());
        Assert.Equal(22, detail.SongCount);
    }

    [Fact]
    public async Task GetArtist_ArtistWithNoAlbums_HasZeroSongCount()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(
            Canned.Artist("id=\"ar-0\" name=\"Empty\" albumCount=\"0\"", string.Empty)));
        var client = TestHelpers.CreateClient(handler);

        var detail = await client.GetArtistAsync("ar-0");

        Assert.Empty(detail.Albums);
        Assert.Equal(0, detail.SongCount);
        Assert.False(detail.Artist.IsStarred);
    }

    [Fact]
    public async Task GetArtist_RejectsEmptyId()
    {
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(Canned.Ok())));

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetArtistAsync("  "));
    }

    [Theory]
    [InlineData(true, "star")]
    [InlineData(false, "unstar")]
    public async Task StarUnstar_SendsIdToExpectedEndpoint(bool star, string endpoint)
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(Canned.Ok()));
        var client = TestHelpers.CreateClient(handler);

        if (star)
            await client.StarAsync("ar-1");
        else
            await client.UnstarAsync("ar-1");

        Assert.Equal($"/rest/{endpoint}", TestHelpers.EndpointOf(handler.Requests[0]));
        Assert.Equal("ar-1", TestHelpers.QueryOf(handler.Requests[0])["id"]);
    }

    [Fact]
    public async Task Star_ServerError_SurfacesAsApiException()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Error(70, "The requested data was not found.")));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<SubsonicApiException>(() => client.StarAsync("nope"));
    }

    [Fact]
    public async Task StarUnstar_RejectsEmptyId()
    {
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(Canned.Ok())));

        await Assert.ThrowsAsync<ArgumentException>(() => client.StarAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => client.UnstarAsync(""));
    }
}
