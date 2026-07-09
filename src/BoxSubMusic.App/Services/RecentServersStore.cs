using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoxSubMusic.Core.Setup;
using Windows.Storage;

namespace BoxSubMusic.App.Services
{
    /// <summary>
    /// Recent-servers cache as a LocalFolder XML file (decision 2026-07-09,
    /// requirements §16). The list logic and XML shape live in Core
    /// (RecentServers); this class only does the UWP file I/O. Corrupt or
    /// missing files read as empty — recents are a convenience, never a
    /// blocker for setup.
    /// </summary>
    internal static class RecentServersStore
    {
        private const string FileName = "recent-servers.xml";

        public static async Task<IReadOnlyList<RecentServer>> LoadAsync()
        {
            try
            {
                var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileName);
                var file = item as StorageFile;
                if (file == null)
                {
                    return new RecentServer[0];
                }

                string xml = await FileIO.ReadTextAsync(file);
                return RecentServers.Parse(xml);
            }
            catch (Exception)
            {
                return new RecentServer[0];
            }
        }

        public static async Task SaveAsync(IReadOnlyList<RecentServer> servers)
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                    FileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, RecentServers.Serialize(servers));
            }
            catch (Exception)
            {
                // Best effort: losing the cache costs retyping, nothing more.
            }
        }
    }
}
