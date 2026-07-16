using System;
using System.Threading.Tasks;
using BoxSubMusic.Core.Playback;
using Windows.Storage;

namespace BoxSubMusic.App.Services
{
    /// <summary>
    /// Resume-state persistence (§6.1; design:
    /// docs/architecture/30-playback-architecture.md, Slice 05b section).
    /// The queue STRUCTURE (tracks, play order, shuffle/repeat, context) is a
    /// LocalFolder XML file rewritten only on structural changes; the
    /// fast-moving current index + in-track position live in LocalSettings,
    /// written cheaply from playback events (including the media thread —
    /// ApplicationData is agile) and overriding the file's snapshot at load.
    /// Corrupt or mismatched state reads as absent, like RecentServersStore.
    /// </summary>
    internal static class ResumeStore
    {
        private const string FileName = "resume-state.xml";
        private const string IndexKey = "resume.index";
        private const string PositionKey = "resume.positionSeconds";

        /// <summary>
        /// Loads the persisted state for the active server, index/position
        /// freshened from LocalSettings. Returns null — deleting the file
        /// best-effort — when nothing usable is stored: no file, corrupt
        /// content, or a state saved for a different server/user.
        /// </summary>
        public static async Task<ResumeState> LoadForActiveServerAsync()
        {
            string serverUrl;
            string username;
            if (!CredentialStore.TryGetActiveIdentity(out serverUrl, out username))
            {
                return null;
            }

            ResumeState state;
            try
            {
                var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileName);
                var file = item as StorageFile;
                if (file == null)
                {
                    return null;
                }

                state = ResumeState.Parse(await FileIO.ReadTextAsync(file));
            }
            catch (Exception)
            {
                state = null;
            }

            if (state == null
                || !string.Equals(state.ServerUrl, serverUrl, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(state.Username, username, StringComparison.Ordinal))
            {
                await ClearAsync();
                return null;
            }

            // The scalars written since the last file rewrite are fresher
            // than the file's snapshot; out-of-range values are ignored.
            var settings = ApplicationData.Current.LocalSettings.Values;
            int index = state.CurrentIndex;
            double position = state.PositionSeconds;
            object value;
            if (settings.TryGetValue(IndexKey, out value) && value is int storedIndex
                && storedIndex >= 0 && storedIndex < state.Tracks.Count)
            {
                index = storedIndex;
            }

            if (settings.TryGetValue(PositionKey, out value) && value is double storedPosition
                && storedPosition >= 0)
            {
                position = storedPosition;
            }

            if (index == state.CurrentIndex && position == state.PositionSeconds)
            {
                return state;
            }

            return new ResumeState(
                state.ServerUrl, state.Username, state.Tracks, state.PlayOrderIndices,
                index, position, state.ShuffleEnabled, state.RepeatMode, state.Context);
        }

        /// <summary>
        /// Persists the queue structure (and resets the scalars to the
        /// state's own index/position). Best effort: resume is a
        /// convenience, never worth failing playback over.
        /// </summary>
        public static async Task SaveAsync(ResumeState state)
        {
            try
            {
                string xml = ResumeState.Serialize(state);
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                    FileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, xml);
                UpdatePosition(state.CurrentIndex, state.PositionSeconds);
            }
            catch (Exception)
            {
                // Losing resume costs a couch shrug, nothing more.
            }
        }

        /// <summary>Cheap scalar update; safe from any thread.</summary>
        public static void UpdatePosition(int currentIndex, double positionSeconds)
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                settings[IndexKey] = currentIndex;
                settings[PositionKey] = positionSeconds;
            }
            catch (Exception)
            {
                // Same best-effort stance as SaveAsync.
            }
        }

        /// <summary>Removes all persisted resume state (sign-out / stop).</summary>
        public static async Task ClearAsync()
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                settings.Remove(IndexKey);
                settings.Remove(PositionKey);
                var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileName);
                if (item != null)
                {
                    await item.DeleteAsync();
                }
            }
            catch (Exception)
            {
                // A leftover file is re-validated (and re-deleted) at load.
            }
        }
    }
}
