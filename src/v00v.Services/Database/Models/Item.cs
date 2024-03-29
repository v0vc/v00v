﻿using System;
using System.Collections.Generic;

namespace v00v.Services.Database.Models
{
    public sealed class Item
    {
        #region Properties

        public Channel Channel { get; set; }
        public IEnumerable<ItemPlaylist> Playlists { get; } = new List<ItemPlaylist>();
        public string ChannelId { get; set; }
        public long Comments { get; set; }
        public string Description { get; set; }
        public long DislikeCount { get; set; }
        public int Duration { get; set; }
        public string FileName { get; set; }
        public string Id { get; set; }
        public long LikeCount { get; set; }
        public byte SyncState { get; set; }
        public byte[] Thumbnail { get; set; }
        public string ThumbnailLink { get; set; }
        public DateTime Timestamp { get; set; }
        public string Title { get; set; }
        public long ViewCount { get; set; }
        public long ViewDiff { get; set; }
        public byte WatchState { get; set; }

        #endregion
    }
}
