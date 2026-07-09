using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace BoxSubMusic.SubsonicClient.Internal;

/// <summary>
/// Parsed &lt;subsonic-response&gt; envelope. Error responses still carry the
/// server's API version, which drives auth/version renegotiation.
/// </summary>
internal sealed class SubsonicResponseEnvelope
{
    public SubsonicResponseEnvelope(
        XElement root,
        bool isOk,
        string? apiVersion,
        string? serverType,
        string? serverVersion,
        bool isOpenSubsonic,
        int? errorCode,
        string? errorMessage)
    {
        Root = root;
        IsOk = isOk;
        ApiVersion = apiVersion;
        ServerType = serverType;
        ServerVersion = serverVersion;
        IsOpenSubsonic = isOpenSubsonic;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public XElement Root { get; }
    public bool IsOk { get; }
    public string? ApiVersion { get; }
    public string? ServerType { get; }
    public string? ServerVersion { get; }
    public bool IsOpenSubsonic { get; }
    public int? ErrorCode { get; }
    public string? ErrorMessage { get; }
}

/// <summary>
/// XDocument-based parsing helpers (decision 2026-07-09: explicit mapping, no
/// reflection serializers). Elements are matched by local name because servers
/// normally use the http://subsonic.org/restapi namespace but some legacy
/// implementations omit it; unprefixed attributes carry no namespace.
/// </summary>
internal static class SubsonicXml
{
    public static SubsonicResponseEnvelope Parse(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException ex)
        {
            throw new SubsonicConnectionException(
                "The server response was not valid XML. Is the URL a Subsonic-compatible server?", ex);
        }

        var root = document.Root;
        if (root == null || root.Name.LocalName != "subsonic-response")
        {
            throw new SubsonicConnectionException(
                "The server did not answer with a Subsonic API response (missing <subsonic-response>).");
        }

        bool isOk = string.Equals(Attr(root, "status"), "ok", StringComparison.OrdinalIgnoreCase);
        int? errorCode = null;
        string? errorMessage = null;
        if (!isOk)
        {
            var error = Element(root, "error");
            errorCode = AttrInt(error, "code");
            errorMessage = Attr(error, "message");
        }

        return new SubsonicResponseEnvelope(
            root,
            isOk,
            apiVersion: Attr(root, "version"),
            serverType: Attr(root, "type"),
            serverVersion: Attr(root, "serverVersion"),
            isOpenSubsonic: string.Equals(Attr(root, "openSubsonic"), "true", StringComparison.OrdinalIgnoreCase),
            errorCode: errorCode,
            errorMessage: errorMessage);
    }

    public static XElement? Element(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    public static IEnumerable<XElement> Elements(XElement parent, string localName) =>
        parent.Elements().Where(e => e.Name.LocalName == localName);

    public static string? Attr(XElement? element, string name) =>
        element?.Attribute(name)?.Value;

    public static int? AttrInt(XElement? element, string name)
    {
        var value = Attr(element, name);
        return value != null &&
               int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : (int?)null;
    }

    public static DateTimeOffset? AttrDate(XElement? element, string name)
    {
        var value = Attr(element, name);
        return value != null &&
               DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : (DateTimeOffset?)null;
    }
}
