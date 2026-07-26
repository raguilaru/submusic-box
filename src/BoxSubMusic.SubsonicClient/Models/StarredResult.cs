using System.Collections.Generic;

namespace BoxSubMusic.SubsonicClient.Models;

/// <summary>
/// One getStarred2 response: every artist, album and song the signed-in user
/// has starred, in server-defined order. Unlike search3 this endpoint takes no
/// counts or offsets — it returns the complete favorites set in one request,
/// which is what lets the Favorites view sort client-side over everything
/// rather than over "pages loaded so far".
/// </summary>
public sealed class StarredResult
{
    public StarredResult(
        IReadOnlyList<Artist> artists,
        IReadOnlyList<Album> albums,
        IReadOnlyList<Song> songs)
    {
        Artists = artists;
        Albums = albums;
        Songs = songs;
    }

    public IReadOnlyList<Artist> Artists { get; }
    public IReadOnlyList<Album> Albums { get; }
    public IReadOnlyList<Song> Songs { get; }
}
