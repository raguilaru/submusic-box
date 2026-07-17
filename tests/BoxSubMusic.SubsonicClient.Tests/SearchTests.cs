using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.SubsonicClient.Tests;

public class SearchTests
{
    [Fact]
    public async Task Search_SendsQueryWithAllScopeParamsAndMapsResult()
    {
        var inner =
            "<artist id=\"ar-1\" name=\"Benson Boone\" coverArt=\"ar-1\" albumCount=\"2\"/>" +
            "<artist id=\"ar-2\" name=\"Boonesboro\" albumCount=\"0\" starred=\"2026-01-01T00:00:00Z\"/>" +
            "<album id=\"al-1\" name=\"American Heart\" artist=\"Benson Boone\" artistId=\"ar-1\" " +
            "coverArt=\"al-1\" songCount=\"10\" duration=\"1800\" year=\"2025\"/>" +
            "<song id=\"s-1\" title=\"Young American Heart\" artist=\"Benson Boone\" artistId=\"ar-1\" " +
            "album=\"American Heart\" albumId=\"al-1\" duration=\"172\" suffix=\"m4a\" bitRate=\"256\"/>";
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.SearchResult3(inner)));
        var client = TestHelpers.CreateClient(handler);

        var result = await client.SearchAsync(
            "boone", artistCount: 6, albumCount: 6, songCount: 10);

        var query = TestHelpers.QueryOf(handler.Requests[0]);
        Assert.Equal("/rest/search3", TestHelpers.EndpointOf(handler.Requests[0]));
        Assert.Equal("boone", query["query"]);
        Assert.Equal("6", query["artistCount"]);
        Assert.Equal("0", query["artistOffset"]);
        Assert.Equal("6", query["albumCount"]);
        Assert.Equal("0", query["albumOffset"]);
        Assert.Equal("10", query["songCount"]);
        Assert.Equal("0", query["songOffset"]);

        Assert.Equal(2, result.Artists.Count);
        Assert.Equal("ar-1", result.Artists[0].Id);
        Assert.Equal("Benson Boone", result.Artists[0].Name);
        Assert.False(result.Artists[0].IsStarred);
        Assert.True(result.Artists[1].IsStarred);

        var album = Assert.Single(result.Albums);
        Assert.Equal("al-1", album.Id);
        Assert.Equal("American Heart", album.Name);
        Assert.Equal("ar-1", album.ArtistId);
        Assert.Equal(2025, album.Year);

        var song = Assert.Single(result.Songs);
        Assert.Equal("s-1", song.Id);
        Assert.Equal("al-1", song.AlbumId);
        Assert.Equal("m4a", song.Format);
        Assert.Equal(256, song.BitRate);
    }

    [Fact]
    public async Task Search_SingleScopePage_SendsOffsetsAndZeroCounts()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.SearchResult3(string.Empty)));
        var client = TestHelpers.CreateClient(handler);

        // A scoped page (the incremental Songs filter): only songs, page two.
        var result = await client.SearchAsync(
            "boone", artistCount: 0, albumCount: 0, songCount: 100, songOffset: 100);

        var query = TestHelpers.QueryOf(handler.Requests[0]);
        Assert.Equal("0", query["artistCount"]);
        Assert.Equal("0", query["albumCount"]);
        Assert.Equal("100", query["songCount"]);
        Assert.Equal("100", query["songOffset"]);

        Assert.Empty(result.Artists);
        Assert.Empty(result.Albums);
        Assert.Empty(result.Songs);
    }

    [Fact]
    public async Task Search_RejectsEmptyQuery()
    {
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.SearchResult3(string.Empty))));

        // The empty-query full-library contract belongs to GetSongsAsync.
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SearchAsync("  ", artistCount: 6, albumCount: 6, songCount: 10));
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, 501, 0)]
    [InlineData(0, 0, -1)]
    public async Task Search_RejectsCountsOutsideLimit(int artistCount, int albumCount, int songCount)
    {
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.SearchResult3(string.Empty))));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.SearchAsync("boone", artistCount, albumCount, songCount));
    }

    [Fact]
    public async Task Search_MissingSearchResultElement_SurfacesConnectionError()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(Canned.Ok()));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<SubsonicConnectionException>(
            () => client.SearchAsync("boone", artistCount: 6, albumCount: 6, songCount: 10));
    }

    [Fact]
    public async Task Search_ServerError_SurfacesAsApiException()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Error(0, "A generic failure.")));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<SubsonicApiException>(
            () => client.SearchAsync("boone", artistCount: 6, albumCount: 6, songCount: 10));
    }
}
