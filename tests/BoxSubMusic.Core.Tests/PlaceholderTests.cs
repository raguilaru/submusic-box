using BoxSubMusic.Core;

namespace BoxSubMusic.Core.Tests;

public class PlaceholderTests
{
    [Fact]
    public void CoreLibraryIsReferenced()
    {
        Assert.Equal("BoxSubMusic.Core", Placeholder.LibraryName);
    }
}
