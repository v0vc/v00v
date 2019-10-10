using System.Collections.Generic;

namespace v00v.Model.BackupEntities
{
    public class BackupAll
    {
        #region Properties

        public IEnumerable<BackupItem> Items { get; set; }

        public Dictionary<string, byte> ItemsState { get; set; }

        #endregion
    }
}