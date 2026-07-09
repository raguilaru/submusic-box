using System;
using System.Collections.Generic;

namespace BoxSubMusic.SubsonicClient.Models;

/// <summary>
/// getArtist result (requirements §8): the artist plus its albums. The song
/// count is summed from the albums because ArtistID3 does not carry a reliable
/// total; the album count comes from the artist's own <c>albumCount</c>.
/// </summary>
public sealed class ArtistDetail
{
    public ArtistDetail(Artist artist, IReadOnlyList<Album> albums, int songCount)
    {
        Artist = artist ?? throw new ArgumentNullException(nameof(artist));
        Albums = albums ?? throw new ArgumentNullException(nameof(albums));
        SongCount = songCount;
    }

    public Artist Artist { get; }

    /// <summary>The artist's albums, in the order the server returned them.</summary>
    public IReadOnlyList<Album> Albums { get; }

    /// <summary>Total song count across all albums (§8).</summary>
    public int SongCount { get; }
}
