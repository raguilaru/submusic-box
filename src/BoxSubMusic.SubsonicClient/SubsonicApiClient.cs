using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using BoxSubMusic.SubsonicClient.Internal;
using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.SubsonicClient;

/// <summary>
/// Typed client for OpenSubsonic / Subsonic-compatible servers (requirements
/// §3). Transport is the API's native XML parsed with XDocument (decision
/// 2026-07-09). The HttpClient is caller-owned: the app shares one instance,
/// tests inject a fake handler.
/// </summary>
public sealed class SubsonicApiClient
{
    public const string ClientName = "BoxSubMusic";

    /// <summary>Highest Subsonic API version this client implements.</summary>
    public const string DeclaredApiVersion = "1.16.1";

    /// <summary>Token auth (t/s params) exists since API 1.13.0.</summary>
    private static readonly Version TokenAuthMinimumVersion = new Version(1, 13, 0);

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<int>> EmptyExtensions =
        new Dictionary<string, IReadOnlyList<int>>();

    private readonly SubsonicServerCredentials _credentials;
    private readonly HttpClient _http;

    // Negotiated by ConnectAsync; the defaults are correct for current servers.
    private SubsonicAuthMode _authMode = SubsonicAuthMode.Token;
    private string _apiVersion = DeclaredApiVersion;

    public SubsonicApiClient(SubsonicServerCredentials credentials, HttpClient httpClient)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>Capabilities discovered by the last successful ConnectAsync; null before connecting.</summary>
    public ServerCapabilities? Capabilities { get; private set; }

    /// <summary>The auth scheme currently in use (may change during ConnectAsync).</summary>
    public SubsonicAuthMode AuthMode => _authMode;

    /// <summary>
    /// Verifies credentials and detects server capabilities (requirements §3:
    /// capability detection on connect). Falls back explicitly to legacy
    /// password auth for pre-1.13 servers.
    /// </summary>
    public async Task<ServerCapabilities> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var envelope = await ExecuteRawAsync("ping", null, cancellationToken).ConfigureAwait(false);
        if (!envelope.IsOk)
        {
            envelope = await RenegotiateAsync(envelope, cancellationToken).ConfigureAwait(false);
        }

        var extensions = envelope.IsOpenSubsonic
            ? await GetOpenSubsonicExtensionsAsync(cancellationToken).ConfigureAwait(false)
            : EmptyExtensions;

