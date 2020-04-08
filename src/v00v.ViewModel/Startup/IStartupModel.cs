using System;

namespace v00v.ViewModel.Startup
{
    public interface IStartupModel
    {
        #region Properties

        bool DailyBackupParsed { get; }
        TimeSpan DailyBackupTime { get; }
        bool DailyParserUpdateParsed { get; }
        TimeSpan DailyParserUpdateTime { get; }
        bool DailySyncParsed { get; }
        TimeSpan DailySyncTime { get; }
        string DbDir { get; set; }
        string DownloadDir { get; set; }
        bool EnableDailyBackupSchedule { get; }
        bool EnableDailyParserUpdateSchedule { get; }
        bool EnableDailySyncSchedule { get; }
        bool EnableRepeatBackupSchedule { get; }
        bool EnableRepeatParserUpdateSchedule { get; }
        bool EnableRepeatSyncSchedule { get; }
        int RepeatBackupMin { get; }
        bool RepeatBackupParsed { get; }
        int RepeatParserMin { get; }
        bool RepeatParserUpdateParsed { get; }
        int RepeatSyncMin { get; }
        bool RepeatSyncParsed { get; }
        string WatchApp { get; set; }
        string YouParam { get; }
        string YouParser { get; }

        #endregion

        #region Methods

        void UpdateParser(int i);

        #endregion
    }
}
