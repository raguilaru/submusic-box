using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoxSubMusic.Core.Concurrency;
using BoxSubMusic.Core.Playback;
using BoxSubMusic.Core.Playlists;
using BoxSubMusic.SubsonicClient;
using BoxSubMusic.SubsonicClient.Models;

namespace BoxSubMusic.App.Services
{
    /// <summary>
    /// Container queue edits (2026-07-18): "Play next" / "Add to queue" for a
    /// whole album, playlist, or artist from a card context menu. The songs
    /// are fetched on demand (cards carry only ids), then handed to
    /// <see cref="PlaybackService"/> as one block. Failure mode matches the
    /// context-menu favorite toggle: the menu is closed by the time the fetch
    /// fails, so nothing changes and nothing shown ever lies (§15 spirit —
    /// the queue simply does not gain songs that could not be loaded).
    /// </summary>
    public static class QueueActions
    {
        public static async Task AddAlbumAsync(string albumId, bool insertNext)
        {
            var client = AppSession.Client;
            if (client == null || string.IsNullOrEmpty(albumId))
            {
                return;
            }

            try
            {
                AlbumDetail detail = await client.GetAlbumAsync(albumId, CancellationToken.None);
                Dispatch(detail.Songs, insertNext,
                    new QueueContext(detail.Album.Name, detail.Album.CoverArtId));
            }
            catch (SubsonicClientException)
            {
                // Nothing changed — but say so (Slice 14, §15): the menu
                // closed long before the fetch failed.
                Toast.Show("Couldn't load the album — nothing was added to the queue.");
            }
        }

        public static async Task AddPlaylistAsync(string playlistId, bool insertNext)
        {
            var client = AppSession.Client;
            if (client == null || string.IsNullOrEmpty(playlistId))
            {
                return;
            }

            try
            {
                PlaylistDetail detail = await client.GetPlaylistAsync(playlistId, CancellationToken.None);
                // Same artwork rule as playlist Play: the first constituent
                // album cover (single-image cut of the §7.4 composition).
                Dispatch(detail.Songs, insertNext, new QueueContext(
                    detail.Playlist.Name,
                    PlaylistArtwork.SelectCoverArtIds(detail.Songs).FirstOrDefault()));
            }
            catch (SubsonicClientException)
            {
                Toast.Show("Couldn't load the playlist — nothing was added to the queue.");
            }
        }

        public static async Task AddArtistAsync(string artistId, bool insertNext)
        {
            var client = AppSession.Client;
            if (client == null || string.IsNullOrEmpty(artistId))
            {
                return;
            }

            try
            {
                ArtistDetail detail = await client.GetArtistAsync(artistId, CancellationToken.None);
                List<Song> pool = await LoadArtistPoolAsync(client, detail, CancellationToken.None);
                Dispatch(pool, insertNext,
                    new QueueContext(detail.Artist.Name, detail.Artist.CoverArtId));
            }
            catch (SubsonicClientException)
            {
                Toast.Show("Couldn't load the artist's songs — nothing was added to the queue.");
            }
        }

        /// <summary>
        /// One request per album/playlist tile or artist-pool album at a
        /// time of this many (Slice 14 performance audit): parallel enough
        /// to hide latency, bounded so a 100-album artist no longer fires
        /// 100 simultaneous requests at a small self-hosted server.
        /// </summary>
        public const int MaxParallelFetches = 4;

        /// <summary>
        /// The §8 artist song pool: every song oldest-album-first (year
        /// ascending, unknown years last, then name). getArtist carries
        /// albums without songs, so this fans out one getAlbum per album —
        /// bounded-parallel since Slice 14 — shared by artist Play/Shuffle
        /// (ArtistDetailPage) and the artist queue edits.
        /// </summary>
        public static async Task<List<Song>> LoadArtistPoolAsync(
            SubsonicApiClient client, ArtistDetail detail, CancellationToken cancellationToken)
        {
            var albums = detail.Albums
                .OrderBy(a => a.Year ?? int.MaxValue)
                .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            AlbumDetail[] loaded = await BoundedParallel.MapAsync(
                albums,
                a => client.GetAlbumAsync(a.Id, cancellationToken),
                MaxParallelFetches);

            var songs = new List<Song>();
            foreach (AlbumDetail album in loaded)
            {
                songs.AddRange(album.Songs);
            }

            return songs;
        }

        private static void Dispatch(IReadOnlyList<Song> songs, bool insertNext, QueueContext context)
        {
            if (songs.Count == 0)
            {
                return;
            }

            if (insertNext)
            {
                PlaybackService.Instance.PlayNext(songs, context);
            }
            else
            {
                PlaybackService.Instance.AddToQueue(songs, context);
            }
        }
    }
}
