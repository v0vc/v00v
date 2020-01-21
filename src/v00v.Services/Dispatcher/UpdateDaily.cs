using System;
using System.Threading.Tasks;
using Quartz;

namespace v00v.Services.Dispatcher
{
    [DisallowConcurrentExecution]
    internal class UpdateDaily : IJob
    {
        #region Methods

        public async Task Execute(IJobExecutionContext context)
        {
            var setLog = (Action<string>)context.JobDetail.JobDataMap[BaseSync.Log];
            setLog?.Invoke($"{DateTime.Now:HH:mm:ss}: -=start {BaseSync.DailyUpdate}=-");
            var updateTask = (Action<int>)context.JobDetail.JobDataMap[BaseSync.UpdateParser];
            await Task.Run(() => updateTask?.Invoke(0)).ContinueWith(x =>
            {
                setLog?.Invoke(x.IsCompletedSuccessfully ? $"{BaseSync.DailyUpdate} completed" :
                               x.Exception == null ? $"{BaseSync.DailyUpdate} failed" :
                               $"{BaseSync.DailyUpdate} failed: {x.Exception.Message}");
            });
            setLog?.Invoke($"{DateTime.Now:HH:mm:ss}: -=stop {BaseSync.DailyUpdate}=-");
        }

        #endregion
    }
}
