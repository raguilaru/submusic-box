using System;
using System.Collections.Generic;

namespace BoxSubMusic.SubsonicClient.Models;

/// <summary>
/// One structured-lyrics entry for a song, from the OpenSubsonic
/// <c>songLyrics</c> extension (<c>getLyricsBySongId</c>). A song may return
/// several entries (e.g. a synced and an unsynced version, or several
/// languages); the caller picks the one it wants.
/// </summary>
public sealed class StructuredLyrics
{
    public StructuredLyrics(
        bool synced,
        string? lang,
        int offsetMs,
        string? displayArtist,
        string? displayTitle,
        IReadOnlyList<LyricLine> lines)
    {
        Synced = synced;
        Lang = lang;
        OffsetMs = offsetMs;
        DisplayArtist = displayArtist;
        DisplayTitle = displayTitle;
        Lines = lines ?? throw new ArgumentNullException(nameof(lines));
    }

    /// <summary>True when the lines carry per-line <see cref="LyricLine.StartMs"/> timestamps.</summary>
    public bool Synced { get; }

    /// <summary>Language code (ideally ISO 639); "xxx" or null when unspecified.</summary>
    public string? Lang { get; }

    /// <summary>Global timing offset in milliseconds applied to every line's start.</summary>
    public int OffsetMs { get; }

    /// <summary>Artist name as carried by the lyrics entry, when present.</summary>
    public string? DisplayArtist { get; }

    /// <summary>Song title as carried by the lyrics entry, when present.</summary>
    public string? DisplayTitle { get; }

    /// <summary>The lyric lines in order.</summary>
    public IReadOnlyList<LyricLine> Lines { get; }
}

/// <summary>
/// A single lyric line. <see cref="StartMs"/> is present only for synced
/// lyrics; unsynced lines carry text only.
/// </summary>
public sealed class LyricLine
{
    public LyricLine(int? startMs, string text)
    {
        StartMs = startMs;
        Text = text ?? string.Empty;
    }

    /// <summary>Line start time in milliseconds from the track start; null when unsynced.</summary>
    public int? StartMs { get; }

    /// <summary>The line text (may be empty for an instrumental/blank line).</summary>
    public string Text { get; }
}
