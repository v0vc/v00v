using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using v00v.Model.Entities;
using v00v.Model.SyncEntities;
using v00v.Services.Backup;
using v00v.Services.Persistence;
using v00v.Services.Synchronization;

namespace v00v.Services.Dispatcher
{
    public interface ITaskDispatcher
    {
        #region Properties

        TimeSpan DailyBackup { set; }
        TimeSpan DailyParser { set; }
        TimeSpan DailySync { set; }
        int RepeatBackup { set; }
        int RepeatParser { set; }
        int RepeatSync { set; }

        #endregion

        #region Methods

        Task RunBackup(IBackupService backupService, IEnumerable<Channel> channels, Action<string> setLog, bool isRepeat);

        Task RunSynchronization(ISyncService syncService,
            IAppLogRepository appLog,
            List<Channel> entries,
            bool syncPls,
            Action<string> log,
            Action<SyncDiff> updateList,
            bool isRepeat);

        Task RunUpdateParser(Action<string> log, Action<int> runUpdate, bool isRepeat);

        Task Stop();

        #endregion
    }
}
