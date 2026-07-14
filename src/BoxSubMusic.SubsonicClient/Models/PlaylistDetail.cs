using System;
using System.Collections.Generic;

namespace BoxSubMusic.SubsonicClient.Models;

/// <summary>
/// getPlaylist result (requirements §7.4): the playlist plus its songs in
/// stored order. The songs carry the album references the client uses to
/// compose the playlist's tile artwork from constituent album covers.
/// </summary>
public sealed class PlaylistDetail
{
    public PlaylistDetail(Playlist playlist, IReadOnlyList<Song> songs)
    {
        Playlist = playlist ?? throw new ArgumentNullException(nameof(playlist));
        Songs = songs ?? throw new ArgumentNullException(nameof(songs));
    }

    public Playlist Playlist { get; }

    /// <summary>The playlist's songs in stored order.</summary>
    public IReadOnlyList<Song> Songs { get; }
}
