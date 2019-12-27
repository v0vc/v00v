using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl;
using v00v.Model.Entities;
using v00v.Model.SyncEntities;
using v00v.Services.Persistence;
using v00v.Services.Synchronization;

namespace v00v.Services.Dispatcher
{
    public class TaskDispatcher : ITaskDispatcher
    {
        #region Static and Readonly Fields

        private static readonly object Locker = new object();

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

        public TimeSpan DailySync { private get; set; }
        public int RepeatSync { private get; set; }

        private IScheduler Scheduler
        {
            get
            {
                lock (Locker)
                {
                    return _scheduler ??= _factory.GetScheduler().GetAwaiter().GetResult();
                }
            }
        }

        #endregion

        #region Static Methods

        private static IJobDetail CreateJob<T>(string name,
            string groupname,
            ISyncService syncService,
            IAppLogRepository appLog,
            IReadOnlyCollection<Channel> entries,
            bool syncPls,
            Action<string> log,
            Action<SyncDiff> updateList) where T : IJob
        {
            var job = JobBuilder.Create<T>().WithIdentity(name, groupname).Build();
            job.JobDataMap[BaseSync.SyncService] = syncService;
            job.JobDataMap[BaseSync.AppLog] = appLog;
            job.JobDataMap[BaseSync.Entries] = entries;
            job.JobDataMap[BaseSync.SyncPls] = syncPls;
            job.JobDataMap[BaseSync.Log] = log;
            job.JobDataMap[BaseSync.UpdateList] = updateList;
            return job;
        }

        #endregion

        #region Methods

        public async Task RunDaily(ISyncService syncService,
            IAppLogRepository appLog,
            List<Channel> entries,
            bool syncPls,
            Action<string> log,
            Action<SyncDiff> updateList)
        {
            await CheckSchedulerStarted();

            var job = CreateJob<SyncDaily>(BaseSync.DailySync,
                                           BaseSync.DailyGroup,
                                           syncService,
                                           appLog,
                                           entries,
                                           syncPls,
                                           log,
                                           updateList);

            var trigger = TriggerBuilder.Create().WithIdentity(BaseSync.DailySync, BaseSync.DailyGroup).StartNow()
                .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(DailySync.Hours, DailySync.Minutes)).ForJob(job).Build();

            await Scheduler.ScheduleJob(job, trigger);
        }

        public async Task RunRepeat(ISyncService syncService,
            IAppLogRepository appLog,
            List<Channel> entries,
            bool syncPls,
            Action<string> log,
            Action<SyncDiff> updateList)
        {
            await CheckSchedulerStarted();

            var job = CreateJob<SyncRepeat>(BaseSync.PeriodicSync,
                                            BaseSync.PeriodicGroup,
                                            syncService,
                                            appLog,
                                            entries,
                                            syncPls,
                                            log,
                                            updateList);

            var trigger = TriggerBuilder.Create().WithIdentity(BaseSync.PeriodicSync, BaseSync.PeriodicGroup).StartNow()
                .WithSimpleSchedule(x => x.WithIntervalInMinutes(RepeatSync).RepeatForever()).ForJob(job).Build();

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
