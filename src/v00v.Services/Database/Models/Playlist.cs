using System.Collections.Generic;

namespace v00v.Services.Database.Models
{
    public sealed class Playlist
    {
        #region Properties

        public Channel Channel { get; set; }
        public List<ItemPlaylist> Items { get; } = new();
        public string ChannelId { get; set; }
        public int Count { get; set; }
        public string Id { get; set; }
        public string SubTitle { get; set; }
        public byte SyncState { get; set; }
        public byte[] Thumbnail { get; set; }
        public string ThumbnailLink { get; set; }
        public string Title { get; set; }

        #endregion
    }
}
