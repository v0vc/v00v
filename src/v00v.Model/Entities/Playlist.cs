using System.Collections.Generic;
using Avalonia.Media.Imaging;
using v00v.Model.Enums;

namespace v00v.Model.Entities
{
    public class Playlist : BaseEntity
    {
        #region Properties

        public virtual Channel Channel { get; set; }

        public virtual List<string> Items { get; } = new List<string>();

        public string ChannelId { get; set; }

        public int Count { get; set; }

        public bool HasFullLoad { get; set; } = false;

        public bool HasNew => Count > 0;

        public string Id { get; set; }

        public bool IsStatePlaylist { get; set; }

        public string Link => $"https://www.youtube.com/playlist?list={Id}";

        public int Order { get; set; }

        public WatchState State { get; set; }

        public List<Item> StateItems { get; set; }

        public string SubTitle { get; set; }

        public byte SyncState { get; set; }

        public IBitmap Thumb => CreateThumb(Thumbnail);

        public byte[] Thumbnail { get; set; }

        public string ThumbnailLink { get; set; }

        public string Title { get; set; }

        #endregion
    }
}
