namespace v00v.Services.Backup
{
    public record struct RestoreResult
    {
        #region Properties

        public int ChannelsCount { get; set; }
        public int PlannedCount { get; set; }
        public int WatchedCount { get; set; }

        #endregion
    }
}
