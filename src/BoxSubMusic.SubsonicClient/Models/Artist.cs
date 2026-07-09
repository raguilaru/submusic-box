using System;

namespace BoxSubMusic.SubsonicClient.Models;

/// <summary>An artist (ArtistID3) as returned by getArtists and getArtist.</summary>
public sealed class Artist
{
    public Artist(
        string id,
        string name,
        string? coverArtId,
        int albumCount,
        bool isStarred)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        CoverArtId = coverArtId;
        AlbumCount = albumCount;
        IsStarred = isStarred;
    }

    public string Id { get; }
    public string Name { get; }

    /// <summary>Cover art id for getCoverArt; null when the server has no artwork for this artist.</summary>
    public string? CoverArtId { get; }

    public int AlbumCount { get; }

    /// <summary>Favorite state, from the presence of the ArtistID3 <c>starred</c> attribute (§8).</summary>
    public bool IsStarred { get; }
}
