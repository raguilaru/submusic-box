using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.SubsonicClient.Tests;

public class AuthenticationTests
{
    [Fact]
    public async Task TokenAuth_SendsSaltedTokenAndCommonParams_NeverThePassword()
    {
        var handler = new FakeHttpMessageHandler(uri =>
            TestHelpers.EndpointOf(uri) == "/rest/ping"
                ? FakeHttpMessageHandler.Xml(Canned.PingOkOpenSubsonic())
                : FakeHttpMessageHandler.Xml(Canned.Extensions()));
        var client = TestHelpers.CreateClient(handler);

        await client.ConnectAsync();

        var query = TestHelpers.QueryOf(handler.Requests[0]);
        Assert.Equal("alice", query["u"]);
        Assert.Equal("1.16.1", query["v"]);
        Assert.Equal("BoxSubMusic", query["c"]);
        Assert.False(query.ContainsKey("p"));
        Assert.Matches("^[0-9a-f]{32}$", query["t"]);
        Assert.Equal(TestHelpers.Md5Hex(TestHelpers.DefaultPassword + query["s"]), query["t"]);
    }

    [Fact]
    public async Task TokenAuth_UsesFreshSaltPerRequest()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.AlbumList2(Canned.TwoAlbums)));
        var client = TestHelpers.CreateClient(handler);

        await client.GetAlbumList2Async(AlbumListType.Newest);
        await client.GetAlbumList2Async(AlbumListType.Newest);

        var firstSalt = TestHelpers.QueryOf(handler.Requests[0])["s"];
        var secondSalt = TestHelpers.QueryOf(handler.Requests[1])["s"];
        Assert.NotEqual(firstSalt, secondSalt);
    }
}
