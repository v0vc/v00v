using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl;
using v00v.Model.Entities;
using v00v.Model.SyncEntities;
using v00v.Services.Backup;
using v00v.Services.Dispatcher.Jobs;
using v00v.Services.Persistence;
using v00v.Services.Synchronization;

namespace v00v.Services.Dispatcher
{
    public class TaskDispatcher : ITaskDispatcher
    {
        #region Static and Readonly Fields

        private static readonly object _locker = new();

        private readonly StdSchedulerFactory _factory;

        #endregion

        #region Fields

        private IScheduler _scheduler;

        #endregion

        #region Constructors

        public TaskDispatcher()
        {
            _factory = new StdSchedulerFactory();
        }

        #endregion

        #region Properties

        public TimeSpan DailyBackup { private get; set; }
        public TimeSpan DailyParser { private get; set; }
        public TimeSpan DailySync { private get; set; }
        public int RepeatBackup { private get; set; }
        public int RepeatParser { private get; set; }
        public int RepeatSync { private get; set; }
        private IScheduler Scheduler
        {
            get
            {
                lock (_locker)
                {
                    return _scheduler ??= _factory.GetScheduler().GetAwaiter().GetResult();
                }
            }
        }

        #endregion

        #region Methods

        public async Task RunBackup(IBackupService backupService, IEnumerable<Channel> channels, Action<string> setLog, bool isRepeat)
        {
            await CheckSchedulerStarted();

            var job = JobBuilder.Create<BackupData>().WithIdentity(isRepeat ? BaseSync.PeriodicBackup : BaseSync.DailyBackup,
                                                                        isRepeat
                                                                            ? BaseSync.PeriodicBackupGroup
                                                                            : BaseSync.DailyBackupGroup).Build();

            job.JobDataMap[BaseSync.BackupService] = backupService;
            job.JobDataMap[BaseSync.Entries] = channels;
            job.JobDataMap[BaseSync.Log] = setLog;
            job.JobDataMap[BaseSync.RepeatBackup] = isRepeat;

            var trigger = isRepeat
                ? TriggerBuilder.Create().WithIdentity(BaseSync.PeriodicBackup, BaseSync.PeriodicBackupGroup).StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInMinutes(RepeatBackup).RepeatForever()).ForJob(job).Build()
                : TriggerBuilder.Create().WithIdentity(BaseSync.DailyBackup, BaseSync.DailyBackupGroup).StartNow()
                    .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(DailyBackup.Hours, DailyBackup.Minutes)).ForJob(job).Build();

            await Scheduler.ScheduleJob(job, trigger);
        }

        public async Task RunSynchronization(ISyncService syncService,
            IAppLogRepository appLog,
            List<Channel> entries,
            bool syncPls,
            Action<string> log,
            Action<SyncDiff> updateList,
            bool isRepeat)
        {
            await CheckSchedulerStarted();

            var job = JobBuilder.Create<SynchronizeData>().WithIdentity(isRepeat ? BaseSync.PeriodicSync : BaseSync.DailySync,
                                                                        isRepeat ? BaseSync.PeriodicSyncGroup : BaseSync.DailySyncGroup)
                .Build();

            job.JobDataMap[BaseSync.SyncService] = syncService;
            job.JobDataMap[BaseSync.AppLog] = appLog;
            job.JobDataMap[BaseSync.Entries] = entries;
            job.JobDataMap[BaseSync.SyncPls] = syncPls;
            job.JobDataMap[BaseSync.Log] = log;
            job.JobDataMap[BaseSync.UpdateList] = updateList;
            job.JobDataMap[BaseSync.RepeatSync] = isRepeat;

            var trigger = isRepeat
                ? TriggerBuilder.Create().WithIdentity(BaseSync.PeriodicSync, BaseSync.PeriodicSyncGroup).StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInMinutes(RepeatSync).RepeatForever()).ForJob(job).Build()
                : TriggerBuilder.Create().WithIdentity(BaseSync.DailySync, BaseSync.DailySyncGroup).StartNow()
                    .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(DailySync.Hours, DailySync.Minutes)).ForJob(job).Build();

            await Scheduler.ScheduleJob(job, trigger);
        }

        public async Task RunUpdateParser(Action<string> log, Action<int> runUpdate, bool isRepeat)
        {
            await CheckSchedulerStarted();

            var job = JobBuilder.Create<UpdateParser>().WithIdentity(isRepeat ? BaseSync.PeriodicParser : BaseSync.DailyParser,
                                                                     isRepeat ? BaseSync.PeriodicParserGroup : BaseSync.DailyParserGroup)
                .Build();

            job.JobDataMap[BaseSync.Log] = log;
            job.JobDataMap[BaseSync.UpdateParser] = runUpdate;
            job.JobDataMap[BaseSync.RepeatParser] = isRepeat;

            var trigger = isRepeat
                ? TriggerBuilder.Create().WithIdentity(BaseSync.PeriodicParser, BaseSync.PeriodicParserGroup).StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInMinutes(RepeatParser).RepeatForever()).ForJob(job).Build()
                : TriggerBuilder.Create().WithIdentity(BaseSync.DailyParser, BaseSync.DailyParserGroup).StartNow()
                    .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(DailyParser.Hours, DailyParser.Minutes)).ForJob(job).Build();

            await Scheduler.ScheduleJob(job, trigger);
        }

        public async Task Stop()
        {
            if (Scheduler != null)
            {
                foreach (var job in await Scheduler.GetCurrentlyExecutingJobs())
                {
                    await Scheduler.Interrupt(job.JobDetail.Key);
                    await Scheduler.UnscheduleJob(job.Trigger.Key);
                    await Scheduler.DeleteJob(job.JobDetail.Key);
                }

                await Scheduler.Clear();
                await Scheduler.Shutdown();
            }
        }

        private async Task CheckSchedulerStarted()
        {
            if (!Scheduler.IsStarted)
            {
                await Scheduler.Start();
            }
        }

        #endregion
    }
}
