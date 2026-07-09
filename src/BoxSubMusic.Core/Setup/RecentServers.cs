using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace BoxSubMusic.Core.Setup;

/// <summary>
/// Recent-servers list logic and its XML persistence shape (decision
/// 2026-07-09, requirements §16: structured local data is reflection-free XML
/// via XDocument explicit mapping). File I/O is the app's responsibility.
/// </summary>
public static class RecentServers
{
    public const int MaxEntries = 5;

    /// <summary>
    /// Returns the list with <paramref name="newest"/> on top: an existing
    /// entry for the same server+user is replaced, and the list is capped at
    /// <see cref="MaxEntries"/>.
    /// </summary>
    public static IReadOnlyList<RecentServer> WithEntry(
        IEnumerable<RecentServer> existing,
        RecentServer newest)
    {
        if (existing == null)
            throw new ArgumentNullException(nameof(existing));
        if (newest == null)
            throw new ArgumentNullException(nameof(newest));

        var result = new List<RecentServer> { newest };
        result.AddRange(existing.Where(entry =>
            !(string.Equals(entry.Url, newest.Url, StringComparison.OrdinalIgnoreCase) &&
              string.Equals(entry.Username, newest.Username, StringComparison.Ordinal))));
        return result.Take(MaxEntries).ToList();
    }

    public static string Serialize(IEnumerable<RecentServer> servers)
    {
        if (servers == null)
            throw new ArgumentNullException(nameof(servers));

        var root = new XElement("recentServers",
            new XAttribute("version", 1),
            servers.Select(server => new XElement("server",
                new XAttribute("url", server.Url),
                new XAttribute("username", server.Username),
                new XAttribute("lastUsed", server.LastUsed.ToString("o", CultureInfo.InvariantCulture)))));
        return new XDocument(root).ToString();
    }

    /// <summary>
    /// Parses a previously serialized list, most recent first. Entries missing
    /// url or username are skipped; invalid XML throws (System.Xml.XmlException)
    /// and the caller decides — the app store treats any failure as an empty
    /// cache, since recents are a convenience, never a blocker.
    /// </summary>
    public static IReadOnlyList<RecentServer> Parse(string xml)
    {
        if (xml == null)
            throw new ArgumentNullException(nameof(xml));

        var result = new List<RecentServer>();
        var root = XDocument.Parse(xml).Root;
        if (root == null || root.Name.LocalName != "recentServers")
            return result;

        foreach (var element in root.Elements().Where(e => e.Name.LocalName == "server"))
        {
            var url = element.Attribute("url")?.Value;
            var username = element.Attribute("username")?.Value;
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(username))
                continue;

            var lastUsedRaw = element.Attribute("lastUsed")?.Value;
            if (!DateTimeOffset.TryParse(
                    lastUsedRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastUsed))
            {
                lastUsed = DateTimeOffset.MinValue;
            }

            result.Add(new RecentServer(url!, username!, lastUsed));
        }

        return result
            .OrderByDescending(server => server.LastUsed)
            .Take(MaxEntries)
            .ToList();
    }
}
