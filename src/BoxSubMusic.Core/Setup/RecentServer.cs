using System;

namespace BoxSubMusic.Core.Setup;

/// <summary>
/// One remembered server connection for the setup flow's recent-servers list
/// (requirements §4). Never carries a secret: the password lives only in the
/// Credential Locker, and only for the active server.
/// </summary>
public sealed class RecentServer
{
    public RecentServer(string url, string username, DateTimeOffset lastUsed)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("A server URL is required.", nameof(url));
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("A username is required.", nameof(username));

        Url = url;
        Username = username;
        LastUsed = lastUsed;
    }

    public string Url { get; }
    public string Username { get; }
    public DateTimeOffset LastUsed { get; }
}
