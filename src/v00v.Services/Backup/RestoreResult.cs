﻿namespace v00v.Services.Backup
{
    public struct RestoreResult
    {
        #region Properties

        public int ChannelsCount { get; set; }
        public int PlannedCount { get; set; }
        public int WatchedCount { get; set; }

        #endregion
    }
}
