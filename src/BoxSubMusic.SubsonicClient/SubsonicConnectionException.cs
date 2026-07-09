using System;

namespace BoxSubMusic.SubsonicClient;

/// <summary>
/// The server could not be reached, timed out, or did not answer with a
/// Subsonic API response. Distinct from <see cref="SubsonicApiException"/> so
/// the setup flow can show "connection error" vs "authentication error"
/// (acceptance criteria §2).
/// </summary>
public sealed class SubsonicConnectionException : SubsonicClientException
{
    public SubsonicConnectionException(string message)
        : base(message)
    {
    }

    public SubsonicConnectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
