using System;

namespace v00v.ViewModel.Startup
{
    public interface IStartupModel
    {
        #region Properties

        bool DailyParserUpdateParsed { get; }
        TimeSpan DailyParserUpdateTime { get; }

        bool DailySyncParsed { get; }
        TimeSpan DailySyncTime { get; }
        string DbDir { get; set; }
        string DownloadDir { get; set; }
        bool EnableCustomDb { get; }
        bool EnableDailyDataBackupSchedule { get; }
        bool EnableDailyParserUpdateSchedule { get; }
        bool EnableDailySyncSchedule { get; }
        bool EnableRepeatDataBackupSchedule { get; }
        bool EnableRepeatParserUpdateSchedule { get; }
        bool EnableRepeatSyncSchedule { get; }
        int RepeatBackupMin { get; }
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
