using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Quartz;
using v00v.Model.Entities;
using v00v.Model.Enums;
using v00v.Model.SyncEntities;
using v00v.Services.Persistence;
using v00v.Services.Synchronization;

namespace v00v.Services.Dispatcher.Jobs
{
    [DisallowConcurrentExecution]
    internal class SyncDaily : IJob
    {
        #region Methods

        public async Task Execute(IJobExecutionContext context)
        {
            var appLog = (IAppLogRepository)context.JobDetail.JobDataMap[BaseSync.AppLog];
            var setLog = (Action<string>)context.JobDetail.JobDataMap[BaseSync.Log];
            var updateList = (Action<SyncDiff>)context.JobDetail.JobDataMap[BaseSync.UpdateList];

            setLog?.Invoke($"{DateTime.Now:HH:mm:ss}: -=start {BaseSync.DailySync}=-");
            var syncStatus = await appLog.GetAppSyncStatus(appLog.AppId);
            if (syncStatus != AppStatus.NoSync && syncStatus != AppStatus.DailySyncFinished
                                               && syncStatus != AppStatus.PeriodicSyncFinished
                                               && syncStatus != AppStatus.SyncPlaylistFinished
                                               && syncStatus != AppStatus.SyncWithoutPlaylistFinished)
            {
                setLog?.Invoke($"{syncStatus} in progress, bye");
                setLog?.Invoke($"{DateTime.Now:HH:mm:ss}: -=stop {BaseSync.DailySync}=-");
                return;
            }

            var syncService = (ISyncService)context.JobDetail.JobDataMap[BaseSync.SyncService];

            var syncPls = (bool)context.JobDetail.JobDataMap[BaseSync.SyncPls];

            setLog?.Invoke($"{BaseSync.PlaylistSync}: {syncPls}");

            await appLog.SetStatus(AppStatus.DailySyncStarted, $"{BaseSync.DailySync} started");

            var res = await syncService.Sync(false, syncPls, (List<Channel>)context.JobDetail.JobDataMap[BaseSync.Entries], setLog);

            var end = res == null ? "with error" : "ok";

            await appLog.SetStatus(AppStatus.DailySyncFinished, $"{BaseSync.DailySync} finished {end}");

            setLog?.Invoke($"{DateTime.Now:HH:mm:ss}: -=stop {BaseSync.DailySync}=-");

            updateList?.Invoke(res);
        }

        #endregion
    }
}
