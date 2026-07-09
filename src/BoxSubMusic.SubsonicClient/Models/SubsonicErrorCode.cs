namespace BoxSubMusic.SubsonicClient.Models;

/// <summary>Well-known error codes defined by the Subsonic API.</summary>
public enum SubsonicErrorCode
{
    Generic = 0,
    RequiredParameterMissing = 10,
    IncompatibleClientVersion = 20,
    IncompatibleServerVersion = 30,
    WrongUsernameOrPassword = 40,
    TokenAuthenticationNotSupported = 41,
    NotAuthorized = 50,
    TrialExpired = 60,
    DataNotFound = 70,
}
