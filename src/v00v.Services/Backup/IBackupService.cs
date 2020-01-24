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
        string DownloadDir { get; }
        bool EnableDailySchedule { get; }
        bool EnableParserUpdateSchedule { get; }
        bool EnableRepeatSyncSchedule { get; }
        string KeyDailySyncSchedule { get; }
        string KeyDbDir { get; }
        string KeyDownloadDir { get; }
        string KeyEnableCustomDb { get; }
        string KeyEnableDailySchedule { get; }
        string KeyEnableParserUpdateSchedule { get; }
        string KeyEnableRepeatSyncSchedule { get; }
        string KeyParserUpdateSchedule { get; }
        string KeyRepeatSyncSchedule { get; }
        string KeyWatchApp { get; }
        string KeyYouParam { get; }
        string KeyYouParser { get; }
        string ParserUpdateSchedule { get; }
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
