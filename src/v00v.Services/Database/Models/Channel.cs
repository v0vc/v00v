using System;
using System.Collections.Generic;

namespace v00v.Services.Database.Models
{
    public sealed class Channel
    {
        #region Properties

        public List<Item> Items { get; } = new();
        public List<Playlist> Playlists { get; } = new();
        public Site Site { get; set; }
        public ICollection<ChannelTag> Tags { get; } = new List<ChannelTag>();
        public int Count { get; set; }
        public string Id { get; set; }
        public long ItemsCount { get; set; }
        public long PlannedCount { get; set; }
        public int SiteId { get; set; }
        public long SubsCount { get; set; }
        public long SubsCountDiff { get; set; }
        public string SubTitle { get; set; }
        
        public byte Sync { get; set; }
        
        public byte[] Thumbnail { get; set; }
        public DateTime Timestamp { get; set; }
        public string Title { get; set; }
        public long ViewCount { get; set; }
        public long ViewCountDiff { get; set; }
        public long WatchedCount { get; set; }

        #endregion
    }
}
