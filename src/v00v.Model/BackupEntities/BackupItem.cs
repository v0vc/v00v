using System.Collections.Generic;

namespace v00v.Model.BackupEntities
{
    public class BackupItem
    {
        #region Properties

        public string ChannelId { get; set; }
        public string ChannelTitle { get; set; }
        public IEnumerable<int> Tags { get; set; }

        #endregion
    }
}
