using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.SubsonicClient.Tests;

/// <summary>
/// Integration tests against a real server (build plan step 2.1 live
/// verification). Gated on a gitignored liveserver.local.txt — see
/// <see cref="LiveServerConfig"/> — and reported as skipped when absent,
/// so they are inert on machines without the config.
/// </summary>
public class LiveServerTests
{
    [SkippableFact]
    public async Task Connect_ReportsOpenSubsonicCapabilities()
    {
        var (client, _) = CreateClient();

        var capabilities = await client.ConnectAsync();

        Assert.True(capabilities.IsOpenSubsonic, "Expected an OpenSubsonic server.");
        Assert.Equal(SubsonicAuthMode.Token, capabilities.AuthMode);
        Assert.False(string.IsNullOrEmpty(capabilities.ApiVersion));
        Assert.False(string.IsNullOrEmpty(capabilities.ServerType));
    }

    [SkippableFact]
    public async Task AlbumLists_AllThreeHomeTypes_Answer()
    {
        var (client, _) = CreateClient();
        await client.ConnectAsync();

        var newest = await client.GetAlbumList2Async(AlbumListType.Newest, size: 10);
        var frequent = await client.GetAlbumList2Async(AlbumListType.Frequent, size: 10);
        var random = await client.GetAlbumList2Async(AlbumListType.Random, size: 5);

        // The Home view needs albums to exist; frequent may legitimately be
        // empty on a server that has never scrobbled plays.
        Assert.True(newest.Count >= 1, "getAlbumList2 type=newest returned no albums — is the library empty?");
        Assert.True(random.Count <= 5);
        Assert.NotNull(frequent);
        foreach (var album in newest)
        {
            Assert.False(string.IsNullOrEmpty(album.Id));
            Assert.False(string.IsNullOrEmpty(album.Name));
        }
    }

    [SkippableFact]
    public async Task WrongPassword_SurfacesAsAuthenticationError()
    {
        var config = LiveServerConfig.Require();
        using var http = new HttpClient();
        var client = new SubsonicApiClient(
            new SubsonicServerCredentials(config.Url, config.Username, config.Password + "-wrong"),
            http);

        var ex = await Assert.ThrowsAsync<SubsonicApiException>(() => client.ConnectAsync());

        Assert.Equal(SubsonicErrorCode.WrongUsernameOrPassword, ex.ErrorCode);
    }

    [SkippableFact]
    public async Task CoverArtUrl_IsFetchableAsImage()
    {
        var (client, http) = CreateClient();
        await client.ConnectAsync();
        var albums = await client.GetAlbumList2Async(AlbumListType.Newest, size: 10);
        var withArt = albums.FirstOrDefault(a => a.CoverArtId != null);
        Skip.If(withArt == null, "No album with cover art on the server.");

        using var response = await http.GetAsync(client.GetCoverArtUri(withArt!.CoverArtId!, size: 300));

        Assert.True(response.IsSuccessStatusCode, $"getCoverArt returned HTTP {(int)response.StatusCode}.");
        Assert.StartsWith("image/", response.Content.Headers.ContentType?.MediaType);
    }

    private static (SubsonicApiClient Client, HttpClient Http) CreateClient()
    {
        var config = LiveServerConfig.Require();
        var http = new HttpClient();
        return (new SubsonicApiClient(
            new SubsonicServerCredentials(config.Url, config.Username, config.Password), http), http);
    }
}
