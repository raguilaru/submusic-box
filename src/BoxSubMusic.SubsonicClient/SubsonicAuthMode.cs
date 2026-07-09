namespace BoxSubMusic.SubsonicClient;

/// <summary>How credentials are sent to the server (requirements §3).</summary>
public enum SubsonicAuthMode
{
    /// <summary>Salted token auth: t = md5(password + salt). Available since API 1.13.0.</summary>
    Token,

    /// <summary>Hex-encoded password (p=enc:...). Explicit fallback for pre-1.13 servers only.</summary>
    LegacyPassword,
}
