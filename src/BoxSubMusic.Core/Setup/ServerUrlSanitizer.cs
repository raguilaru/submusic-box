using System;

namespace BoxSubMusic.Core.Setup;

/// <summary>
/// Sanitizes the raw server-address string typed in the setup flow
/// (requirements §4): trim whitespace, default to https:// when no scheme is
/// given, strip trailing slashes, validate the basic URL shape. The
/// SubsonicClient's credentials type validates but never sanitizes; this is
/// the only place raw user input becomes a URL.
/// </summary>
public static class ServerUrlSanitizer
{
    /// <summary>
    /// Turns raw input into a usable server base URL, or null when the input
    /// has no valid http(s) URL shape (the caller shows inline validation).
    /// </summary>
    public static Uri? Sanitize(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
            return null;

        var candidate = rawInput!.Trim();

        // Bare hosts like "music.example.com:4533/music" default to https.
        // Explicit non-http(s) schemes are rejected below, not rewritten.
        if (candidate.IndexOf("://", StringComparison.Ordinal) < 0)
            candidate = "https://" + candidate;

        candidate = candidate.TrimEnd('/');

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;
        if (string.IsNullOrEmpty(uri.Host))
            return null;
        // A server *base* URL never carries a query or fragment; their
        // presence means something like a pasted web-player URL.
        if (uri.Query.Length > 0 || uri.Fragment.Length > 0)
            return null;

        return uri;
    }
}
