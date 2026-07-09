namespace BoxSubMusic.SubsonicClient.Tests;

/// <summary>
/// Loads the live-test server settings from a gitignored liveserver.local.txt
/// (key=value lines: url, username, password), searched upward from the test
/// bin directory so it works both on the Mac and from the VM's UNC path.
/// The canonical copy lives next to this project; see CLAUDE.local.md.
/// </summary>
public sealed class LiveServerConfig
{
    private const string FileName = "liveserver.local.txt";

    private LiveServerConfig(Uri url, string username, string password)
    {
        Url = url;
        Username = username;
        Password = password;
    }

    public Uri Url { get; }
    public string Username { get; }
    public string Password { get; }

    /// <summary>Returns the config or triggers a skip when it is not present.</summary>
    public static LiveServerConfig Require()
    {
        var config = TryLoad();
        Skip.If(config == null, $"{FileName} not found — live server tests skipped.");
        return config!;
    }

    public static LiveServerConfig? TryLoad()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, FileName);
            if (File.Exists(candidate))
            {
                return Parse(File.ReadAllLines(candidate), candidate);
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static LiveServerConfig Parse(string[] lines, string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#"))
            {
                continue;
            }

            var separator = trimmed.IndexOf('=');
            if (separator > 0)
            {
                values[trimmed.Substring(0, separator).Trim()] = trimmed.Substring(separator + 1).Trim();
            }
        }

        if (!values.TryGetValue("url", out var url) ||
            !values.TryGetValue("username", out var username) ||
            !values.TryGetValue("password", out var password))
        {
            throw new InvalidOperationException(
                $"{path} is malformed: expected url=, username= and password= lines.");
        }

        return new LiveServerConfig(new Uri(url), username, password);
    }
}
