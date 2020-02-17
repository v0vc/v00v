using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using v00v.Model.Entities;

namespace v00v.Services.Backup
{
    public interface IBackupService
    {
        #region Properties

        string AppSettings { get; }
        bool CustomDbEnabled { get; }
        string CustomDbPath { get; }
        string DailyBackupSchedule { get; }
        string DailyParserUpdateSchedule { get; }
        string DailySyncSchedule { get; }
        string DownloadDir { get; }
        bool EnableDailyBackupSchedule { get; }
        bool EnableDailyParserUpdateSchedule { get; }
        bool EnableDailySyncSchedule { get; }
        bool EnableRepeatBackupSchedule { get; }
        bool EnableRepeatParserUpdateSchedule { get; }
        bool EnableRepeatSyncSchedule { get; }
        string KeyDailyBackupSchedule { get; }
        string KeyDailyParserUpdateSchedule { get; }
        string KeyDailySyncSchedule { get; }
        string KeyDbDir { get; }
        string KeyDownloadDir { get; }
        string KeyEnableCustomDb { get; }
        string KeyEnableDailyBackupSchedule { get; }
        string KeyEnableDailyParserUpdateSchedule { get; }
        string KeyEnableDailySyncSchedule { get; }
        string KeyEnableRepeatBackupSchedule { get; }
        string KeyEnableRepeatParserUpdateSchedule { get; }
        string KeyEnableRepeatSyncSchedule { get; }
        string KeyRepeatBackupSchedule { get; }
        string KeyRepeatParserUpdateSchedule { get; }
        string KeyRepeatSyncSchedule { get; }
        string KeyWatchApp { get; }
        string KeyYouApiKey { get; }
        string KeyYouParam { get; }
        string KeyYouParser { get; }
        string RepeatBackupSchedule { get; }
        string RepeatParserUpdateSchedule { get; }
        string RepeatSyncSchedule { get; }
        string UseSqlite { get; set; }
        bool UseSqliteInit { get; set; }
        string WatchApp { get; }
        string YouApiKey { get; }
        string YouParam { get; }
        string YouParser { get; }

        #endregion

        #region Methods

        Task<int> Backup(IEnumerable<Channel> entries, Action<string> setLog);

        Task<RestoreResult> Restore(IEnumerable<string> existChannel,
            bool isFast,
            Action<string> setTitle,
            Action<Channel> updateList,
            Action<string> setLog);

        void SaveChanges(string key, string value);

        #endregion
    }
}
