using System.Collections.Generic;
using v00v.Model.Entities;

namespace v00v.Model.SyncEntities
{
    public class ChannelDiff
    {
        #region Constructors

        public ChannelDiff(string channelId, bool syncPls)
        {
            ChannelId = channelId;
            AddedItems = new List<ItemPrivacy>();
            UploadedIds = new List<string>();
            DeletedItems = new List<string>();
            if (syncPls)
            {
                AddedPls = new Dictionary<Playlist, List<ItemPrivacy>>();
                DeletedPls = new List<string>();
                ExistPls = new Dictionary<string, List<ItemPrivacy>>();
            }
        }

        #endregion

        #region Properties

        public List<ItemPrivacy> AddedItems { get; }
        public Dictionary<Playlist, List<ItemPrivacy>> AddedPls { get; }
        public string ChannelId { get; }
        public List<string> DeletedItems { get; }
        public List<string> DeletedPls { get; }
        public string Description { get; set; }
        public Dictionary<string, List<ItemPrivacy>> ExistPls { get; }
        public bool Faulted { get; set; }
        public long SubsCount { get; set; }
        public List<string> UploadedIds { get; }
        public long ViewCount { get; set; }

        #endregion
    }
}
