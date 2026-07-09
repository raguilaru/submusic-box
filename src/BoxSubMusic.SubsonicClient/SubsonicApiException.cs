using System;
using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.SubsonicClient;

/// <summary>
/// The server answered the API call with a failed status. Carries the
/// server-reported error code so callers can react per case (e.g. wrong
/// credentials in the setup flow).
/// </summary>
public sealed class SubsonicApiException : SubsonicClientException
{
    public SubsonicApiException(int rawErrorCode, string message)
        : base(message)
    {
        RawErrorCode = rawErrorCode;
        ErrorCode = Enum.IsDefined(typeof(SubsonicErrorCode), rawErrorCode)
            ? (SubsonicErrorCode)rawErrorCode
            : SubsonicErrorCode.Generic;
    }

    /// <summary>The error code exactly as reported by the server.</summary>
    public int RawErrorCode { get; }

    /// <summary>The mapped well-known code; unknown codes map to <see cref="SubsonicErrorCode.Generic"/>.</summary>
    public SubsonicErrorCode ErrorCode { get; }
}
