using System;

namespace BoxSubMusic.SubsonicClient.Models;

/// <summary>
/// A playlist summary as returned by getPlaylists (requirements §7.4). The
/// browse grid needs the name and counts; <see cref="CoverArtId"/> is the
/// server's own (possibly composite) art, but §7.4 requires the tile artwork to
/// be composed client-side from the constituent album covers, so this is kept
/// only as reference/metadata and is not used to build the tile.
/// </summary>
public sealed class Playlist
{
    public Playlist(
        string id,
        string name,
        string? owner,
        string? comment,
        int songCount,
        TimeSpan duration,
        string? coverArtId)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Owner = owner;
        Comment = comment;
        SongCount = songCount;
        Duration = duration;
        CoverArtId = coverArtId;
    }

    public string Id { get; }
    public string Name { get; }
    public string? Owner { get; }
    public string? Comment { get; }
    public int SongCount { get; }
    public TimeSpan Duration { get; }

    /// <summary>Server-provided cover art id; null when the server has none.</summary>
    public string? CoverArtId { get; }
}
