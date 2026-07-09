using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.SubsonicClient.Tests;

public class ConnectTests
{
    [Fact]
    public async Task Connect_OpenSubsonicServer_DetectsCapabilitiesAndExtensions()
    {
        var handler = new FakeHttpMessageHandler(uri =>
            TestHelpers.EndpointOf(uri) == "/rest/ping"
                ? FakeHttpMessageHandler.Xml(Canned.PingOkOpenSubsonic())
                : FakeHttpMessageHandler.Xml(Canned.Extensions()));
        var client = TestHelpers.CreateClient(handler);

        var capabilities = await client.ConnectAsync();

        Assert.True(capabilities.IsOpenSubsonic);
        Assert.Equal("navidrome", capabilities.ServerType);
        Assert.Equal("1.16.1", capabilities.ApiVersion);
        Assert.Equal(SubsonicAuthMode.Token, capabilities.AuthMode);
        Assert.True(capabilities.SupportsExtension("indexBasedQueue"));
        Assert.True(capabilities.SupportsExtension("songLyrics", 2));
        Assert.False(capabilities.SupportsExtension("transcodeOffset"));
        Assert.Equal("/rest/getOpenSubsonicExtensions", TestHelpers.EndpointOf(handler.Requests[1]));
        Assert.Same(capabilities, client.Capabilities);
    }

    [Fact]
    public async Task Connect_PlainSubsonicServer_SkipsExtensionsCall()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.PingOkPlain()));
        var client = TestHelpers.CreateClient(handler);

        var capabilities = await client.ConnectAsync();

        Assert.False(capabilities.IsOpenSubsonic);
        Assert.Empty(capabilities.Extensions);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Connect_Pre113Server_FallsBackToLegacyPasswordAuth()
    {
        // A pre-1.13 server rejects the token params; the retry must carry the
        // hex-encoded password and the server's own API version.
        var handler = new FakeHttpMessageHandler(uri =>
            TestHelpers.QueryOf(uri).ContainsKey("t")
                ? FakeHttpMessageHandler.Xml(Canned.Error(40, "Wrong username or password.", "1.10.2"))
                : TestHelpers.EndpointOf(uri) == "/rest/getAlbumList2"
                    ? FakeHttpMessageHandler.Xml(Canned.AlbumList2(string.Empty))
                    : FakeHttpMessageHandler.Xml(Canned.PingOkPlain("1.10.2")));
        var client = TestHelpers.CreateClient(handler);

        var capabilities = await client.ConnectAsync();

        Assert.Equal(SubsonicAuthMode.LegacyPassword, capabilities.AuthMode);
        Assert.Equal("1.10.2", capabilities.ApiVersion);
        var retryQuery = TestHelpers.QueryOf(handler.Requests[1]);
        Assert.Equal("enc:" + TestHelpers.HexOf(TestHelpers.DefaultPassword), retryQuery["p"]);
        Assert.Equal("1.10.2", retryQuery["v"]);
        Assert.False(retryQuery.ContainsKey("t"));

        // Later calls keep the negotiated auth mode and version.
        await client.GetAlbumList2Async(AlbumListType.Random);
        var laterQuery = TestHelpers.QueryOf(handler.Requests[2]);
        Assert.True(laterQuery.ContainsKey("p"));
        Assert.Equal("1.10.2", laterQuery["v"]);
    }

    [Fact]
    public async Task Connect_ModernServerRejectsCredentials_ThrowsWithoutLegacyRetry()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Error(40, "Wrong username or password.")));
        var client = TestHelpers.CreateClient(handler);

        var ex = await Assert.ThrowsAsync<SubsonicApiException>(() => client.ConnectAsync());

        Assert.Equal(SubsonicErrorCode.WrongUsernameOrPassword, ex.ErrorCode);
        // The password must never be sent to a token-capable server.
        Assert.All(handler.Requests, uri => Assert.False(TestHelpers.QueryOf(uri).ContainsKey("p")));
    }

    [Fact]
    public async Task Connect_ServerOlderThanClient_AdoptsServerApiVersion()
    {
        var handler = new FakeHttpMessageHandler(uri =>
            TestHelpers.QueryOf(uri)["v"] == "1.14.0"
                ? FakeHttpMessageHandler.Xml(Canned.PingOkPlain("1.14.0"))
                : FakeHttpMessageHandler.Xml(Canned.Error(30, "Incompatible Subsonic REST protocol version. Server must upgrade.", "1.14.0")));
        var client = TestHelpers.CreateClient(handler);

        var capabilities = await client.ConnectAsync();

        // 1.14 still supports token auth, so only the declared version changes.
        Assert.Equal(SubsonicAuthMode.Token, capabilities.AuthMode);
        Assert.Equal("1.14.0", capabilities.ApiVersion);
        Assert.True(TestHelpers.QueryOf(handler.Requests[1]).ContainsKey("t"));
    }

    [Fact]
    public async Task Connect_UnreachableServer_ThrowsConnectionException()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            throw new HttpRequestException("No such host is known."));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<SubsonicConnectionException>(() => client.ConnectAsync());
    }

    [Fact]
    public async Task Connect_NonSubsonicAnswer_ThrowsConnectionException()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml("<html><body>It works!</body></html>"));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<SubsonicConnectionException>(() => client.ConnectAsync());
    }

    [Fact]
    public async Task Connect_HttpErrorStatus_ThrowsConnectionException()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<SubsonicConnectionException>(() => client.ConnectAsync());
    }
}
