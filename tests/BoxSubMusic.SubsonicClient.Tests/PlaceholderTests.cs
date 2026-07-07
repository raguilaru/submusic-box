using BoxSubMusic.SubsonicClient;

namespace BoxSubMusic.SubsonicClient.Tests;

public class PlaceholderTests
{
    [Fact]
    public void SubsonicClientLibraryIsReferenced()
    {
        Assert.Equal("BoxSubMusic.SubsonicClient", Placeholder.LibraryName);
    }
}
