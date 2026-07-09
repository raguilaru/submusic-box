using System.Xml;
using BoxSubMusic.Core.Setup;

namespace BoxSubMusic.Core.Tests;

/// <summary>
/// Requirements §4 (recent-servers list) and §16 (reflection-free XML
/// persistence): list maintenance rules and the XML round trip.
/// </summary>
public class RecentServersTests
{
    private static RecentServer Server(string url, string username, int daysAgo) =>
        new(url, username, new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero).AddDays(-daysAgo));

    [Fact]
    public void WithEntryPutsTheNewestFirst()
    {
        var existing = new[] { Server("https://a.example.com", "rafa", 1) };

        var updated = RecentServers.WithEntry(existing, Server("https://b.example.com", "rafa", 0));

        Assert.Equal(2, updated.Count);
        Assert.Equal("https://b.example.com", updated[0].Url);
        Assert.Equal("https://a.example.com", updated[1].Url);
    }

    [Fact]
    public void WithEntryReplacesTheSameServerAndUser()
    {
        var existing = new[]
        {
            Server("https://a.example.com", "rafa", 2),
            Server("https://b.example.com", "rafa", 1),
        };

        var updated = RecentServers.WithEntry(existing, Server("HTTPS://A.EXAMPLE.COM", "rafa", 0));

        Assert.Equal(2, updated.Count);
        Assert.Equal("HTTPS://A.EXAMPLE.COM", updated[0].Url);
        Assert.Equal("https://b.example.com", updated[1].Url);
    }

    [Fact]
    public void WithEntryKeepsDistinctUsersOnTheSameServer()
    {
        var existing = new[] { Server("https://a.example.com", "rafa", 1) };

        var updated = RecentServers.WithEntry(existing, Server("https://a.example.com", "test", 0));

        Assert.Equal(2, updated.Count);
    }

    [Fact]
    public void WithEntryCapsTheListAtMaxEntries()
    {
        var existing = new List<RecentServer>();
        for (int i = 0; i < RecentServers.MaxEntries; i++)
        {
            existing.Add(Server($"https://server{i}.example.com", "rafa", i + 1));
        }

        var updated = RecentServers.WithEntry(existing, Server("https://new.example.com", "rafa", 0));

        Assert.Equal(RecentServers.MaxEntries, updated.Count);
        Assert.Equal("https://new.example.com", updated[0].Url);
        Assert.DoesNotContain(updated, s => s.Url == $"https://server{RecentServers.MaxEntries - 1}.example.com");
    }

    [Fact]
    public void SerializeThenParseRoundTrips()
    {
        var servers = new[]
        {
            Server("https://a.example.com", "rafa", 0),
            Server("http://10.0.0.5:4533", "test", 1),
        };

        var parsed = RecentServers.Parse(RecentServers.Serialize(servers));

        Assert.Equal(2, parsed.Count);
        Assert.Equal("https://a.example.com", parsed[0].Url);
        Assert.Equal("rafa", parsed[0].Username);
        Assert.Equal(servers[0].LastUsed, parsed[0].LastUsed);
        Assert.Equal("http://10.0.0.5:4533", parsed[1].Url);
    }

    [Fact]
    public void ParseOrdersByLastUsedDescending()
    {
        var serialized = RecentServers.Serialize(new[]
        {
            Server("https://old.example.com", "rafa", 5),
            Server("https://new.example.com", "rafa", 0),
        });

        var parsed = RecentServers.Parse(serialized);

        Assert.Equal("https://new.example.com", parsed[0].Url);
    }

    [Fact]
    public void ParseSkipsEntriesMissingUrlOrUsername()
    {
        const string xml = """
            <recentServers version="1">
              <server url="https://a.example.com" username="rafa" lastUsed="2026-07-09T12:00:00Z" />
              <server username="ghost" lastUsed="2026-07-09T12:00:00Z" />
              <server url="https://b.example.com" lastUsed="2026-07-09T12:00:00Z" />
            </recentServers>
            """;

        var parsed = RecentServers.Parse(xml);

        Assert.Single(parsed);
        Assert.Equal("https://a.example.com", parsed[0].Url);
    }

    [Fact]
    public void ParseToleratesAMissingLastUsedDate()
    {
        const string xml = """
            <recentServers version="1">
              <server url="https://a.example.com" username="rafa" />
            </recentServers>
            """;

        var parsed = RecentServers.Parse(xml);

        Assert.Single(parsed);
        Assert.Equal(DateTimeOffset.MinValue, parsed[0].LastUsed);
    }

    [Fact]
    public void ParseThrowsOnInvalidXmlSoTheStoreCanResetTheCache()
    {
        Assert.Throws<XmlException>(() => RecentServers.Parse("not xml at all"));
    }
}
