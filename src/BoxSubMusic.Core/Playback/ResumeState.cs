using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.Core.Playback
{
    /// <summary>
    /// The §6.1 resume contract and its XML persistence shape (decision
    /// 2026-07-09, requirements §16: structured local data is reflection-free
    /// XML via XDocument explicit mapping; design:
    /// docs/architecture/30-playback-architecture.md, Slice 05b section).
    /// Carries song ids and display metadata only — never stream URLs, which
    /// embed the auth token (§4: secrets stay out of plain storage). File I/O
    /// and the fast-moving index/position overrides are the app's
    /// responsibility.
    /// </summary>
    public sealed class ResumeState
    {
        public ResumeState(
            string serverUrl,
            string username,
            IReadOnlyList<Song> tracks,
            IReadOnlyList<int>? playOrderIndices,
            int currentIndex,
            double positionSeconds,
            bool shuffleEnabled,
            RepeatMode repeatMode,
            QueueContext? context)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                throw new ArgumentException("The server URL is required.", nameof(serverUrl));
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("The username is required.", nameof(username));
            if (tracks == null)
                throw new ArgumentNullException(nameof(tracks));
            if (tracks.Count == 0)
                throw new ArgumentException("A resume state needs at least one track.", nameof(tracks));
            if (shuffleEnabled && !IsPermutation(playOrderIndices, tracks.Count))
                throw new ArgumentException(
                    "With shuffle on, the play order must be a permutation of the track indices.",
                    nameof(playOrderIndices));
            if (currentIndex < 0 || currentIndex >= tracks.Count)
                throw new ArgumentOutOfRangeException(nameof(currentIndex));

            ServerUrl = serverUrl;
            Username = username;
            Tracks = tracks;
            PlayOrderIndices = shuffleEnabled ? playOrderIndices : null;
            CurrentIndex = currentIndex;
            PositionSeconds = positionSeconds > 0 ? positionSeconds : 0;
            ShuffleEnabled = shuffleEnabled;
            RepeatMode = repeatMode;
            Context = context;
        }

        /// <summary>The server the state belongs to; validated at load.</summary>
        public string ServerUrl { get; }

        public string Username { get; }

        /// <summary>The queue in original (unshuffled) order.</summary>
        public IReadOnlyList<Song> Tracks { get; }

        /// <summary>
        /// Play order as indices into <see cref="Tracks"/>; non-null exactly
        /// when <see cref="ShuffleEnabled"/> (without shuffle the play order
        /// IS the original order).
        /// </summary>
        public IReadOnlyList<int>? PlayOrderIndices { get; }

        /// <summary>Index of the current track within the play order.</summary>
        public int CurrentIndex { get; }

        /// <summary>In-track position, seconds.</summary>
        public double PositionSeconds { get; }

        public bool ShuffleEnabled { get; }

        public RepeatMode RepeatMode { get; }

        /// <summary>What the user pressed Play on; feeds the resume card.</summary>
        public QueueContext? Context { get; }

        public static string Serialize(ResumeState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var root = new XElement("resumeState",
                new XAttribute("version", 1),
                new XAttribute("serverUrl", state.ServerUrl),
                new XAttribute("username", state.Username),
                new XAttribute("currentIndex", state.CurrentIndex),
                new XAttribute("positionSeconds",
                    state.PositionSeconds.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("shuffle", state.ShuffleEnabled),
                new XAttribute("repeat", state.RepeatMode.ToString()));

            if (state.Context != null)
            {
                var context = new XElement("context",
                    new XAttribute("displayName", state.Context.DisplayName));
                if (state.Context.CoverArtId != null)
                    context.Add(new XAttribute("coverArtId", state.Context.CoverArtId));
                root.Add(context);
            }

            if (state.PlayOrderIndices != null)
            {
                root.Add(new XElement("playOrder",
                    string.Join(" ", state.PlayOrderIndices)));
            }

            root.Add(new XElement("songs", state.Tracks.Select(SongElement)));
            return new XDocument(root).ToString();
        }

        /// <summary>
        /// Parses a previously serialized state. All-or-nothing: any
        /// malformed song, out-of-range index, or non-permutation play order
        /// returns null — a partially restored queue would misrepresent
        /// "where you left off". Invalid XML throws
        /// (System.Xml.XmlException), like <c>RecentServers.Parse</c>; the
        /// app store treats both the same way (no resume state).
        /// </summary>
        public static ResumeState? Parse(string xml)
        {
            if (xml == null)
                throw new ArgumentNullException(nameof(xml));

            var root = XDocument.Parse(xml).Root;
            if (root == null || root.Name.LocalName != "resumeState")
                return null;

            var serverUrl = root.Attribute("serverUrl")?.Value;
            var username = root.Attribute("username")?.Value;
            if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(username))
                return null;

            var songs = new List<Song>();
            var songsElement = root.Elements().FirstOrDefault(e => e.Name.LocalName == "songs");
            if (songsElement == null)
                return null;
            foreach (var element in songsElement.Elements().Where(e => e.Name.LocalName == "song"))
            {
                var song = ParseSong(element);
                if (song == null)
                    return null;
                songs.Add(song);
            }

            if (songs.Count == 0)
                return null;

            bool shuffle = string.Equals(
                root.Attribute("shuffle")?.Value, "true", StringComparison.OrdinalIgnoreCase);

            IReadOnlyList<int>? playOrder = null;
            if (shuffle)
            {
                var orderText = root.Elements()
                    .FirstOrDefault(e => e.Name.LocalName == "playOrder")?.Value;
                playOrder = ParseIndices(orderText, songs.Count);
                if (playOrder == null)
                    return null;
            }

            if (!int.TryParse(root.Attribute("currentIndex")?.Value,
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out int currentIndex)
                || currentIndex < 0 || currentIndex >= songs.Count)
            {
                return null;
            }

            double.TryParse(root.Attribute("positionSeconds")?.Value,
                NumberStyles.Float, CultureInfo.InvariantCulture, out double positionSeconds);

            var repeatRaw = root.Attribute("repeat")?.Value;
            RepeatMode repeat =
                string.Equals(repeatRaw, "All", StringComparison.OrdinalIgnoreCase) ? RepeatMode.All
                : string.Equals(repeatRaw, "One", StringComparison.OrdinalIgnoreCase) ? RepeatMode.One
                : RepeatMode.Off;

            QueueContext? context = null;
            var contextElement = root.Elements().FirstOrDefault(e => e.Name.LocalName == "context");
            var displayName = contextElement?.Attribute("displayName")?.Value;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                context = new QueueContext(displayName!, contextElement?.Attribute("coverArtId")?.Value);
            }

            return new ResumeState(
                serverUrl!, username!, songs, playOrder,
                currentIndex, positionSeconds, shuffle, repeat, context);
        }

        private static XElement SongElement(Song song)
        {
            var element = new XElement("song",
                new XAttribute("id", song.Id),
                new XAttribute("title", song.Title),
                new XAttribute("durationSeconds",
                    song.Duration.TotalSeconds.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("starred", song.IsStarred));

            void AddOptional(string name, string? value)
            {
                if (value != null)
                    element.Add(new XAttribute(name, value));
            }

            AddOptional("artist", song.Artist);
            AddOptional("artistId", song.ArtistId);
            AddOptional("album", song.Album);
            AddOptional("albumId", song.AlbumId);
            AddOptional("coverArtId", song.CoverArtId);
            AddOptional("track", song.Track?.ToString(CultureInfo.InvariantCulture));
            AddOptional("disc", song.DiscNumber?.ToString(CultureInfo.InvariantCulture));
            AddOptional("year", song.Year?.ToString(CultureInfo.InvariantCulture));
            AddOptional("format", song.Format);
            AddOptional("bitRate", song.BitRate?.ToString(CultureInfo.InvariantCulture));
            AddOptional("contentType", song.ContentType);
            return element;
        }

        private static Song? ParseSong(XElement element)
        {
            var id = element.Attribute("id")?.Value;
            var title = element.Attribute("title")?.Value;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                return null;

            double.TryParse(element.Attribute("durationSeconds")?.Value,
                NumberStyles.Float, CultureInfo.InvariantCulture, out double durationSeconds);

            int? OptionalInt(string name) =>
                int.TryParse(element.Attribute(name)?.Value,
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                    ? value
                    : (int?)null;

            return new Song(
                id!,
                title!,
                element.Attribute("artist")?.Value,
                element.Attribute("artistId")?.Value,
                element.Attribute("album")?.Value,
                element.Attribute("albumId")?.Value,
                element.Attribute("coverArtId")?.Value,
                TimeSpan.FromSeconds(durationSeconds > 0 ? durationSeconds : 0),
                OptionalInt("track"),
                OptionalInt("disc"),
                OptionalInt("year"),
                element.Attribute("format")?.Value,
                OptionalInt("bitRate"),
                element.Attribute("contentType")?.Value,
                string.Equals(element.Attribute("starred")?.Value, "true",
                    StringComparison.OrdinalIgnoreCase));
        }

        private static IReadOnlyList<int>? ParseIndices(string? text, int count)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var parts = text!.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var indices = new List<int>(parts.Length);
            foreach (var part in parts)
            {
                if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                    return null;
                indices.Add(index);
            }

            return IsPermutation(indices, count) ? indices : null;
        }

        private static bool IsPermutation(IReadOnlyList<int>? indices, int count)
        {
            if (indices == null || indices.Count != count)
                return false;

            var seen = new bool[count];
            foreach (int index in indices)
            {
                if (index < 0 || index >= count || seen[index])
                    return false;
                seen[index] = true;
            }

            return true;
        }
    }
}
