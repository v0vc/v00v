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
            //Items = new List<SyncPrivacy>();
            NewItems = new List<Item>();
            NewPlaylists = new List<Playlist>();
            ErrorSyncChannels = new List<string>();
            NoUnlistedAgain = new List<string>();
        }

        #endregion

        #region Properties

        public Dictionary<string, ChannelStats> Channels { get; set; }
        public List<string> DeletedPlaylists { get; }
        public List<string> ErrorSyncChannels { get; }
        public Dictionary<string, List<ItemPrivacy>> ExistPlaylists { get; set; }
        public Dictionary<string, SyncPrivacy> Items { get; }
        //public List<SyncPrivacy> Items { get; }
        public List<Item> NewItems { get; }
        public List<Playlist> NewPlaylists { get; }
        public List<string> NoUnlistedAgain { get; }
        public bool SyncPls { get; }

        #endregion
    }
}
