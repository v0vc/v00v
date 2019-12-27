using System;

namespace v00v.ViewModel.Startup
{
    public interface IStartupModel
    {
        #region Properties

        string BaseDir { get; set; }
        bool DailyParsed { get; }
        TimeSpan DailySyncTime { get; }
        bool EnableDailySchedule { get; }
        bool EnableRepeatSchedule { get; }
        int RepeatMin { get; }
        bool RepeatParsed { get; }
        string WatchApp { get; set; }
        string YouParam { get; }
        string YouParser { get; }

        #endregion
    }
}
