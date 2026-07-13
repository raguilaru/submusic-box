using System;
using System.Collections.Generic;

namespace BoxSubMusic.SubsonicClient.Models;

/// <summary>
/// getAlbum result (requirements §9): the album plus its songs in disc/track
/// order.
/// </summary>
public sealed class AlbumDetail
{
    public AlbumDetail(Album album, IReadOnlyList<Song> songs)
    {
        Album = album ?? throw new ArgumentNullException(nameof(album));
        Songs = songs ?? throw new ArgumentNullException(nameof(songs));
    }

    public Album Album { get; }

    /// <summary>The album's songs, ordered by disc number then track number (§9).</summary>
    public IReadOnlyList<Song> Songs { get; }
}
