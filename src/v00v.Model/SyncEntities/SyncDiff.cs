using System.Collections.Concurrent;
using System.Collections.Generic;
using v00v.Model.Entities;

namespace v00v.Model.SyncEntities
{
    public class SyncDiff
    {
        #region Constructors

        public SyncDiff(bool syncPls)
        {
            SyncPls = syncPls;
            Channels = new Dictionary<string, ChannelStats>();
            Items = new ConcurrentDictionary<string, SyncPrivacy>();
            NewItems = new List<Item>();
            ErrorSyncChannels = new List<string>();
            NoUnlistedAgain = new List<string>();
            DeletedItems = new List<string>();
            UnlistedItems = new List<string>();
            if (syncPls)
            {
                DeletedPlaylists = new List<string>();
                NewPlaylists = new List<Playlist>();
            }
        }

        #endregion

        #region Properties

        public Dictionary<string, ChannelStats> Channels { get; set; }
        public List<string> DeletedItems { get; }
        public List<string> DeletedPlaylists { get; }
        public List<string> ErrorSyncChannels { get; }
        public Dictionary<string, List<ItemPrivacy>> ExistPlaylists { get; set; }
        public ConcurrentDictionary<string, SyncPrivacy> Items { get; }
        public List<Item> NewItems { get; }
        public List<Playlist> NewPlaylists { get; }
        public List<string> NoUnlistedAgain { get; }
        public List<string> UnlistedItems { get; }
        public bool SyncPls { get; }

        #endregion
    }
}
