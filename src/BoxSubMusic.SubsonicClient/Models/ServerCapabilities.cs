using System;
using System.Collections.Generic;
using System.Linq;

namespace BoxSubMusic.SubsonicClient.Models;

/// <summary>
/// What the connected server supports, discovered during ConnectAsync
/// (requirements §3: capability detection over version branching).
/// </summary>
public sealed class ServerCapabilities
{
    public ServerCapabilities(
        string apiVersion,
        string? serverType,
        string? serverVersion,
        bool isOpenSubsonic,
        SubsonicAuthMode authMode,
        IReadOnlyDictionary<string, IReadOnlyList<int>> extensions)
    {
        ApiVersion = apiVersion ?? throw new ArgumentNullException(nameof(apiVersion));
        ServerType = serverType;
        ServerVersion = serverVersion;
        IsOpenSubsonic = isOpenSubsonic;
        AuthMode = authMode;
        Extensions = extensions ?? throw new ArgumentNullException(nameof(extensions));
    }

    /// <summary>The Subsonic API version negotiated with the server.</summary>
    public string ApiVersion { get; }

    /// <summary>OpenSubsonic server type (e.g. "navidrome"); null on legacy Subsonic.</summary>
    public string? ServerType { get; }

    /// <summary>Server software version string; null on legacy Subsonic.</summary>
    public string? ServerVersion { get; }

    public bool IsOpenSubsonic { get; }

    /// <summary>The auth scheme that actually worked against this server.</summary>
    public SubsonicAuthMode AuthMode { get; }

    /// <summary>OpenSubsonic extensions by name, each with its supported versions.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<int>> Extensions { get; }

    public bool SupportsExtension(string name) => Extensions.ContainsKey(name);

    public bool SupportsExtension(string name, int version) =>
        Extensions.TryGetValue(name, out var versions) && versions.Contains(version);
}
