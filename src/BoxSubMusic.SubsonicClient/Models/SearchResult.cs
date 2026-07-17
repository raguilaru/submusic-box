using System.Collections.Generic;

namespace BoxSubMusic.SubsonicClient.Models;

/// <summary>
/// One search3 response (requirements §17 resolution 2026-07-19 / Slice 13):
/// artists, albums and songs matching a query, each list in server-defined
/// relevance order. A scope the caller requested with count 0 is an empty
/// list, indistinguishable from "no matches" by design — the caller decides
/// which scopes it asked for.
/// </summary>
public sealed class SearchResult
{
    public SearchResult(
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
