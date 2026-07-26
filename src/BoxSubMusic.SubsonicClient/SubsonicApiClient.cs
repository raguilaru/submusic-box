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
    public const string ClientName = "SubMusicBox";

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
    /// getArtists — the full artist index (requirements §7.1). The server
    /// groups artists under alphabetical &lt;index&gt; buckets; the browse grid
    /// wants a flat list, so the buckets are flattened in server order.
    /// </summary>
    public async Task<IReadOnlyList<Artist>> GetArtistsAsync(CancellationToken cancellationToken = default)
    {
        var root = await ExecuteAsync("getArtists", null, cancellationToken).ConfigureAwait(false);
        var artists = SubsonicXml.Element(root, "artists")
            ?? throw new SubsonicConnectionException("The server response is missing the <artists> element.");
        return SubsonicXml.Elements(artists, "index")
            .SelectMany(index => SubsonicXml.Elements(index, "artist"))
            .Select(MapArtist)
            .ToList();
    }

    /// <summary>
    /// getArtist — one artist plus its albums (requirements §8). The album
    /// count is authoritative from the artist element; the song count is
    /// summed from the returned albums (ArtistID3 has no reliable total).
    /// </summary>
    public async Task<ArtistDetail> GetArtistAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("An artist id is required.", nameof(id));

        var parameters = new List<KeyValuePair<string, string>> { Param("id", id) };
        var root = await ExecuteAsync("getArtist", parameters, cancellationToken).ConfigureAwait(false);
        var element = SubsonicXml.Element(root, "artist")
            ?? throw new SubsonicConnectionException("The server response is missing the <artist> element.");

        var albums = SubsonicXml.Elements(element, "album").Select(MapAlbum).ToList();
        var songCount = albums.Sum(a => a.SongCount);
        return new ArtistDetail(MapArtist(element), albums, songCount);
    }

    /// <summary>
    /// getAlbum — one album plus its songs (requirements §9). Songs are
    /// ordered by disc number then track number so the track list shows the
    /// original disc/track order regardless of server ordering quirks.
    /// </summary>
    public async Task<AlbumDetail> GetAlbumAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("An album id is required.", nameof(id));

        var parameters = new List<KeyValuePair<string, string>> { Param("id", id) };
        var root = await ExecuteAsync("getAlbum", parameters, cancellationToken).ConfigureAwait(false);
        var element = SubsonicXml.Element(root, "album")
            ?? throw new SubsonicConnectionException("The server response is missing the <album> element.");

        var songs = SubsonicXml.Elements(element, "song")
            .Select(MapSong)
            .OrderBy(s => s.DiscNumber ?? 1)
            .ThenBy(s => s.Track ?? int.MaxValue)
            .ToList();
        return new AlbumDetail(MapAlbum(element), songs);
    }

    /// <summary>
    /// search3 with an empty query — the full song library, paged
    /// (requirements §7.3). OpenSubsonic requires servers to return all data
    /// for an empty query (verified against Navidrome 2026-07-13); legacy
    /// Subsonic servers make no such promise, so callers should gate this on
    /// <see cref="ServerCapabilities.IsOpenSubsonic"/> and degrade explicitly
    /// (§3). Song order is server-defined.
    /// </summary>
    public async Task<IReadOnlyList<Song>> GetSongsAsync(
        int size = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (size < 1 || size > 500)
            throw new ArgumentOutOfRangeException(nameof(size), size, "Size must be 1..500.");
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must not be negative.");

        var parameters = new List<KeyValuePair<string, string>>
        {
            Param("query", string.Empty),
            Param("songCount", size.ToString(CultureInfo.InvariantCulture)),
            Param("songOffset", offset.ToString(CultureInfo.InvariantCulture)),
            // Only songs are wanted; suppress the other result groups.
            Param("artistCount", "0"),
            Param("albumCount", "0"),
        };
        var root = await ExecuteAsync("search3", parameters, cancellationToken).ConfigureAwait(false);
        var result = SubsonicXml.Element(root, "searchResult3")
            ?? throw new SubsonicConnectionException("The server response is missing the <searchResult3> element.");
        return SubsonicXml.Elements(result, "song").Select(MapSong).ToList();
    }

    /// <summary>
    /// search3 with a real query — the Search view's data source (Slice 13,
    /// §17 resolution). One request can fill any combination of the three
    /// scopes; a scope with count 0 is suppressed server-side and comes back
    /// as an empty list. search3 exists since API 1.8, so callers gate on
    /// <see cref="ServerCapabilities.ApiVersion"/> and degrade explicitly on
    /// older servers (§3). Result order within each scope is server-defined.
    /// </summary>
    public async Task<SearchResult> SearchAsync(
        string query,
        int artistCount,
        int albumCount,
        int songCount,
        int artistOffset = 0,
        int albumOffset = 0,
        int songOffset = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException(
                "A search query is required; the empty-query full-library listing is GetSongsAsync.",
                nameof(query));
        ValidateSearchScope(artistCount, artistOffset, nameof(artistCount), nameof(artistOffset));
        ValidateSearchScope(albumCount, albumOffset, nameof(albumCount), nameof(albumOffset));
        ValidateSearchScope(songCount, songOffset, nameof(songCount), nameof(songOffset));

        var parameters = new List<KeyValuePair<string, string>>
        {
            Param("query", query),
            Param("artistCount", artistCount.ToString(CultureInfo.InvariantCulture)),
            Param("artistOffset", artistOffset.ToString(CultureInfo.InvariantCulture)),
            Param("albumCount", albumCount.ToString(CultureInfo.InvariantCulture)),
            Param("albumOffset", albumOffset.ToString(CultureInfo.InvariantCulture)),
            Param("songCount", songCount.ToString(CultureInfo.InvariantCulture)),
            Param("songOffset", songOffset.ToString(CultureInfo.InvariantCulture)),
        };
        var root = await ExecuteAsync("search3", parameters, cancellationToken).ConfigureAwait(false);
        var result = SubsonicXml.Element(root, "searchResult3")
            ?? throw new SubsonicConnectionException("The server response is missing the <searchResult3> element.");
        return new SearchResult(
            artists: SubsonicXml.Elements(result, "artist").Select(MapArtist).ToList(),
            albums: SubsonicXml.Elements(result, "album").Select(MapAlbum).ToList(),
            songs: SubsonicXml.Elements(result, "song").Select(MapSong).ToList());
    }

    private static void ValidateSearchScope(int count, int offset, string countName, string offsetName)
    {
        if (count < 0 || count > 500)
            throw new ArgumentOutOfRangeException(countName, count, "Count must be 0..500 (Subsonic API limit).");
        if (offset < 0)
            throw new ArgumentOutOfRangeException(offsetName, offset, "Offset must not be negative.");
    }

    /// <summary>
    /// getStarred2 — everything the signed-in user has starred, as ID3 entities
    /// (the Favorites view's data source). The ID3 variant is deliberate: it
    /// returns the same artist/album shapes as getArtists/getAlbumList2, so the
    /// favorites cards behave exactly like their browse counterparts. There are
    /// no count/offset parameters — the server returns the complete set in one
    /// response, so the caller can sort over all of it. getStarred2 exists
    /// since API 1.8, so callers gate on
    /// <see cref="ServerCapabilities.ApiVersion"/> and degrade explicitly on
    /// older servers (§3). Order within each scope is server-defined.
    /// </summary>
    public async Task<StarredResult> GetStarred2Async(CancellationToken cancellationToken = default)
    {
        var root = await ExecuteAsync("getStarred2", null, cancellationToken).ConfigureAwait(false);
        var starred = SubsonicXml.Element(root, "starred2")
            ?? throw new SubsonicConnectionException("The server response is missing the <starred2> element.");
        return new StarredResult(
            artists: SubsonicXml.Elements(starred, "artist").Select(MapArtist).ToList(),
            albums: SubsonicXml.Elements(starred, "album").Select(MapAlbum).ToList(),
            songs: SubsonicXml.Elements(starred, "song").Select(MapSong).ToList());
    }

    /// <summary>
    /// getPlaylists — the user's playlists (requirements §7.4). Returns
    /// summaries only; the track list (needed to compose tile artwork) comes
    /// from <see cref="GetPlaylistAsync"/> per playlist.
    /// </summary>
    public async Task<IReadOnlyList<Playlist>> GetPlaylistsAsync(CancellationToken cancellationToken = default)
    {
        var root = await ExecuteAsync("getPlaylists", null, cancellationToken).ConfigureAwait(false);
        var playlists = SubsonicXml.Element(root, "playlists")
            ?? throw new SubsonicConnectionException("The server response is missing the <playlists> element.");
        return SubsonicXml.Elements(playlists, "playlist").Select(MapPlaylist).ToList();
    }

    /// <summary>
    /// getPlaylist — one playlist plus its songs (requirements §7.4). Songs are
    /// returned as &lt;entry&gt; children in stored order; the client composes
    /// tile artwork from their album covers.
    /// </summary>
    public async Task<PlaylistDetail> GetPlaylistAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A playlist id is required.", nameof(id));

        var parameters = new List<KeyValuePair<string, string>> { Param("id", id) };
        var root = await ExecuteAsync("getPlaylist", parameters, cancellationToken).ConfigureAwait(false);
        var element = SubsonicXml.Element(root, "playlist")
            ?? throw new SubsonicConnectionException("The server response is missing the <playlist> element.");

        var songs = SubsonicXml.Elements(element, "entry").Select(MapSong).ToList();
        return new PlaylistDetail(MapPlaylist(element), songs);
    }

    /// <summary>
    /// star — mark an item (artist/album/song id) as a favorite (§8). The
    /// server echoes an empty ok envelope; a failure surfaces as an exception.
    /// </summary>
    public Task StarAsync(string id, CancellationToken cancellationToken = default) =>
        SetStarredAsync("star", id, cancellationToken);

    /// <summary>unstar — remove an item's favorite mark (§8).</summary>
    public Task UnstarAsync(string id, CancellationToken cancellationToken = default) =>
        SetStarredAsync("unstar", id, cancellationToken);

    private async Task SetStarredAsync(string endpoint, string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("An id is required.", nameof(id));

        var parameters = new List<KeyValuePair<string, string>> { Param("id", id) };
        await ExecuteAsync(endpoint, parameters, cancellationToken).ConfigureAwait(false);
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
    /// Builds an authenticated stream URL for a song, usable directly as a
    /// MediaPlayer source (§10/§11). With no overrides (the default) the
    /// server decides the delivered stream — including enforcing any
    /// server-side bitrate restriction, which is the authoritative quality
    /// control (§16 decision 2026-07-19: no client-side quality setting).
    /// <paramref name="format"/> (core API 1.6.0) pins the transcode format
    /// — the app uses it only as the decode-failure fallback (format=mp3),
    /// never by default; the server still applies its own bitrate cap to
    /// that transcode. <paramref name="maxBitRate"/> (core API 1.2.0) is
    /// the protocol's client-side cap, exposed for completeness; the app
    /// does not send it. In token mode each URL embeds a fresh salt+token
    /// pair.
    /// </summary>
    public Uri GetStreamUri(string songId, int? maxBitRate = null, string? format = null)
    {
        if (string.IsNullOrWhiteSpace(songId))
            throw new ArgumentException("A song id is required.", nameof(songId));
        if (maxBitRate.HasValue && maxBitRate.Value < 1)
            throw new ArgumentOutOfRangeException(nameof(maxBitRate), maxBitRate, "Max bitrate must be positive.");

        var parameters = new List<KeyValuePair<string, string>> { Param("id", songId) };
        if (maxBitRate.HasValue)
            parameters.Add(Param("maxBitRate", maxBitRate.Value.ToString(CultureInfo.InvariantCulture)));
        if (!string.IsNullOrWhiteSpace(format))
            parameters.Add(Param("format", format!));
        return BuildRequestUri("stream", parameters);
    }

    /// <summary>
    /// scrobble — reports a play to the server so play counts (and the
    /// Most Played list, §6.2) reflect listening done in this client.
    /// <paramref name="submission"/> true registers a play; false is the
    /// "now playing" notification. Core Subsonic API since 1.5.0.
    /// </summary>
    public async Task ScrobbleAsync(string id, bool submission = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A song id is required.", nameof(id));

        var parameters = new List<KeyValuePair<string, string>>
        {
            Param("id", id),
            Param("submission", submission ? "true" : "false"),
        };
        await ExecuteAsync("scrobble", parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// getLyricsBySongId — structured lyrics for a track (OpenSubsonic
    /// <c>songLyrics</c> extension). Returns every entry the server has for
    /// the song (synced and/or unsynced, one per language); the caller picks
    /// which to show. A song with no lyrics is not an error — the server
    /// returns an empty &lt;lyricsList&gt; and this returns an empty list.
    /// Gate calls on <see cref="ServerCapabilities.SupportsExtension"/>
    /// ("songLyrics"); the app hides the lyrics UI when it is absent (§3).
    /// </summary>
    public async Task<IReadOnlyList<StructuredLyrics>> GetLyricsBySongIdAsync(
        string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A song id is required.", nameof(id));

        var parameters = new List<KeyValuePair<string, string>> { Param("id", id) };
        var root = await ExecuteAsync("getLyricsBySongId", parameters, cancellationToken).ConfigureAwait(false);

        var list = SubsonicXml.Element(root, "lyricsList");
        if (list == null)
            return Array.Empty<StructuredLyrics>();

        return SubsonicXml.Elements(list, "structuredLyrics")
            .Select(MapStructuredLyrics)
            .ToList();
    }

    private static StructuredLyrics MapStructuredLyrics(XElement element)
    {
        bool synced = string.Equals(SubsonicXml.Attr(element, "synced"), "true", StringComparison.OrdinalIgnoreCase);
        var lines = SubsonicXml.Elements(element, "line")
            .Select(line => new LyricLine(SubsonicXml.AttrInt(line, "start"), line.Value))
            .ToList();

        return new StructuredLyrics(
            synced: synced,
            lang: SubsonicXml.Attr(element, "lang"),
            offsetMs: SubsonicXml.AttrInt(element, "offset") ?? 0,
            displayArtist: SubsonicXml.Attr(element, "displayArtist"),
            displayTitle: SubsonicXml.Attr(element, "displayTitle"),
            lines: lines);
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
            genre: SubsonicXml.Attr(element, "genre"),
            // A `starred` timestamp is present only on favorites (§9).
            isStarred: SubsonicXml.Attr(element, "starred") != null);
    }

    private static Song MapSong(XElement element)
    {
        var id = SubsonicXml.Attr(element, "id");
        if (string.IsNullOrEmpty(id))
        {
            throw new SubsonicConnectionException("The server returned a song entry without an id.");
        }

        return new Song(
            id: id!,
            title: SubsonicXml.Attr(element, "title") ?? string.Empty,
            artist: SubsonicXml.Attr(element, "artist"),
            artistId: SubsonicXml.Attr(element, "artistId"),
            album: SubsonicXml.Attr(element, "album"),
            albumId: SubsonicXml.Attr(element, "albumId"),
            coverArtId: SubsonicXml.Attr(element, "coverArt"),
            duration: TimeSpan.FromSeconds(SubsonicXml.AttrInt(element, "duration") ?? 0),
            track: SubsonicXml.AttrInt(element, "track"),
            discNumber: SubsonicXml.AttrInt(element, "discNumber"),
            year: SubsonicXml.AttrInt(element, "year"),
            format: SubsonicXml.Attr(element, "suffix"),
            bitRate: SubsonicXml.AttrInt(element, "bitRate"),
            contentType: SubsonicXml.Attr(element, "contentType"),
            isStarred: SubsonicXml.Attr(element, "starred") != null,
            created: SubsonicXml.AttrDate(element, "created"));
    }

    private static Playlist MapPlaylist(XElement element)
    {
        var id = SubsonicXml.Attr(element, "id");
        if (string.IsNullOrEmpty(id))
        {
            throw new SubsonicConnectionException("The server returned a playlist entry without an id.");
        }

        return new Playlist(
            id: id!,
            name: SubsonicXml.Attr(element, "name") ?? string.Empty,
            owner: SubsonicXml.Attr(element, "owner"),
            comment: SubsonicXml.Attr(element, "comment"),
            songCount: SubsonicXml.AttrInt(element, "songCount") ?? 0,
            duration: TimeSpan.FromSeconds(SubsonicXml.AttrInt(element, "duration") ?? 0),
            coverArtId: SubsonicXml.Attr(element, "coverArt"));
    }

    private static Artist MapArtist(XElement element)
    {
        var id = SubsonicXml.Attr(element, "id");
        if (string.IsNullOrEmpty(id))
        {
            throw new SubsonicConnectionException("The server returned an artist entry without an id.");
        }

        return new Artist(
            id: id!,
            name: SubsonicXml.Attr(element, "name") ?? string.Empty,
            coverArtId: SubsonicXml.Attr(element, "coverArt"),
            albumCount: SubsonicXml.AttrInt(element, "albumCount") ?? 0,
            // ArtistID3 carries a `starred` timestamp only when the item is a
            // favorite; its presence is the flag (§8).
            isStarred: SubsonicXml.Attr(element, "starred") != null);
    }

    private static string MapAlbumListType(AlbumListType type)
    {
        switch (type)
        {
            case AlbumListType.Frequent: return "frequent";
            case AlbumListType.Newest: return "newest";
            case AlbumListType.Random: return "random";
            case AlbumListType.AlphabeticalByName: return "alphabeticalByName";
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
