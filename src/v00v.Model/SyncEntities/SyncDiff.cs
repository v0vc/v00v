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
            DeletedPlaylists = new List<string>();
            Items = new Dictionary<string, SyncPrivacy>();
            NewItems = new List<Item>();
            NewPlaylists = new List<Playlist>();
            ErrorSyncChannels = new List<string>();
        }

        #endregion

        #region Properties

        public Dictionary<string, ChannelStats> Channels { get; set; }
        public List<string> DeletedPlaylists { get; }
        public List<string> ErrorSyncChannels { get; }
        public Dictionary<string, List<ItemPrivacy>> ExistPlaylists { get; set; }
        public Dictionary<string, SyncPrivacy> Items { get; }
        public List<Item> NewItems { get; }
        public List<Playlist> NewPlaylists { get; }
        public bool SyncPls { get; }
        public bool TrueDiff => NewItems.Count > 0 || DeletedPlaylists?.Count > 0 || ExistPlaylists?.Count > 0 || NewPlaylists?.Count > 0;

        #endregion
    }
}
