﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Quartz;
using v00v.Model.Entities;
using v00v.Model.Enums;
using v00v.Model.SyncEntities;
using v00v.Services.Persistence;
using v00v.Services.Synchronization;

namespace v00v.Services.Dispatcher
{
    [DisallowConcurrentExecution]
    internal class SyncRepeat : IJob
    {
        #region Methods

        public async Task Execute(IJobExecutionContext context)
        {
            if (context.PreviousFireTimeUtc == null)
            {
                return;
            }

            var appLog = (IAppLogRepository)context.JobDetail.JobDataMap[BaseSync.AppLog];
            var setLog = (Action<string>)context.JobDetail.JobDataMap[BaseSync.Log];
            var updateList = (Action<SyncDiff>)context.JobDetail.JobDataMap[BaseSync.UpdateList];
            setLog?.Invoke($"-=start {BaseSync.PeriodicSync}=-");

            var syncStatus = await appLog.GetAppSyncStatus(appLog.AppId);
            if (syncStatus != AppStatus.NoSync && syncStatus != AppStatus.DailySyncFinished
                                               && syncStatus != AppStatus.PeriodicSyncFinished
                                               && syncStatus != AppStatus.SyncPlaylistFinished
                                               && syncStatus != AppStatus.SyncWithoutPlaylistFinished)
            {
                setLog?.Invoke($"{syncStatus} in progress, bye");
                setLog?.Invoke($"-=stop {BaseSync.PeriodicSync}=-");
                return;
            }

            var syncService = (ISyncService)context.JobDetail.JobDataMap[BaseSync.SyncService];

            var syncPls = (bool)context.JobDetail.JobDataMap[BaseSync.SyncPls];

            setLog?.Invoke($"{BaseSync.PlaylistSync}:{syncPls}");

            await appLog.SetStatus(AppStatus.PeriodicSyncStarted, $"{BaseSync.PeriodicSync} started");

            var res = await syncService.Sync(false, syncPls, (List<Channel>)context.JobDetail.JobDataMap[BaseSync.Entries], setLog);

            var end = res == null ? "with error" : "ok";

            await appLog.SetStatus(AppStatus.PeriodicSyncFinished, $"{BaseSync.PeriodicSync} finished {end}");

            setLog?.Invoke($"-=stop {BaseSync.PeriodicSync}=-");

            updateList?.Invoke(res);
        }

        #endregion
    }
}
