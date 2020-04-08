using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Quartz;
using v00v.Model.Entities;
using v00v.Services.Backup;

namespace v00v.Services.Dispatcher.Jobs
{
    [DisallowConcurrentExecution]
    internal class BackupData : IJob
    {
        #region Methods

        public async Task Execute(IJobExecutionContext context)
        {
            var isRepeat = (bool)context.JobDetail.JobDataMap[BaseSync.RepeatBackup];
            if (isRepeat && context.PreviousFireTimeUtc == null)
            {
                return;
            }

            var setLog = (Action<string>)context.JobDetail.JobDataMap[BaseSync.Log];

            var log = isRepeat ? BaseSync.PeriodicBackup : BaseSync.DailyBackup;

            setLog?.Invoke($"{DateTime.Now:HH:mm:ss}: -=start {log}=-");

            var backupService = (IBackupService)context.JobDetail.JobDataMap[BaseSync.BackupService];

            var res = await backupService.Backup((IEnumerable<Channel>)context.JobDetail.JobDataMap[BaseSync.Entries], setLog);

            setLog?.Invoke($"Stored {res} items..");

            setLog?.Invoke($"{DateTime.Now:HH:mm:ss}: -=stop {log}=-");
        }

        #endregion
    }
}
