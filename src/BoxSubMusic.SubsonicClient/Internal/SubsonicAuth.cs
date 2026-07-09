using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BoxSubMusic.SubsonicClient.Internal;

internal static class SubsonicAuth
{
    public static string GenerateSalt()
    {
        var bytes = new byte[8];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        return ToLowerHex(bytes);
    }

    /// <summary>
    /// Subsonic token auth: t = md5(password + salt), lowercase hex. MD5 is
    /// mandated by the API contract; it is not used as a security primitive here.
    /// </summary>
    public static string ComputeToken(string password, string salt)
    {
        using (var md5 = MD5.Create())
        {
            return ToLowerHex(md5.ComputeHash(Encoding.UTF8.GetBytes(password + salt)));
        }
    }

    /// <summary>Legacy pre-1.13 auth: hex-encoded password ("enc:" prefix per the API).</summary>
    public static string HexEncodePassword(string password) =>
        "enc:" + ToLowerHex(Encoding.UTF8.GetBytes(password));

    private static string ToLowerHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }
}
