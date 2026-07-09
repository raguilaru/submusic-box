using System;

namespace BoxSubMusic.SubsonicClient;

/// <summary>
/// Immutable connection settings for one Subsonic-compatible server.
/// The password is held in memory only; persistent storage is the app's
/// responsibility (Credential Locker, requirements §4).
/// </summary>
public sealed class SubsonicServerCredentials
{
    public SubsonicServerCredentials(Uri baseUrl, string username, string password)
    {
        if (baseUrl == null)
            throw new ArgumentNullException(nameof(baseUrl));
        if (!baseUrl.IsAbsoluteUri ||
            (baseUrl.Scheme != Uri.UriSchemeHttp && baseUrl.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Server URL must be an absolute http(s) URL.", nameof(baseUrl));
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.", nameof(username));
        if (password == null)
            throw new ArgumentNullException(nameof(password));

        BaseUrl = baseUrl;
        Username = username;
        Password = password;
    }

    public Uri BaseUrl { get; }
    public string Username { get; }
    public string Password { get; }
}
