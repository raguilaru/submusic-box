using System;

namespace BoxSubMusic.SubsonicClient.Models;

/// <summary>
/// A song (Child element) as returned by getAlbum and related endpoints.
/// Carries the §7.3 list metadata (title, artist, album, length, format,
/// bitrate) plus the ordering fields the album track list needs (§9).
/// </summary>
public sealed class Song
{
    public Song(
        string id,
        string title,
        string? artist,
        string? artistId,
        string? album,
        string? albumId,
        string? coverArtId,
        TimeSpan duration,
        int? track,
        int? discNumber,
        int? year,
        string? format,
        int? bitRate,
        string? contentType,
        bool isStarred)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Artist = artist;
        ArtistId = artistId;
        Album = album;
        AlbumId = albumId;
        CoverArtId = coverArtId;
        Duration = duration;
        Track = track;
        DiscNumber = discNumber;
        Year = year;
        Format = format;
        BitRate = bitRate;
        ContentType = contentType;
        IsStarred = isStarred;
    }

    public string Id { get; }
    public string Title { get; }
    public string? Artist { get; }
    public string? ArtistId { get; }
    public string? Album { get; }
    public string? AlbumId { get; }

    /// <summary>Cover art id for getCoverArt; null when the server has none.</summary>
    public string? CoverArtId { get; }

    public TimeSpan Duration { get; }

    /// <summary>Track number within its disc; null when the server omits it.</summary>
    public int? Track { get; }

    /// <summary>Disc number for multi-disc albums; null when the server omits it.</summary>
    public int? DiscNumber { get; }

    public int? Year { get; }

    /// <summary>File format from the <c>suffix</c> attribute (e.g. "flac", "mp3").</summary>
    public string? Format { get; }

    /// <summary>Bitrate in kbit/s; null when the server omits it.</summary>
    public int? BitRate { get; }

    public string? ContentType { get; }

    /// <summary>
    /// Favorite flag: a <c>starred</c> timestamp is present only on
    /// favorites. Settable so a successful star/unstar can keep an already
    /// loaded instance honest (the now-playing bar re-reads it on every
    /// track change) without refetching.
    /// </summary>
    public bool IsStarred { get; set; }
}
