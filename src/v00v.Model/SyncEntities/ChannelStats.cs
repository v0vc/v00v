using System;

namespace v00v.Model.SyncEntities
{
    public class ChannelStats
    {
        #region Properties

        public string Description { get; set; }
        public long ItemsCount { get; set; }
        public long SubsCount { get; set; }
        public DateTime Timestamp { get; set; }
        public long ViewCount { get; set; }

        #endregion
    }
}
