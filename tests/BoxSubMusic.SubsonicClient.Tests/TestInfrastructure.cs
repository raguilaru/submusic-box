using System.Net;
using System.Text;

namespace BoxSubMusic.SubsonicClient.Tests;

/// <summary>Routes requests to canned responses and records every request URI.</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<Uri, HttpResponseMessage> _responder;

    public FakeHttpMessageHandler(Func<Uri, HttpResponseMessage> responder) => _responder = responder;

    public List<Uri> Requests { get; } = new();

    public static HttpResponseMessage Xml(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/xml") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(_responder(request.RequestUri!));
    }
}

public static class TestHelpers
{
    public const string DefaultPassword = "secret";

    public static SubsonicApiClient CreateClient(
        FakeHttpMessageHandler handler,
        string baseUrl = "http://music.example:4533",
        string username = "alice",
        string password = DefaultPassword) =>
        new(new SubsonicServerCredentials(new Uri(baseUrl), username, password), new HttpClient(handler));

    /// <summary>Endpoint path of a request, e.g. "/rest/ping".</summary>
    public static string EndpointOf(Uri uri) => uri.AbsolutePath;

    public static Dictionary<string, string> QueryOf(Uri uri)
    {
        var result = new Dictionary<string, string>();
        foreach (var pair in uri.Query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            result[Uri.UnescapeDataString(pair.Substring(0, separator))] =
                Uri.UnescapeDataString(pair.Substring(separator + 1));
        }

        return result;
    }

    public static string Md5Hex(string input)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        return string.Concat(md5.ComputeHash(Encoding.UTF8.GetBytes(input)).Select(b => b.ToString("x2")));
    }

    public static string HexOf(string input) =>
        string.Concat(Encoding.UTF8.GetBytes(input).Select(b => b.ToString("x2")));
}

/// <summary>Canned server responses mirroring real Navidrome / legacy Subsonic XML.</summary>
public static class Canned
{
    private const string NsAttr = " xmlns=\"http://subsonic.org/restapi\"";

    public static string PingOkOpenSubsonic(string version = "1.16.1") =>
        $"<subsonic-response{NsAttr} status=\"ok\" version=\"{version}\" type=\"navidrome\" serverVersion=\"0.62.0 (1b46b977)\" openSubsonic=\"true\"></subsonic-response>";

    public static string PingOkPlain(string version = "1.16.1") =>
        $"<subsonic-response{NsAttr} status=\"ok\" version=\"{version}\"></subsonic-response>";

    public static string Error(int code, string message, string version = "1.16.1") =>
        $"<subsonic-response{NsAttr} status=\"failed\" version=\"{version}\"><error code=\"{code}\" message=\"{message}\"/></subsonic-response>";

    public static string Extensions() =>
        $"<subsonic-response{NsAttr} status=\"ok\" version=\"1.16.1\" type=\"navidrome\" serverVersion=\"0.62.0 (1b46b977)\" openSubsonic=\"true\">" +
        "<openSubsonicExtensions name=\"indexBasedQueue\"><versions>1</versions></openSubsonicExtensions>" +
        "<openSubsonicExtensions name=\"songLyrics\"><versions>1</versions><versions>2</versions></openSubsonicExtensions>" +
        "</subsonic-response>";

    public static string AlbumList2(string albums) =>
        $"<subsonic-response{NsAttr} status=\"ok\" version=\"1.16.1\" type=\"navidrome\" serverVersion=\"0.62.0\" openSubsonic=\"true\"><albumList2>{albums}</albumList2></subsonic-response>";

    /// <summary>Empty ok envelope, as star/unstar return.</summary>
    public static string Ok() =>
        $"<subsonic-response{NsAttr} status=\"ok\" version=\"1.16.1\" type=\"navidrome\" serverVersion=\"0.62.0\" openSubsonic=\"true\"></subsonic-response>";

    public static string Artists(string indexes) =>
        $"<subsonic-response{NsAttr} status=\"ok\" version=\"1.16.1\" type=\"navidrome\" serverVersion=\"0.62.0\" openSubsonic=\"true\">" +
        $"<artists ignoredArticles=\"The El La\">{indexes}</artists></subsonic-response>";

    /// <summary>Two alphabetical index buckets holding three artists total; the
    /// middle one is starred and one has no cover art.</summary>
    public const string TwoIndexes =
        "<index name=\"A\">" +
        "<artist id=\"ar-1\" name=\"ABBA\" coverArt=\"ar-1\" albumCount=\"3\"/>" +
        "<artist id=\"ar-2\" name=\"Air\" coverArt=\"ar-2\" albumCount=\"2\" starred=\"2024-05-06T07:08:09Z\"/>" +
        "</index>" +
        "<index name=\"B\">" +
        "<artist id=\"ar-3\" name=\"Beck\" albumCount=\"5\"/>" +
        "</index>";

    public static string Artist(string artistAttrs, string albums) =>
        $"<subsonic-response{NsAttr} status=\"ok\" version=\"1.16.1\" type=\"navidrome\" serverVersion=\"0.62.0\" openSubsonic=\"true\">" +
        $"<artist {artistAttrs}>{albums}</artist></subsonic-response>";

    public const string TwoAlbums =
        "<album id=\"al-1\" name=\"Abbey Road\" artist=\"The Beatles\" artistId=\"ar-1\" coverArt=\"al-1\" " +
        "songCount=\"17\" duration=\"2830\" created=\"2024-01-02T10:20:30Z\" year=\"1969\" genre=\"Rock\"/>" +
        "<album id=\"al-2\" name=\"Sparse\" songCount=\"0\" duration=\"0\"/>";
}
