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
        string DailySyncSchedule { get; }
        string DailyParserUpdateSchedule { get; }
        string DownloadDir { get; }
        bool EnableDailyDataBackupSchedule { get; }
        bool EnableDailyParserUpdateSchedule { get; }
        bool EnableDailySyncSchedule { get; }
        bool EnableRepeatDataBackupSchedule { get; }
        bool EnableRepeatParserUpdateSchedule { get; }
        bool EnableRepeatSyncSchedule { get; }
        string KeyDailyDataBackupSchedule { get; }
        string KeyDailyParserUpdateSchedule { get; }
        string KeyDailySyncSchedule { get; }
        string KeyDbDir { get; }
        string KeyDownloadDir { get; }
        string KeyEnableCustomDb { get; }
        string KeyEnableDailyParserUpdateSchedule { get; }
        string KeyEnableDailySyncSchedule { get; }
        string KeyEnableRepeatParserUpdateSchedule { get; }
        string KeyEnableRepeatSyncSchedule { get; }
        string KeyRepeatDataBackupSchedule { get; }
        string KeyRepeatSyncSchedule { get; }
        string KeyRepeatParserUpdateSchedule { get; }
        string KeyWatchApp { get; }
        string KeyYouParam { get; }
        string KeyYouParser { get; }
        string RepeatParserUpdateSchedule { get; }
        string RepeatSyncSchedule { get; }
        string UseSqlite { get; set; }
        bool UseSqliteInit { get; set; }
        string WatchApp { get; }
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
