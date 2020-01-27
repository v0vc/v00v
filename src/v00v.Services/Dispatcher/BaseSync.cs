namespace v00v.Services.Dispatcher
{
    internal static class BaseSync
    {
        #region Constants

        public const string AppLog = "appLog";

        public const string DailySyncGroup = "sync_daily";
        public const string PeriodicSyncGroup = "sync_periodic";

        public const string DailyParserGroup = "parser_daily";
        public const string PeriodicParserGroup = "parser_periodic";

        public const string DailyBackupGroup = "backup_daily";
        public const string PeriodicBackupGroup = "backup_periodic";

        public const string DailySync = "daily sync";
        public const string DailyParser = "daily parser update";
        public const string DailyBackup = "daily backup";
        
        public const string PeriodicSync = "periodic sync";
        public const string PeriodicParser = "periodic parser update";
        public const string PeriodicBackup = "periodic backup";

        public const string PlaylistSync = "playlist sync";
        public const string Entries = "entries";
        public const string Log = "log";
        public const string SyncPls = "syncPls";
        public const string SyncService = "syncService";
        public const string UpdateList = "updatelist";
        public const string UpdateParser = "updateparser";
        public const string BackupService = "backupService";
        public const string RepeatParser = "repeatParser";
        public const string RepeatSync = "repeatSync";
        public const string RepeatBackup = "repeatBackup";

        #endregion
    }
}
