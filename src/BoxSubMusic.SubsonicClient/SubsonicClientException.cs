using System;

namespace BoxSubMusic.SubsonicClient;

/// <summary>Base type for all failures raised by the Subsonic client.</summary>
public abstract class SubsonicClientException : Exception
{
    protected SubsonicClientException(string message)
        : base(message)
    {
    }

    protected SubsonicClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
