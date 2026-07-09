using BoxSubMusic.Core.Setup;

namespace BoxSubMusic.Core.Tests;

/// <summary>Requirements §4: URL sanitization rules for the setup flow.</summary>
public class ServerUrlSanitizerTests
{
    [Fact]
    public void TrimsSurroundingWhitespace()
    {
        var uri = ServerUrlSanitizer.Sanitize("  https://music.example.com \t");

        Assert.NotNull(uri);
        Assert.Equal("https://music.example.com/", uri!.AbsoluteUri);
    }

    [Fact]
    public void DefaultsToHttpsWhenSchemeIsMissing()
    {
        var uri = ServerUrlSanitizer.Sanitize("music.example.com");

        Assert.NotNull(uri);
        Assert.Equal("https", uri!.Scheme);
        Assert.Equal("https://music.example.com/", uri.AbsoluteUri);
    }

    [Fact]
    public void DefaultsToHttpsForBareHostWithPortAndPath()
    {
        var uri = ServerUrlSanitizer.Sanitize("raspberrypi.local:4533/music");

        Assert.NotNull(uri);
        Assert.Equal("https://raspberrypi.local:4533/music", uri!.AbsoluteUri);
    }

    [Fact]
    public void KeepsExplicitHttpSchemeAndPort()
    {
        var uri = ServerUrlSanitizer.Sanitize("http://10.0.0.5:4533");

        Assert.NotNull(uri);
        Assert.Equal("http", uri!.Scheme);
        Assert.Equal(4533, uri.Port);
    }

    [Fact]
    public void StripsTrailingSlashes()
    {
        var uri = ServerUrlSanitizer.Sanitize("https://music.example.com/subsonic///");

        Assert.NotNull(uri);
        Assert.Equal("https://music.example.com/subsonic", uri!.AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("///")]
    [InlineData("https://")]
    [InlineData("ftp://music.example.com")]
    [InlineData("https://bad host.example.com")]
    [InlineData("https://music.example.com/rest?f=json")]
    [InlineData("https://music.example.com/app/#/album")]
    public void RejectsInvalidShapes(string? rawInput)
    {
        Assert.Null(ServerUrlSanitizer.Sanitize(rawInput));
    }
}
