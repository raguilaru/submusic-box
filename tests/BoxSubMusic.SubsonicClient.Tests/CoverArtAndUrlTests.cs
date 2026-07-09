namespace BoxSubMusic.SubsonicClient.Tests;

public class CoverArtAndUrlTests
{
    [Fact]
    public void GetCoverArtUri_BuildsAuthenticatedUrl()
    {
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP expected")));

        var uri = client.GetCoverArtUri("al-1", size: 600);

        Assert.Equal("/rest/getCoverArt", TestHelpers.EndpointOf(uri));
        var query = TestHelpers.QueryOf(uri);
        Assert.Equal("al-1", query["id"]);
        Assert.Equal("600", query["size"]);
        Assert.Equal("alice", query["u"]);
        Assert.Equal(TestHelpers.Md5Hex(TestHelpers.DefaultPassword + query["s"]), query["t"]);
        Assert.Equal("BoxSubMusic", query["c"]);
    }

    [Fact]
    public void GetCoverArtUri_WithoutSize_OmitsSizeParam()
    {
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP expected")));

        var query = TestHelpers.QueryOf(client.GetCoverArtUri("al-1"));

        Assert.False(query.ContainsKey("size"));
    }

    [Fact]
    public void GetCoverArtUri_EmptyId_Throws()
    {
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP expected")));

        Assert.Throws<ArgumentException>(() => client.GetCoverArtUri(" "));
    }

    [Fact]
    public async Task BaseUrlWithPath_IsPreservedInRequests()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.PingOkPlain()));
        var client = TestHelpers.CreateClient(handler, baseUrl: "https://home.example/navidrome");

        await client.ConnectAsync();

        Assert.Equal("/navidrome/rest/ping", TestHelpers.EndpointOf(handler.Requests[0]));
    }

    [Fact]
    public async Task BaseUrlWithTrailingSlash_DoesNotDoubleTheSlash()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.PingOkPlain()));
        var client = TestHelpers.CreateClient(handler, baseUrl: "http://music.example:4533/");

        await client.ConnectAsync();

        Assert.Equal("/rest/ping", TestHelpers.EndpointOf(handler.Requests[0]));
    }

    [Fact]
    public void Credentials_RejectNonHttpUrl()
    {
        Assert.Throws<ArgumentException>(() =>
            new SubsonicServerCredentials(new Uri("ftp://music.example"), "alice", "secret"));
    }
}
