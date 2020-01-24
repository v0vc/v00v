using System;

namespace v00v.ViewModel.Startup
{
    public interface IStartupModel
    {
        #region Properties

        bool DailyParsed { get; }
        TimeSpan DailyParserUpdateTime { get; }
        TimeSpan DailySyncTime { get; }
        string DbDir { get; set; }
        string DownloadDir { get; set; }
        bool EnableCustomDb { get; }
        bool EnableDailySchedule { get; }
        bool EnableParserUpdateSchedule { get; }
        bool EnableRepeatSchedule { get; }
        bool ParserUpdateParsed { get; }
        int RepeatMin { get; }
        bool RepeatParsed { get; }
        string WatchApp { get; set; }
        string YouParam { get; }
        string YouParser { get; }

        #endregion

        #region Methods

        void UpdateParser(int i);

        #endregion
    }
}
