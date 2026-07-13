using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.SubsonicClient.Tests;

public class SongListTests
{
    [Fact]
    public async Task GetSongs_SendsEmptyQuerySearch3AndMapsSongs()
    {
        var songs =
            "<song id=\"s-1\" title=\"Young American Heart\" artist=\"Benson Boone\" artistId=\"ar-1\" " +
            "album=\"American Heart\" albumId=\"al-1\" coverArt=\"mf-1\" duration=\"172\" track=\"10\" " +
            "discNumber=\"1\" year=\"2025\" suffix=\"m4a\" bitRate=\"256\" contentType=\"audio/mp4\"/>" +
            "<song id=\"s-2\" title=\"Sparse\" duration=\"0\"/>";
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.SearchResult3(songs)));
        var client = TestHelpers.CreateClient(handler);

        var page = await client.GetSongsAsync(size: 50, offset: 100);

        var query = TestHelpers.QueryOf(handler.Requests[0]);
        Assert.Equal("/rest/search3", TestHelpers.EndpointOf(handler.Requests[0]));
        // OpenSubsonic full-library listing: empty query, songs only.
        Assert.Equal("", query["query"]);
        Assert.Equal("50", query["songCount"]);
        Assert.Equal("100", query["songOffset"]);
        Assert.Equal("0", query["artistCount"]);
        Assert.Equal("0", query["albumCount"]);

        Assert.Equal(2, page.Count);
        var full = page[0];
        Assert.Equal("s-1", full.Id);
        Assert.Equal("Young American Heart", full.Title);
        Assert.Equal("Benson Boone", full.Artist);
        Assert.Equal("American Heart", full.Album);
        Assert.Equal(TimeSpan.FromSeconds(172), full.Duration);
        Assert.Equal("m4a", full.Format);
        Assert.Equal(256, full.BitRate);

        // Server order is preserved: search3 has no client-controlled sort.
        Assert.Equal("s-2", page[1].Id);
        Assert.Null(page[1].Format);
        Assert.Null(page[1].BitRate);
    }

    [Fact]
    public async Task GetSongs_EmptyLibrary_ReturnsEmptyList()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.SearchResult3(string.Empty)));
        var client = TestHelpers.CreateClient(handler);

        var page = await client.GetSongsAsync();

        Assert.Empty(page);
    }

    [Fact]
    public async Task GetSongs_MissingSearchResultElement_SurfacesConnectionError()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(Canned.Ok()));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<SubsonicConnectionException>(() => client.GetSongsAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task GetSongs_RejectsSizeOutsideLimit(int size)
    {
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.SearchResult3(string.Empty))));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetSongsAsync(size: size));
    }

    [Fact]
    public async Task GetSongs_ServerError_SurfacesAsApiException()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Error(0, "A generic failure.")));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<SubsonicApiException>(() => client.GetSongsAsync());
    }
}
