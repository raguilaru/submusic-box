namespace BoxSubMusic.SubsonicClient.Tests;

public class LyricsTests
{
    [Fact]
    public async Task GetLyricsBySongId_MapsSyncedLinesWithMillisecondTimestamps()
    {
        var entry =
            "<structuredLyrics displayArtist=\"Queen\" displayTitle=\"Bohemian Rhapsody\" " +
            "lang=\"eng\" offset=\"-200\" synced=\"true\">" +
            "<line start=\"0\">Is this the real life?</line>" +
            "<line start=\"3500\">Is this just fantasy?</line>" +
            "<line start=\"7250\"></line>" +
            "</structuredLyrics>";
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.LyricsList(entry)));
        var client = TestHelpers.CreateClient(handler);

        var lyrics = await client.GetLyricsBySongIdAsync("s-1");

        Assert.Equal("/rest/getLyricsBySongId", TestHelpers.EndpointOf(handler.Requests[0]));
        Assert.Equal("s-1", TestHelpers.QueryOf(handler.Requests[0])["id"]);

        var only = Assert.Single(lyrics);
        Assert.True(only.Synced);
        Assert.Equal("eng", only.Lang);
        Assert.Equal(-200, only.OffsetMs);
        Assert.Equal("Queen", only.DisplayArtist);
        Assert.Equal("Bohemian Rhapsody", only.DisplayTitle);

        Assert.Equal(3, only.Lines.Count);
        Assert.Equal(0, only.Lines[0].StartMs);
        Assert.Equal("Is this the real life?", only.Lines[0].Text);
        Assert.Equal(3500, only.Lines[1].StartMs);
        Assert.Equal(7250, only.Lines[2].StartMs);
        Assert.Equal(string.Empty, only.Lines[2].Text);
    }

    [Fact]
    public async Task GetLyricsBySongId_UnsyncedLinesHaveNoTimestamps()
    {
        var entry =
            "<structuredLyrics lang=\"xxx\" synced=\"false\">" +
            "<line>First line of the poem</line>" +
            "<line>Second line of the poem</line>" +
            "</structuredLyrics>";
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.LyricsList(entry)));
        var client = TestHelpers.CreateClient(handler);

        var lyrics = await client.GetLyricsBySongIdAsync("s-2");

        var only = Assert.Single(lyrics);
        Assert.False(only.Synced);
        Assert.Equal(0, only.OffsetMs);
        Assert.All(only.Lines, line => Assert.Null(line.StartMs));
        Assert.Equal("First line of the poem", only.Lines[0].Text);
    }

    [Fact]
    public async Task GetLyricsBySongId_ReturnsEveryEntry()
    {
        var entries =
            "<structuredLyrics lang=\"eng\" synced=\"true\"><line start=\"0\">Hello</line></structuredLyrics>" +
            "<structuredLyrics lang=\"fra\" synced=\"false\"><line>Bonjour</line></structuredLyrics>";
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.LyricsList(entries)));
        var client = TestHelpers.CreateClient(handler);

        var lyrics = await client.GetLyricsBySongIdAsync("s-3");

        Assert.Equal(2, lyrics.Count);
        Assert.True(lyrics[0].Synced);
        Assert.False(lyrics[1].Synced);
    }

    [Fact]
    public async Task GetLyricsBySongId_EmptyList_ReturnsEmpty()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.LyricsList(string.Empty)));
        var client = TestHelpers.CreateClient(handler);

        var lyrics = await client.GetLyricsBySongIdAsync("s-4");

        Assert.Empty(lyrics);
    }

    [Fact]
    public async Task GetLyricsBySongId_MissingLyricsListElement_ReturnsEmpty()
    {
        // A legacy/edge server that answers ok with no <lyricsList> is treated
        // as "no lyrics", not an error.
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(Canned.Ok()));
        var client = TestHelpers.CreateClient(handler);

        var lyrics = await client.GetLyricsBySongIdAsync("s-5");

        Assert.Empty(lyrics);
    }

    [Fact]
    public async Task GetLyricsBySongId_RejectsEmptyId()
    {
        var client = TestHelpers.CreateClient(new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Ok())));

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetLyricsBySongIdAsync(" "));
    }
}
