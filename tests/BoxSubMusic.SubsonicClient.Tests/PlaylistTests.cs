using BoxSubMusic.SubsonicClient;

namespace BoxSubMusic.SubsonicClient.Tests;

public class PlaylistTests
{
    [Fact]
    public async Task GetPlaylists_MapsSummariesAndCoverArt()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Xml(Canned.Playlists(Canned.TwoPlaylists)));
        var client = TestHelpers.CreateClient(handler);

        var playlists = await client.GetPlaylistsAsync();

        Assert.Equal("/rest/getPlaylists", TestHelpers.EndpointOf(handler.Requests[0]));
        Assert.Equal(new[] { "Roadtrip", "Focus" }, playlists.Select(p => p.Name).ToArray());

        var roadtrip = playlists[0];
        Assert.Equal("pl-1", roadtrip.Id);
        Assert.Equal("test", roadtrip.Owner);
        Assert.Equal("windows down", roadtrip.Comment);
        Assert.Equal(12, roadtrip.SongCount);
        Assert.Equal(2880, roadtrip.Duration.TotalSeconds);
        Assert.Equal("pl-1", roadtrip.CoverArtId);

        // No coverArt attribute -> null (so the tile shows a placeholder).
        Assert.Null(playlists[1].CoverArtId);
        Assert.Equal(0, playlists[1].SongCount);
    }

    [Fact]
    public async Task GetPlaylists_MissingPlaylistsElement_SurfacesConnectionError()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(Canned.Ok()));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<SubsonicConnectionException>(() => client.GetPlaylistsAsync());
    }

    [Fact]
    public async Task GetPlaylist_MapsEntriesAsSongsInStoredOrder()
    {
        var entries =
            "<entry id=\"s-1\" title=\"One\" artist=\"A\" album=\"First\" albumId=\"al-1\" coverArt=\"al-1\" duration=\"200\"/>" +
            "<entry id=\"s-2\" title=\"Two\" artist=\"B\" album=\"Second\" albumId=\"al-2\" coverArt=\"al-2\" duration=\"180\"/>";
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(
            Canned.Playlist("id=\"pl-1\" name=\"Roadtrip\" songCount=\"2\" duration=\"380\" coverArt=\"pl-1\"", entries)));
        var client = TestHelpers.CreateClient(handler);

        var detail = await client.GetPlaylistAsync("pl-1");

        Assert.Equal("/rest/getPlaylist", TestHelpers.EndpointOf(handler.Requests[0]));
        Assert.Equal("pl-1", TestHelpers.QueryOf(handler.Requests[0])["id"]);

        Assert.Equal("Roadtrip", detail.Playlist.Name);
        Assert.Equal(2, detail.Playlist.SongCount);
        // Stored order is preserved (no client re-sorting for playlists).
        Assert.Equal(new[] { "One", "Two" }, detail.Songs.Select(s => s.Title).ToArray());
        Assert.Equal("al-1", detail.Songs[0].AlbumId);
        Assert.Equal("al-1", detail.Songs[0].CoverArtId);
    }

    [Fact]
    public async Task GetPlaylist_EmptyPlaylist_HasNoSongs()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(
            Canned.Playlist("id=\"pl-2\" name=\"Focus\" songCount=\"0\" duration=\"0\"", string.Empty)));
        var client = TestHelpers.CreateClient(handler);

        var detail = await client.GetPlaylistAsync("pl-2");

        Assert.Empty(detail.Songs);
        Assert.Equal("Focus", detail.Playlist.Name);
    }

    [Fact]
    public async Task GetPlaylist_MissingPlaylistElement_SurfacesConnectionError()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(Canned.Ok()));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<SubsonicConnectionException>(() => client.GetPlaylistAsync("pl-1"));
    }

    [Fact]
    public async Task GetPlaylist_RejectsEmptyId()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(Canned.Ok()));
        var client = TestHelpers.CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetPlaylistAsync("  "));
    }
}
