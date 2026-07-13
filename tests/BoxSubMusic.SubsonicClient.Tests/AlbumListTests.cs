using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.SubsonicClient.Tests;

public class AlbumListTests
{
    [Fact]
    public async Task GetAlbumList2_SendsExpectedRequestAndMapsAlbums()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.AlbumList2(Canned.TwoAlbums)));
        var client = TestHelpers.CreateClient(handler);

        var albums = await client.GetAlbumList2Async(AlbumListType.Frequent, size: 20, offset: 40);

        var query = TestHelpers.QueryOf(handler.Requests[0]);
        Assert.Equal("/rest/getAlbumList2", TestHelpers.EndpointOf(handler.Requests[0]));
        Assert.Equal("frequent", query["type"]);
        Assert.Equal("20", query["size"]);
        Assert.Equal("40", query["offset"]);

        Assert.Equal(2, albums.Count);
        var full = albums[0];
        Assert.Equal("al-1", full.Id);
        Assert.Equal("Abbey Road", full.Name);
        Assert.Equal("The Beatles", full.Artist);
        Assert.Equal("ar-1", full.ArtistId);
        Assert.Equal("al-1", full.CoverArtId);
        Assert.Equal(17, full.SongCount);
        Assert.Equal(TimeSpan.FromSeconds(2830), full.Duration);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 10, 20, 30, TimeSpan.Zero), full.Created);
        Assert.Equal(1969, full.Year);
        Assert.Equal("Rock", full.Genre);

        var sparse = albums[1];
        Assert.Equal("al-2", sparse.Id);
        Assert.Null(sparse.Artist);
        Assert.Null(sparse.CoverArtId);
        Assert.Null(sparse.Created);
        Assert.Null(sparse.Year);
        Assert.Equal(TimeSpan.Zero, sparse.Duration);
    }

    [Theory]
    [InlineData(AlbumListType.Frequent, "frequent")]
    [InlineData(AlbumListType.Newest, "newest")]
    [InlineData(AlbumListType.Random, "random")]
    [InlineData(AlbumListType.AlphabeticalByName, "alphabeticalByName")]
    public async Task GetAlbumList2_MapsListTypesToApiValues(AlbumListType type, string expected)
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.AlbumList2(string.Empty)));
        var client = TestHelpers.CreateClient(handler);

        await client.GetAlbumList2Async(type);

        Assert.Equal(expected, TestHelpers.QueryOf(handler.Requests[0])["type"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task GetAlbumList2_RejectsSizeOutsideApiLimit(int size)
    {
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.AlbumList2(string.Empty))));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.GetAlbumList2Async(AlbumListType.Newest, size: size));
    }

    [Fact]
    public async Task GetAlbumList2_ServerError_SurfacesAsApiException()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Error(0, "A generic failure.")));
        var client = TestHelpers.CreateClient(handler);

        var ex = await Assert.ThrowsAsync<SubsonicApiException>(
            () => client.GetAlbumList2Async(AlbumListType.Newest));

        Assert.Equal(SubsonicErrorCode.Generic, ex.ErrorCode);
        Assert.Equal("A generic failure.", ex.Message);
    }
}
