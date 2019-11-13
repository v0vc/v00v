using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using v00v.Model.Enums;

namespace v00v.Model.Entities
{
    public class Channel : BaseEntity
    {
        #region Fields

        private int _count;
        private string _title;

        #endregion

        #region Properties

        public virtual List<Item> Items { get; } = new List<Item>();

        public virtual List<Playlist> Playlists { get; } = new List<Playlist>();

        //public virtual Site Site { get; set; }

        public virtual List<Tag> Tags { get; } = new List<Tag>();

        public int Count
        {
            get => _count;
            set => Update(ref _count, value);
        }

        public string ExCache => "e" + Id;

        public bool HasNew => Count > 0;

        public string Id { get; set; }

        public bool IsStateChannel { get; set; } = false;
        public long ItemsCount { get; set; }
        public bool Loaded => Items.Count > 0;

        public long PlannedCount { get; set; }

        public string PlCache => "p" + Id;

        public long SubsCount { get; set; }

        public long SubsCountDiff { get; set; }

        public string SubTitle { get; set; }

        public SyncState Sync { get; set; }

        public IBitmap Thumb => CreateThumb(Thumbnail);

        public byte[] Thumbnail { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public string Title
        {
            get => _title;
            set => Update(ref _title, value);
        }

        public byte Type { get; set; }

        public long ViewCount { get; set; }

        public long ViewCountDiff { get; set; }

        public long WatchedCount { get; set; }

        public bool Working { get; set; }

        #endregion
    }
}
