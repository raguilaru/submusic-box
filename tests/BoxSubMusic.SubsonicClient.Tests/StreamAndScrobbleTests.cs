namespace BoxSubMusic.SubsonicClient.Tests;

public class StreamAndScrobbleTests
{
    [Fact]
    public void GetStreamUri_BuildsAuthenticatedUrl()
    {
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP expected")));

        var uri = client.GetStreamUri("tr-1");

        Assert.Equal("/rest/stream", TestHelpers.EndpointOf(uri));
        var query = TestHelpers.QueryOf(uri);
        Assert.Equal("tr-1", query["id"]);
        Assert.Equal("alice", query["u"]);
        Assert.Equal(TestHelpers.Md5Hex(TestHelpers.DefaultPassword + query["s"]), query["t"]);
        Assert.Equal(SubsonicApiClient.ClientName, query["c"]);
    }

    [Fact]
    public void GetStreamUri_NoTranscodingOverridesByDefault()
    {
        // The default sends no format/maxBitRate: the server decides the
        // delivered stream, including any server-side bitrate restriction —
        // the authoritative quality control (§16 decision 2026-07-19; see
        // docs/architecture/30-playback-architecture.md).
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP expected")));

        var query = TestHelpers.QueryOf(client.GetStreamUri("tr-1"));

        Assert.False(query.ContainsKey("format"));
        Assert.False(query.ContainsKey("maxBitRate"));
    }

    [Fact]
    public void GetStreamUri_Format_SendsParam()
    {
        // The decode-failure fallback path (§16 decision 2026-07-19): pin
        // format=mp3; the server still applies its own bitrate cap.
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP expected")));

        var query = TestHelpers.QueryOf(client.GetStreamUri("tr-1", format: "mp3"));

        Assert.Equal("mp3", query["format"]);
        Assert.False(query.ContainsKey("maxBitRate"));
    }

    [Fact]
    public void GetStreamUri_MaxBitRate_SendsParam()
    {
        // Protocol completeness (core API 1.2.0); the app itself never
        // sends a client-side cap.
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP expected")));

        var query = TestHelpers.QueryOf(client.GetStreamUri("tr-1", maxBitRate: 192));

        Assert.Equal("192", query["maxBitRate"]);
        Assert.False(query.ContainsKey("format"));
    }

    [Fact]
    public void GetStreamUri_NonPositiveMaxBitRate_Throws()
    {
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP expected")));

        Assert.Throws<ArgumentOutOfRangeException>(() => client.GetStreamUri("tr-1", maxBitRate: 0));
    }

    [Fact]
    public void GetStreamUri_EmptyId_Throws()
    {
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP expected")));

        Assert.Throws<ArgumentException>(() => client.GetStreamUri(" "));
    }

    [Fact]
    public async Task Scrobble_SendsIdAndSubmission()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(Canned.Ok()));
        var client = TestHelpers.CreateClient(handler);

        await client.ScrobbleAsync("tr-1");

        Assert.Equal("/rest/scrobble", TestHelpers.EndpointOf(handler.Requests[0]));
        var query = TestHelpers.QueryOf(handler.Requests[0]);
        Assert.Equal("tr-1", query["id"]);
        Assert.Equal("true", query["submission"]);
    }

    [Fact]
    public async Task Scrobble_NowPlayingVariant_SendsSubmissionFalse()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(Canned.Ok()));
        var client = TestHelpers.CreateClient(handler);

        await client.ScrobbleAsync("tr-1", submission: false);

        Assert.Equal("false", TestHelpers.QueryOf(handler.Requests[0])["submission"]);
    }

    [Fact]
    public async Task Scrobble_ServerError_Throws()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Error(70, "The requested data was not found.")));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<SubsonicApiException>(() => client.ScrobbleAsync("gone"));
    }

    [Fact]
    public async Task Scrobble_EmptyId_Throws()
    {
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP expected")));

        await Assert.ThrowsAsync<ArgumentException>(() => client.ScrobbleAsync(""));
    }
}
