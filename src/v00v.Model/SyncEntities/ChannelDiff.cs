using System.Collections.Concurrent;
using System.Collections.Generic;
using v00v.Model.Entities;

namespace v00v.Model.SyncEntities
{
    public class ChannelDiff
    {
        #region Constructors

        public ChannelDiff(string channelId, string channelTitle, bool syncPls)
        {
            ChannelId = channelId;
            ChannelTitle = channelTitle;
            AddedItems = new List<ItemPrivacy>();
            UploadedIds = new List<string>();
            DeletedItems = new List<string>();
            if (syncPls)
            {
                AddedPls = new ConcurrentDictionary<Playlist, List<ItemPrivacy>>();
                DeletedPls = new List<string>();
                ExistPls = new ConcurrentDictionary<string, List<ItemPrivacy>>();
            }
        }

        #endregion

        #region Properties

        public List<ItemPrivacy> AddedItems { get; }
        public ConcurrentDictionary<Playlist, List<ItemPrivacy>> AddedPls { get; }
        public string ChannelId { get; }
        public string ChannelTitle { get; }
        public List<string> DeletedItems { get; }
        public List<string> DeletedPls { get; }
        public string Description { get; set; }
        public ConcurrentDictionary<string, List<ItemPrivacy>> ExistPls { get; }
        public bool Faulted { get; set; }
        public long SubsCount { get; set; }
        public List<string> UploadedIds { get; }
        public long ViewCount { get; set; }

        #endregion
    }
}
