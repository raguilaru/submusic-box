using System;

namespace BoxSubMusic.SubsonicClient.Models;

/// <summary>An album (AlbumID3) as returned by getAlbumList2 and related endpoints.</summary>
public sealed class Album
{
    public Album(
        string id,
        string name,
        string? artist,
        string? artistId,
        string? coverArtId,
        int songCount,
        TimeSpan duration,
        DateTimeOffset? created,
        int? year,
        string? genre,
        bool isStarred)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Artist = artist;
        ArtistId = artistId;
        CoverArtId = coverArtId;
        SongCount = songCount;
        Duration = duration;
        Created = created;
        Year = year;
        Genre = genre;
        IsStarred = isStarred;
    }

    public string Id { get; }
    public string Name { get; }
    public string? Artist { get; }
    public string? ArtistId { get; }

    /// <summary>Cover art id for getCoverArt; null when the server has no artwork for this album.</summary>
    public string? CoverArtId { get; }

    public int SongCount { get; }
    public TimeSpan Duration { get; }
    public DateTimeOffset? Created { get; }
    public int? Year { get; }
    public string? Genre { get; }

    /// <summary>Favorite flag (§9): a <c>starred</c> timestamp is present only on favorites.</summary>
    public bool IsStarred { get; }
}
