using System.Collections.Concurrent;
using System.Collections.Generic;

namespace v00v.Model.BackupEntities
{
    public class BackupAll
    {
        #region Properties

        public ConcurrentBag<BackupItem> Items { get; set; }
        public Dictionary<string, byte> ItemsState { get; set; }

        #endregion
    }
}