        Capabilities = new ServerCapabilities(
            apiVersion: envelope.ApiVersion ?? _apiVersion,
            serverType: envelope.ServerType,
            serverVersion: envelope.ServerVersion,
            isOpenSubsonic: envelope.IsOpenSubsonic,
            authMode: _authMode,
            extensions: extensions);
        return Capabilities;
    }

    /// <summary>
    /// getAlbumList2 — album-based lists for the Home carousels
    /// (requirements §6.2–§6.4).
    /// </summary>
    public async Task<IReadOnlyList<Album>> GetAlbumList2Async(
        AlbumListType type,
        int size = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (size < 1 || size > 500)
            throw new ArgumentOutOfRangeException(nameof(size), size, "Size must be 1..500 (Subsonic API limit).");
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must not be negative.");

        var parameters = new List<KeyValuePair<string, string>>
        {
            Param("type", MapAlbumListType(type)),
            Param("size", size.ToString(CultureInfo.InvariantCulture)),
            Param("offset", offset.ToString(CultureInfo.InvariantCulture)),
        };
        var root = await ExecuteAsync("getAlbumList2", parameters, cancellationToken).ConfigureAwait(false);
        var list = SubsonicXml.Element(root, "albumList2")
            ?? throw new SubsonicConnectionException("The server response is missing the <albumList2> element.");
        return SubsonicXml.Elements(list, "album").Select(MapAlbum).ToList();
    }

    /// <summary>
    /// Builds an authenticated getCoverArt URL usable directly as an image
    /// source. In token mode each URL embeds a fresh salt+token pair.
    /// </summary>
    public Uri GetCoverArtUri(string coverArtId, int? size = null)
    {
        if (string.IsNullOrWhiteSpace(coverArtId))
            throw new ArgumentException("A cover art id is required.", nameof(coverArtId));

        var parameters = new List<KeyValuePair<string, string>> { Param("id", coverArtId) };
        if (size.HasValue)
        {
            if (size.Value < 1)
                throw new ArgumentOutOfRangeException(nameof(size), size, "Size must be positive.");
            parameters.Add(Param("size", size.Value.ToString(CultureInfo.InvariantCulture)));
        }

        return BuildRequestUri("getCoverArt", parameters);
    }

    /// <summary>
    /// One explicit renegotiation attempt, driven by the API version the
    /// server reports in its error response:
    /// - pre-1.13 servers do not understand the t/s token params → retry with
    ///   the legacy hex-encoded password and the server's API version;
    /// - error 30 (client declared a newer version than the server supports)
    ///   on a token-capable server → retry with the server's version.
    /// Anything else surfaces as <see cref="SubsonicApiException"/>.
    /// </summary>
    private async Task<SubsonicResponseEnvelope> RenegotiateAsync(
        SubsonicResponseEnvelope failed,
        CancellationToken cancellationToken)
    {
        var serverApi = ParseApiVersion(failed.ApiVersion);
        int code = failed.ErrorCode ?? -1;
        bool renegotiable = code == 10 || code == 30 || code == 40 || code == 41;
        if (serverApi == null || !renegotiable)
        {
            throw MapApiError(failed);
        }

        if (serverApi < TokenAuthMinimumVersion)
        {
            _authMode = SubsonicAuthMode.LegacyPassword;
        }
        else if (code != 30)
        {
            // Token-capable server rejected the credentials: a real auth failure.
            throw MapApiError(failed);
        }

        _apiVersion = failed.ApiVersion!;
        var retried = await ExecuteRawAsync("ping", null, cancellationToken).ConfigureAwait(false);
        if (!retried.IsOk)
        {
            throw MapApiError(retried);
        }

        return retried;
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<int>>> GetOpenSubsonicExtensionsAsync(
        CancellationToken cancellationToken)
    {
        var root = await ExecuteAsync("getOpenSubsonicExtensions", null, cancellationToken).ConfigureAwait(false);
        var result = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
        foreach (var extension in SubsonicXml.Elements(root, "openSubsonicExtensions"))
        {
            var name = SubsonicXml.Attr(extension, "name");
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            result[name!] = SubsonicXml.Elements(extension, "versions")
                .Select(v => int.TryParse(v.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                    ? n
                    : (int?)null)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToList();
        }

        return result;
    }

    private async Task<XElement> ExecuteAsync(
        string endpoint,
        IReadOnlyList<KeyValuePair<string, string>>? parameters,
        CancellationToken cancellationToken)
    {
        var envelope = await ExecuteRawAsync(endpoint, parameters, cancellationToken).ConfigureAwait(false);
        if (!envelope.IsOk)
        {
            throw MapApiError(envelope);
        }

        return envelope.Root;
    }

    private async Task<SubsonicResponseEnvelope> ExecuteRawAsync(
        string endpoint,
        IReadOnlyList<KeyValuePair<string, string>>? parameters,
        CancellationToken cancellationToken)
    {
        var uri = BuildRequestUri(endpoint, parameters);
        string body;
        try
        {
            using (var response = await _http.GetAsync(uri, cancellationToken).ConfigureAwait(false))
            {
                // The Subsonic API reports its own errors inside HTTP 200;
                // a non-success status means transport/proxy trouble.
                if (!response.IsSuccessStatusCode)
                {
                    throw new SubsonicConnectionException(
                        $"The server returned HTTP {(int)response.StatusCode} for {endpoint}.");
                }

                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new SubsonicConnectionException("Could not reach the server: " + ex.Message, ex);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SubsonicConnectionException("The request to the server timed out.");
        }

        return SubsonicXml.Parse(body);
    }

    private Uri BuildRequestUri(string endpoint, IReadOnlyList<KeyValuePair<string, string>>? parameters)
    {
        var query = new List<KeyValuePair<string, string>> { Param("u", _credentials.Username) };
        if (_authMode == SubsonicAuthMode.Token)
        {
            var salt = SubsonicAuth.GenerateSalt();
            query.Add(Param("t", SubsonicAuth.ComputeToken(_credentials.Password, salt)));
            query.Add(Param("s", salt));
        }
        else
        {
            query.Add(Param("p", SubsonicAuth.HexEncodePassword(_credentials.Password)));
        }

        query.Add(Param("v", _apiVersion));
        query.Add(Param("c", ClientName));
        if (parameters != null)
        {
            query.AddRange(parameters);
        }

        var sb = new StringBuilder();
        sb.Append(_credentials.BaseUrl.AbsoluteUri.TrimEnd('/'));
        sb.Append("/rest/").Append(endpoint);
        for (int i = 0; i < query.Count; i++)
        {
            sb.Append(i == 0 ? '?' : '&');
            sb.Append(Uri.EscapeDataString(query[i].Key))
              .Append('=')
              .Append(Uri.EscapeDataString(query[i].Value));
        }

        return new Uri(sb.ToString());
    }

    private static Album MapAlbum(XElement element)
    {
        var id = SubsonicXml.Attr(element, "id");
        if (string.IsNullOrEmpty(id))
        {
            throw new SubsonicConnectionException("The server returned an album entry without an id.");
        }

        return new Album(
            id: id!,
            name: SubsonicXml.Attr(element, "name") ?? string.Empty,
            artist: SubsonicXml.Attr(element, "artist"),
            artistId: SubsonicXml.Attr(element, "artistId"),
            coverArtId: SubsonicXml.Attr(element, "coverArt"),
            songCount: SubsonicXml.AttrInt(element, "songCount") ?? 0,
            duration: TimeSpan.FromSeconds(SubsonicXml.AttrInt(element, "duration") ?? 0),
            created: SubsonicXml.AttrDate(element, "created"),
            year: SubsonicXml.AttrInt(element, "year"),
            genre: SubsonicXml.Attr(element, "genre"));
    }

    private static string MapAlbumListType(AlbumListType type)
    {
        switch (type)
        {
            case AlbumListType.Frequent: return "frequent";
            case AlbumListType.Newest: return "newest";
            case AlbumListType.Random: return "random";
            default: throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown album list type.");
        }
    }

    private static SubsonicApiException MapApiError(SubsonicResponseEnvelope envelope) =>
        new SubsonicApiException(
            envelope.ErrorCode ?? 0,
            envelope.ErrorMessage ?? "The server reported an unspecified error.");

    private static KeyValuePair<string, string> Param(string key, string value) =>
        new KeyValuePair<string, string>(key, value);

    private static Version? ParseApiVersion(string? version) =>
        version != null && Version.TryParse(version, out var parsed) ? parsed : null;
}
