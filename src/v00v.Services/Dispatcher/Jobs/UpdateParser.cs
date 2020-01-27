using System;
using System.Threading.Tasks;
using Quartz;

namespace v00v.Services.Dispatcher.Jobs
{
    [DisallowConcurrentExecution]
    internal class UpdateParser : IJob
    {
        #region Methods

        public async Task Execute(IJobExecutionContext context)
        {
            var isRepeat = (bool)context.JobDetail.JobDataMap[BaseSync.RepeatParser];
            if (isRepeat && context.PreviousFireTimeUtc == null)
            {
                return;
            }
            var setLog = (Action<string>)context.JobDetail.JobDataMap[BaseSync.Log];
            var log = isRepeat ? BaseSync.PeriodicUpdate : BaseSync.DailyUpdate;
            setLog?.Invoke($"{DateTime.Now:HH:mm:ss}: -=start {log}=-");
            var updateTask = (Action<int>)context.JobDetail.JobDataMap[BaseSync.UpdateParser];
            await Task.Run(() => updateTask?.Invoke(0)).ContinueWith(x =>
            {
                setLog?.Invoke(x.IsCompletedSuccessfully ? $"{log} completed" :
                               x.Exception == null ? $"{log} failed" : $"{log} failed: {x.Exception.Message}");
            });
            setLog?.Invoke($"{DateTime.Now:HH:mm:ss}: -=stop {log}=-");
        }

        #endregion
    }
}
