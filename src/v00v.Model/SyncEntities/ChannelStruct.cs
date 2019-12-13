using System.Collections.Generic;

namespace v00v.Model.SyncEntities
{
    public class ChannelStruct
    {
        #region Properties

        public string ChannelId { get; set; }
        public string ChannelTitle { get; set; }
        public IEnumerable<string> Items { get; set; }
        public IEnumerable<string> Playlists { get; set; }
        public IEnumerable<string> UnlistedItems { get; set; }

        #endregion
    }
}
