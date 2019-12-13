using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using v00v.Model.Entities;
using v00v.Model.SyncEntities;
using v00v.Services.Persistence;
using v00v.Services.Synchronization;

namespace v00v.Services.Dispatcher
{
    public interface ITaskDispatcher
    {
        #region Properties

        TimeSpan DailySync { set; }

        int RepeatSync { set; }

        #endregion

        #region Methods

        Task RunDaily(ISyncService syncService,
            IAppLogRepository appLog,
            List<Channel> entries,
            bool syncPls,
            Action<string> log,
            Action<SyncDiff> updateList);

        Task RunRepeat(ISyncService syncService,
            IAppLogRepository appLog,
            List<Channel> entries,
            bool syncPls,
            Action<string> log,
            Action<SyncDiff> updateList);

        Task Stop();

        #endregion
    }
}
